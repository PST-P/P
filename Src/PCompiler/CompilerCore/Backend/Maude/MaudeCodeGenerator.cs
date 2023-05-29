using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.Maude
{
    public class MaudeCodeGenerator : ICodeGenerator
    {
        /// <summary>
        /// This compiler has a compilation stage.
        /// </summary>
        public bool HasCompilationStage => true;

        public void Compile(ICompilerConfiguration job)
        {
        }

        public IEnumerable<CompiledFile> GenerateCode(ICompilerConfiguration job, Scope globalScope)
        {
            var context = new CompilationContext(job);
            var maudeSource = GenerateSource(context, globalScope);
            return new List<CompiledFile> {maudeSource};
        }

        private CompiledFile GenerateSource(CompilationContext context, Scope globalScope)
        {
            var source = new CompiledFile(context.FileName);

            WriteSourcePrologue(context, source.Stream);

            // write the top level declarations
            foreach (var decl in globalScope.AllDecls)
            {
                WriteDecl(context, source.Stream, decl);
            }

            WriteSourceEpilogue(context, source.Stream);

            return source;
        }

        private void WriteSourcePrologue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, "load p-0.3.maude");
            context.WriteLine(output);
            context.WriteLine(output, $"mod {context.ProjectName} is");
            context.WriteLine(output, $"\t inc SYSTEM-EXEC .");
            context.WriteLine(output);
        }

        private void WriteSourceEpilogue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, "endm");
            context.WriteLine(output);
            context.WriteLine(output, "set trace off .");
            context.WriteLine(output, "set trace eqs on .");
            context.WriteLine(output, "set trace select off .");
            context.WriteLine(output, "trace select ----void-function-invocation");
            context.WriteLine(output, "             ----eval-function-invocation");
            context.WriteLine(output, "             ----function-invocation");
            context.WriteLine(output, "             eval-new");
            context.WriteLine(output, "             new-sentence");
            context.WriteLine(output, "             new");
            context.WriteLine(output, "             assignment");
            context.WriteLine(output, "             .");
        }

        private void WriteDecl(CompilationContext context, StringWriter output, IPDecl decl)
        {
            string declName;

            switch (decl)
            {
                case Function function:
                    break;

                case PEvent pEvent:
                    if (!pEvent.IsBuiltIn)
                    {
                        // WriteEvent(context, output, pEvent);
                    }
                    break;

                case Machine machine:
                    WriteMachine(context, output, machine);
                    break;

                case PEnum _:
                    break;

                case TypeDef typeDef:
                    break;

                case Implementation impl:
                    break;

                case SafetyTest safety:
                    WriteSafetyTestDecl(context, output, safety);
                    break;

                case Interface _:
                    break;

                case EnumElem _:
                    break;

                default:
                    break;
            }
        }
        
        private static void WriteSafetyTestDecl(CompilationContext context, StringWriter output, SafetyTest safety)
        {
            var originalName = safety.Name;
            var declName = context.Names.GetNameForDecl(safety);
            var testNameLower = $"{char.ToLower(declName[0]) + declName[1..]}M";

            // Write op definition for this machine
            context.WriteLine(output, $"op {declName} : -> Name .");
            context.WriteLine(output, $"op {testNameLower} : -> MachineDecl .");
            context.WriteLine(output, $"op {testNameLower} : -> TestDecl .");

            // Search original file to dump (Filename must be equals to machine name
            var strToSearch = $"test {declName}";
            var fileName = context.Job.InputPFiles.First(f => File.ReadAllText(f).Contains(strToSearch));
            var fileContent = File.ReadAllText(fileName);
            // Split content
            var firstIndex = fileContent.IndexOf(strToSearch, StringComparison.Ordinal);
            // Get only machine definition
            fileContent = fileContent[firstIndex..];
            // Clean content
            fileContent = CleanContent(fileContent);

            // Write machine definition
            context.WriteLine(output);
            context.WriteLine(output, $"eq {testNameLower} = {fileContent} .");
            context.WriteLine(output);
        }

        private static void WriteMachine(CompilationContext context, StringWriter output, Machine machine)
        {
            var baseName = machine.Name;
            var machineName = $"{char.ToLower(baseName[0]) + baseName[1..]}$Machine";

            // Write op definition for this machine
            context.WriteLine(output, $"op {baseName} : -> Name .");
            context.WriteLine(output, $"op {machineName} : -> MachineDecl .");

            // Write variables names
            foreach (var field in machine.Fields)
            {
                context.WriteLine(
                    output,
                    $"op {field.Name}$Attr : -> {GetMaudeType(field.Type)} ."
                );
            }

            var namesToReplace = new Dictionary<string, string>();
            
            foreach (var state in machine.States)
            {
                // Get name for declaration
                var stateName = $"{state.Name}$State";
                
                // Add that name to replace
                namesToReplace.Add(state.Name, stateName);
                
                // Add op
                context.WriteLine(output, $"op {stateName} : -> Name .");
            }
            
            // Search original file to dump (Filename must be equals to machine name)
            var strToSearch = $"machine {baseName}";
            var fileName = context.Job.InputPFiles.First(f => File.ReadAllText(f).Contains(strToSearch));
            var fileContent = File.ReadAllText(fileName);
            // Split content
            var firstIndex = fileContent.IndexOf(strToSearch, StringComparison.Ordinal);
            // Get only machine definition
            fileContent = fileContent[firstIndex..];
            // Clean content
            fileContent = CleanContent(fileContent);
            
            // Replaces all names
            foreach (var (k, v) in namesToReplace)
            {
                fileContent = Regex.Replace(fileContent, $@"[\s]{k}[\s]", $" {v} ");
            }

            // Write machine definition
            context.WriteLine(output);
            context.WriteLine(output, $"eq {machineName}");
            context.WriteLine(output, $"\t= {fileContent} .");
            context.WriteLine(output);
        }

        private static string CleanContent(string fileContent)
        {
            // Remove comments of one line
            fileContent = Regex.Replace(fileContent, @"//.*", "");
            // Fix white spaces for maude
            const string patterns = @"(\<\=|\=|\;|\+|\-|\{|\}|\:)";
            fileContent = Regex.Replace(fileContent, $@"{patterns}(?!\s)", "$1 ");
            fileContent = Regex.Replace(fileContent, $@"(?!\s){patterns}", " $1");

            // Return clean content
            return fileContent;
        }

        private static string GetMaudeType(PLanguageType type, bool isVar = false)
        {
            return type.Canonicalize() switch
            {
                EnumType _ => "IntVarId",
                MapType _ => "MapVarId",
                PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int) => "IntVarId",
                PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Machine) => "MachVarId",
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }
    }
}