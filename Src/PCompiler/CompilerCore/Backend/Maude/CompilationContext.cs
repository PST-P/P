using System.Collections.Generic;
using System.IO;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.Maude
{
    internal class CompilationContext : CompilationContextBase
    {
        public CompilationContext(ICompilerConfiguration job) : base(job)
        {
            Names = new MaudeNameManager("PGEN_");

            FileName = $"{ProjectName}.maude";
            ProjectDependencies = job.ProjectDependencies.Count == 0 ? new List<string>() { ProjectName } : job.ProjectDependencies;
            GlobalFunctionClassName = "GlobalFunctions";
        }

        public MaudeNameManager Names { get; }

        public IEnumerable<PLanguageType> UsedTypes => Names.UsedTypes;

        public string GlobalFunctionClassName { get; }

        public string FileName { get; }

        public IList<string> ProjectDependencies { get; }

        public string GetStaticMethodQualifiedName(Function function)
        {
            return $"{GlobalFunctionClassName}.{Names.GetNameForDecl(function)}";
        }
    }
}