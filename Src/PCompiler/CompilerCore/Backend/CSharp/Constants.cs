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
        // PST-P: Definition of tObserver class implemented on C#
        internal static readonly string tObsCs = @"
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Plang.CSharpRuntime.Values;
using System.Text.Json;
using Plang.CSharpRuntime;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace PImplementation
{
    public sealed class tObserver : IPrtValue
    {
        private static tObserver _instance;
        private readonly ConcurrentDictionary<string, Dictionary<int, PMachine>> _info = new ConcurrentDictionary<string, Dictionary<int, PMachine>>();
        private readonly ConcurrentDictionary<string, int> _counter = new ConcurrentDictionary<string, int>();

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

        public static void AddMachine(PrtString name, PMachine machine)
        {
            // Get information from instance
            var info = GetInstance()._info;
            var counter = GetInstance()._counter;
            var identifier = counter.GetValueOrDefault(name, 0);
            Dictionary<int, PMachine> machines;

            // We identify the machine to give it its identifier
            if (counter.ContainsKey(name))
            {
                // Already exists machines with that name
                machines = info.GetValueOrDefault(name, new Dictionary<int, PMachine>());
                // Add machine with that counter
                machines?.Add(identifier, machine);
            }
            else
            {
                // Is the first machine with that name
                machines = new Dictionary<int, PMachine> { { identifier, machine } };
            }
            
            // Add new information
            info.TryAdd(name, machines);
            // Update counter
            counter.TryAdd(name, identifier + 1);
        }

        public static ConcurrentDictionary<string, Dictionary<int, PMachine>> GetInfo()
        {
            return GetInstance()._info;
        }

        public static string Decode(object o)
        {
            // Add all P variables converters
            var options = new JsonSerializerOptions();
            options.Converters.Add(new PrtIntConverter());
            options.Converters.Add(new PrtStringConverter());
            // Serialize object with it type
            var serialize = JsonSerializer.Serialize(o, o.GetType(), options);
            // Remove numeric on variables p transcription
            return Regex.Replace(serialize, @""w*(_\d*)"", """");
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

        // PST-P: Implements functions to use observer pattern
        internal static readonly string tObsCsFunctions = @"
using Plang.CSharpRuntime;
using Plang.CSharpRuntime.Values;
using System;

namespace PImplementation
{
    public static partial class GlobalFunctions
    {
        public static PrtString Decode(object o, PMachine machine)
        {
            PrtString decode =  tObserver.Decode(o);
            machine.Log(decode);

            return decode;
        }

        public static void AddMachine(PrtString name, PMachine machine)
        {
            tObserver.AddMachine(name, machine);
        }

        public static object GetInfo()
        {
            return tObserver.GetInfo();
        }
    }
}
";
        // PST-P: Define P global functions to observation pattern
        internal static readonly string tObsP = @"
type tObserver;

fun Decode(o: any) : string;
fun AddMachine(name: string);
fun GetInfo() : any;

";

        internal static readonly string tConverters = @"
        using System;
        using System.Text.Json;
        using System.Text.Json.Serialization;
        using Plang.CSharpRuntime.Values;

        namespace PImplementation {
            public class PrtIntConverter : JsonConverter<PrtInt>
            {
                public override PrtInt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    return reader.GetInt32();
                }

                public override void Write(Utf8JsonWriter writer, PrtInt value, JsonSerializerOptions options)
                {
                    writer.WriteStringValue(((int)value).ToString());
                }
            }

            public class PrtStringConverter : JsonConverter<PrtString>
            {
                public override PrtString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
                {
                    return (PrtString)reader.GetString();
                }

                public override void Write(Utf8JsonWriter writer, PrtString value, JsonSerializerOptions options)
                {
                    writer.WriteStringValue(value.ToString());
                }
            }
        }
        ";
    }
}
