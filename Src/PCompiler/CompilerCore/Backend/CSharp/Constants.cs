namespace Plang.Compiler.Backend.CSharp
{
    internal class Constants
    {
        internal static readonly string csprojTemplate = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>netcoreapp3.1</TargetFramework>
    <ApplicationIcon />
    <OutputType>Exe</OutputType>
    <StartupObject />
    <LangVersion>latest</LangVersion>
    <OutputPath>POutput/</OutputPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Microsoft.Coyote"" Version=""1.0.5""/>
    <PackageReference Include=""PCSharpRuntime"" Version=""1.*""/>
  </ItemGroup>
</Project>";

        internal static readonly string mainCode = @"
using Microsoft.Coyote;
using Microsoft.Coyote.SystematicTesting;
using Microsoft.Coyote.Actors;
using System;
using System.Linq;
using System.IO;

namespace -projectName-
{
    public class _TestRegression {
        public static void Main(string[] args)
        {
            /*
            Configuration configuration = Configuration.Create();
            configuration.WithVerbosityEnabled(true);
            // update the path to the schedule file.
            string schedule = File.ReadAllText(""absolute path to *.schedule file"");
            configuration.WithReplayStrategy(schedule);
            TestingEngine engine = TestingEngine.Create(configuration, (Action<IActorRuntime>)PImplementation.<Name of the test case>.Execute);
            engine.Run();
            string bug = engine.TestReport.BugReports.FirstOrDefault();
                if (bug != null)
            {
                Console.WriteLine(bug);
            }
            */
        }
    }
}";
        // TODO: Custom implementation
        internal static readonly string tObsCs = @"
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Plang.CSharpRuntime.Values;

namespace PImplementation
{
    public sealed class tObserver : IPrtValue
    {
        private static tObserver _instance;
        private Dictionary<string, string> _states = new Dictionary<string, string>();

        // Singleton pattern
        private tObserver()
        {
        }

        public static tObserver GetInstance()
        {
            if (_instance == null)
            {
                _instance = new tObserver();
            }

            return _instance;
        }

        public static void SetState(PrtString m, PrtString s)
        {
            GetInstance()._states[m] = s;
        }

        public static PrtString GetState(PrtString m)
        {
            return GetInstance()._states.GetValueOrDefault(m, ""none"");
    }

    public bool Equals(IPrtValue other)
    {
        return other is tObserver;
    }

    public IPrtValue Clone()
    {
        var cloned = new tObserver();
        return cloned;
    }

    public string ToEscapedString()
    {
        return ""Observer"";
    }
}
}

";

        // Implements functions to use observer pattern
        internal static readonly string tObsCsFunctions = @"
using Plang.CSharpRuntime;
using Plang.CSharpRuntime.Values;

namespace PImplementation
{
    public static partial class GlobalFunctions
    {
        public static void SetState(PrtString m, PrtString s, PMachine machine)
        {
            machine.LogLine(""Observer: setting state "" + s + "" to machine "" + m);
        tObserver.SetState(m, s);
    }

    public static PrtString GetState(PrtString m, PMachine machine)
    {
        machine.LogLine(""Observer: getting state from machine "" + m);
        return tObserver.GetState(m);
    }
}
}
";
        // Define P global functions to observation pattern
        internal static readonly string tObsP = @"
type tObserver;

fun SetState(m : string, s: string);
fun GetState(m : string) : string;

";
        // TODO: End custom implementation
    }
}
