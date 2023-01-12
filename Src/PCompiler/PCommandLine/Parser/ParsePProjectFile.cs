﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using PChecker.IO;
using PChecker.IO.Debugging;
using Plang.Compiler;

namespace Plang.Parser
{
    internal class ParsePProjectFile
    {

        /// <summary>
        /// Parse the P Project file
        /// </summary>
        /// <param name="projectFile">Path to the P project file</param>
        /// <param name="job">out parameter of P compilation job, after parsing the project file</param>
        /// <returns></returns>
        public void ParseProjectFile(string projectFile, out CompilerConfiguration job)
        {
            job = null;
            try
            {
                if (!CheckFileValidity.IsLegalPProjFile(projectFile, out var projectFilePath))
                {
                    throw new CommandlineParsingError($"Illegal P project file name {projectFile} or file {projectFilePath?.FullName} not found");
                }
                CommandLineOutput.WriteInfo($"----------------------------------------");
                CommandLineOutput.WriteInfo($"==== Loading project file: {projectFile}");

                var outputLanguage = CompilerOutput.CSharp;
                var inputFiles = new HashSet<string>();
                var generateSourceMaps = false;
                var projectDependencies = new HashSet<string>();

                // get all project dependencies and the input files
                var (fileInfos, list) = GetAllProjectDependencies(projectFilePath, inputFiles, projectDependencies);

                inputFiles.UnionWith(fileInfos);
                projectDependencies.UnionWith(list);

                if (inputFiles.Count == 0)
                {
                    Error.ReportAndExit("At least one .p file must be provided as input files, no input files found after parsing the project file");
                }

                // get project name
                var projectName = GetProjectName(projectFilePath);

                // get output directory
                var outputDirectory = GetOutputDirectory(projectFilePath);

                // get target language
                GetTargetLanguage(projectFilePath, ref outputLanguage, ref generateSourceMaps);

                job = new CompilerConfiguration(output: new DefaultCompilerOutput(outputDirectory), outputDir: outputDirectory,
                    outputLanguage: outputLanguage, inputFiles: inputFiles.ToList(), projectName: projectName, projectRoot: projectFilePath.Directory, projectDependencies: projectDependencies.ToList());

                CommandLineOutput.WriteInfo($"----------------------------------------");
            }
            catch (CommandlineParsingError ex)
            {
                Error.ReportAndExit($"<Error parsing project file>:\n {ex.Message}");
            }
            catch (Exception other)
            {
                Error.ReportAndExit($"<Internal Error>:\n {other.Message}\n <Please report to the P team or create a issue on GitHub, Thanks!>");
            }
        }

       

        /// <summary>
        /// Parse the P Project file and return all the input P files and project dependencies (includes transitive dependencies)
        /// </summary>
        /// <param name="projectFilePath">Path to the P Project file</param>
        /// <param name="preInputFiles"></param>
        /// <param name="preProjectDependencies"></param>
        /// <returns></returns>
        private (HashSet<string> inputFiles, HashSet<string> projectDependencies) GetAllProjectDependencies(FileInfo projectFilePath, HashSet<string> preInputFiles, HashSet<string> preProjectDependencies)
        {
            var projectDependencies = new HashSet<string>(preProjectDependencies);
            var inputFiles = new HashSet<string>(preInputFiles);
            var projectXml = XElement.Load(projectFilePath.FullName);
            projectDependencies.Add(GetProjectName(projectFilePath));
            // add all input files from the current project
            inputFiles.UnionWith(ReadAllInputFiles(projectFilePath));

            // get recursive project dependencies
            foreach (var projectDepen in projectXml.Elements("IncludeProject"))
            {
                if (!CheckFileValidity.IsLegalPProjFile(projectDepen.Value, out var fullProjectDepenPathName))
                {
                    throw new CommandlineParsingError($"Illegal P project file name {projectDepen.Value} or file {fullProjectDepenPathName?.FullName} not found");
                }

                CommandLineOutput.WriteInfo($"==== Loading project file: {fullProjectDepenPathName.FullName}");

                if (projectDependencies.Contains(GetProjectName(fullProjectDepenPathName))) continue;
                var inputsAndDependencies = GetAllProjectDependencies(fullProjectDepenPathName, inputFiles, projectDependencies);
                projectDependencies.UnionWith(inputsAndDependencies.projectDependencies);
                inputFiles.UnionWith(inputsAndDependencies.inputFiles);
            }

            return (inputFiles, projectDependencies);
        }

        /// <summary>
        /// Parse the Project Name from the pproj file
        /// </summary>
        /// <param name="projectFullPath">Path to the pproj file</param>
        /// <returns>project name</returns>
        private string GetProjectName(FileInfo projectFullPath)
        {
            string projectName;
            var projectXml = XElement.Load(projectFullPath.FullName);
            if (projectXml.Elements("ProjectName").Any())
            {
                projectName = projectXml.Element("ProjectName")?.Value;
                if (!CheckFileValidity.IsLegalProjectName(projectName))
                {
                    throw new CommandlineParsingError($"{projectName} is not a legal project name");
                }
            }
            else
            {
                throw new CommandlineParsingError($"Missing project name in {projectFullPath.FullName}");
            }

            return projectName;
        }

        /// <summary>
        /// Parse the output directory information from the pproj file
        /// </summary>
        /// <param name="fullPathName"></param>
        /// <returns>If present returns the passed directory path, else the current directory</returns>
        private DirectoryInfo GetOutputDirectory(FileInfo fullPathName)
        {
            var projectXml = XElement.Load(fullPathName.FullName);
            if (projectXml.Elements("OutputDir").Any())
                return Directory.CreateDirectory(projectXml.Element("OutputDir")?.Value);
            else
                return new DirectoryInfo(Directory.GetCurrentDirectory());
        }

        private void GetTargetLanguage(FileInfo fullPathName, ref CompilerOutput outputLanguage, ref bool generateSourceMaps)
        {
            var projectXml = XElement.Load(fullPathName.FullName);
            if (!projectXml.Elements("Target").Any()) return;
            switch (projectXml.Element("Target")?.Value.ToLowerInvariant())
            {
                case "c":
                    outputLanguage = CompilerOutput.C;
                    // check for generate source maps attribute
                    try
                    {
                        if (projectXml.Element("Target")!.Attributes("sourcemaps").Any())
                        {
                            generateSourceMaps = bool.Parse(projectXml.Element("Target")?.Attribute("sourcemaps")?.Value ?? string.Empty);
                        }
                    }
                    catch (Exception)
                    {
                        throw new CommandlineParsingError($"Expected true or false, received {projectXml.Element("Target")?.Attribute("sourcemaps")?.Value}");
                    }
                    break;

                case "csharp":
                    outputLanguage = CompilerOutput.CSharp;
                    break;

                case "java":
                    outputLanguage = CompilerOutput.Java;
                    break;

                case "symbolic":
                    outputLanguage = CompilerOutput.Symbolic;
                    break;

                default:
                    throw new CommandlineParsingError($"Expected c, csharp, java, or symbolic as target, received {projectXml.Element("Target")?.Value}");
            }
        }

        /// <summary>
        /// Read all the input P files included in the pproj 
        /// </summary>
        /// <param name="fullPathName">Path to the pproj file</param>
        /// <returns>List of the all the P files included in the project</returns>
        private HashSet<string> ReadAllInputFiles(FileInfo fullPathName)
        {
            var inputFiles = new HashSet<string>();
            var projectXml = XElement.Load(fullPathName.FullName);

            // get all files to be compiled
            foreach (var inputs in projectXml.Elements("InputFiles"))
            {
                foreach (var inputFileName in inputs.Elements("PFile"))
                {
                    var pFiles = new List<string>();
                    var inputFileNameFull = Path.Combine(Path.GetDirectoryName(fullPathName.FullName) ?? throw new InvalidOperationException(), inputFileName.Value);

                    if (Directory.Exists(inputFileNameFull))
                    {
                        var enumerate = new EnumerationOptions();
                        enumerate.RecurseSubdirectories = true;
                        foreach (var files in Directory.GetFiles(inputFileNameFull, "*.p", enumerate))
                        {
                            pFiles.Add(files);
                        }
                    }
                    else
                    {
                        pFiles.Add(inputFileNameFull);
                    }

                    foreach (var pFile in pFiles)
                    {
                        if (CheckFileValidity.IsLegalPFile(pFile, out var pFilePathName))
                        {
                            CommandLineOutput.WriteInfo($"....... includes p file: {pFilePathName.FullName}");
                            inputFiles.Add(pFilePathName.FullName);
                        }
                        else
                        {
                            throw new CommandlineParsingError($"Illegal P file name {pFile} or file {pFilePathName?.FullName} not found");
                        }
                    }
                }
            }

            return inputFiles;
        }
    }
}
