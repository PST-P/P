﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using PChecker.Coverage;
using PChecker.SystematicTesting;
using PChecker.Instrumentation;
using PChecker.Interfaces;
using PChecker.SmartSockets;
using PChecker.Utilities;

namespace PChecker.Testing
{
    /// <summary>
    /// A testing process, this can also be the client side of a multi-process test
    /// </summary>
    public class TestingProcess
    {
        /// <summary>
        /// Whether this process is terminating.
        /// </summary>
        private bool Terminating;

        /// <summary>
        /// A name for the test client
        /// </summary>
        private readonly string Name = "PCheckerProcess";

        /// <summary>
        /// CheckerConfiguration.
        /// </summary>
        private readonly CheckerConfiguration _checkerConfiguration;

        /// <summary>
        /// The testing engine associated with
        /// this testing process.
        /// </summary>
        private readonly TestingEngine TestingEngine;

        /// <summary>
        /// The channel to the TestProcessScheduler.
        /// </summary>
        private SmartSocketClient Server;

        /// <summary>
        /// A way to synchronouse background progress task with the main thread.
        /// </summary>
        private ProgressLock ProgressTask;

        /// <summary>
        /// Creates a Coyote testing process.
        /// </summary>
        public static TestingProcess Create(CheckerConfiguration checkerConfiguration)
        {
            return new TestingProcess(checkerConfiguration);
        }

        /// <summary>
        /// Get the current test report.
        /// </summary>
        public TestReport GetTestReport()
        {
            return this.TestingEngine.TestReport.Clone();
        }

        // Gets a handle to the standard output and error streams.
        private readonly TextWriter StdOut = Console.Out;

        /// <summary>
        /// Runs the Coyote testing process.
        /// </summary>
        public void Run()
        {
            this.RunAsync().Wait();
        }

        private async Task RunAsync()
        {
            if (this._checkerConfiguration.RunAsParallelBugFindingTask)
            {
                // Opens the remote notification listener.
                await this.ConnectToServer();

                this.StartProgressMonitorTask();
            }

            this.TestingEngine.Run();

            Console.SetOut(this.StdOut);

            this.Terminating = true;

            // wait for any pending progress
            var task = this.ProgressTask;
            if (task != null)
            {
                task.Wait(30000);
            }

            if (this._checkerConfiguration.RunAsParallelBugFindingTask)
            {
                if (this.TestingEngine.TestReport.InternalErrors.Count > 0)
                {
                    Environment.ExitCode = (int)ExitCode.InternalError;
                }
                else if (this.TestingEngine.TestReport.NumOfFoundBugs > 0)
                {
                    Environment.ExitCode = (int)ExitCode.BugFound;
                    await this.NotifyBugFound();
                }

                await this.SendTestReport();
            }

            if (!this._checkerConfiguration.PerformFullExploration &&
                this.TestingEngine.TestReport.NumOfFoundBugs > 0 &&
                !this._checkerConfiguration.RunAsParallelBugFindingTask)
            {
                Console.WriteLine($"... Process {this._checkerConfiguration.TestingProcessId} found a bug.");
            }

            // we want the graph generation even if doing full exploration.
            if ((!this._checkerConfiguration.PerformFullExploration && this.TestingEngine.TestReport.NumOfFoundBugs > 0) ||
                (this._checkerConfiguration.IsDgmlGraphEnabled && !this._checkerConfiguration.IsDgmlBugGraph))
            {
                await this.EmitTraces();
            }

            // Closes the remote notification listener.
            if (this._checkerConfiguration.IsVerbose)
            {
                Console.WriteLine($"... ### Process {this._checkerConfiguration.TestingProcessId} is terminating");
            }

            this.Disconnect();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestingProcess"/> class.
        /// </summary>
        private TestingProcess(CheckerConfiguration checkerConfiguration)
        {
            this.Name = this.Name + "." + checkerConfiguration.TestingProcessId;

            if (checkerConfiguration.SchedulingStrategy is "portfolio")
            {
                TestingPortfolio.ConfigureStrategyForCurrentProcess(checkerConfiguration);
            }

            if (checkerConfiguration.RandomGeneratorSeed.HasValue)
            {
                checkerConfiguration.RandomGeneratorSeed = checkerConfiguration.RandomGeneratorSeed.Value +
                    (673 * checkerConfiguration.TestingProcessId);
            }

            checkerConfiguration.EnableColoredConsoleOutput = true;

            this._checkerConfiguration = checkerConfiguration;
            this.TestingEngine = TestingEngine.Create(this._checkerConfiguration);
        }

        ~TestingProcess()
        {
            this.Terminating = true;
        }

        /// <summary>
        /// Opens the remote notification listener. If this is
        /// not a parallel testing process, then this operation
        /// does nothing.
        /// </summary>
        private async Task ConnectToServer()
        {
            string serviceName = this._checkerConfiguration.TestingSchedulerEndPoint;
            var source = new System.Threading.CancellationTokenSource();

            var resolver = new SmartSocketTypeResolver(typeof(BugFoundMessage),
                                                       typeof(TestReportMessage),
                                                       typeof(TestServerMessage),
                                                       typeof(TestProgressMessage),
                                                       typeof(TestTraceMessage),
                                                       typeof(TestReport),
                                                       typeof(CoverageInfo),
                                                       typeof(CheckerConfiguration));

            SmartSocketClient client = null;
            if (!string.IsNullOrEmpty(this._checkerConfiguration.TestingSchedulerIpAddress))
            {
                string[] parts = this._checkerConfiguration.TestingSchedulerIpAddress.Split(':');
                if (parts.Length == 2)
                {
                    var endPoint = new IPEndPoint(IPAddress.Parse(parts[0]), int.Parse(parts[1]));
                    while (!source.IsCancellationRequested && client == null)
                    {
                        client = await SmartSocketClient.ConnectAsync(endPoint, this.Name, resolver);
                    }
                }
            }
            else
            {
                client = await SmartSocketClient.FindServerAsync(serviceName, this.Name, resolver, source.Token);
            }

            if (client == null)
            {
                throw new Exception("Failed to connect to server");
            }

            client.Error += this.OnClientError;
            client.ServerName = serviceName;
            this.Server = client;

            // open back channel so server can also send messages to us any time.
            await client.OpenBackChannel(this.OnBackChannelConnected);
        }

        private void OnBackChannelConnected(object sender, SmartSocketClient e)
        {
            Task.Run(() => this.HandleBackChannel(e));
        }

        private async void HandleBackChannel(SmartSocketClient server)
        {
            while (!this.Terminating && server.IsConnected)
            {
                var msg = await server.ReceiveAsync();
                if (msg is TestServerMessage)
                {
                    this.HandleServerMessage((TestServerMessage)msg);
                }
            }
        }

        private void OnClientError(object sender, Exception e)
        {
            // todo: error handling, happens if we fail to get a message to the server for some reason.
        }

        /// <summary>
        /// Closes the remote notification listener. If this is
        /// not a parallel testing process, then this operation
        /// does nothing.
        /// </summary>
        private void Disconnect()
        {
            using (this.Server)
            {
                if (this.Server != null)
                {
                    this.Server.Close();
                }
            }
        }

        /// <summary>
        /// Notifies the remote testing scheduler
        /// about a discovered bug.
        /// </summary>
        private async Task NotifyBugFound()
        {
            await this.Server.SendReceiveAsync(new BugFoundMessage("BugFoundMessage", this.Name, this._checkerConfiguration.TestingProcessId));
        }

        /// <summary>
        /// Sends the test report associated with this testing process.
        /// </summary>
        private async Task SendTestReport()
        {
            var report = this.TestingEngine.TestReport.Clone();
            await this.Server.SendReceiveAsync(new TestReportMessage("TestReportMessage", this.Name, this._checkerConfiguration.TestingProcessId, report));
        }

        /// <summary>
        /// Emits the testing traces.
        /// </summary>
        private async Task EmitTraces()
        {
            string file = Path.GetFileNameWithoutExtension(this._checkerConfiguration.AssemblyToBeAnalyzed);
            file += "_" + this._checkerConfiguration.TestingProcessId;

            // If this is a separate (sub-)process, CodeCoverageInstrumentation.OutputDirectory may not have been set up.
            CodeCoverageInstrumentation.SetOutputDirectory(this._checkerConfiguration, makeHistory: false);

            Console.WriteLine($"... Emitting process {this._checkerConfiguration.TestingProcessId} traces:");
            var traces = new List<string>(this.TestingEngine.TryEmitTraces(CodeCoverageInstrumentation.OutputDirectory, file));

            if (this.Server != null && this.Server.IsConnected)
            {
                await this.SendTraces(traces);
            }
        }

        private async Task SendTraces(List<string> traces)
        {
            IPEndPoint localEndPoint = (IPEndPoint)this.Server.Socket.LocalEndPoint;
            IPEndPoint serverEndPoint = (IPEndPoint)this.Server.Socket.RemoteEndPoint;
            bool differentMachine = localEndPoint.Address.ToString() != serverEndPoint.Address.ToString();
            foreach (var filename in traces)
            {
                string contents = null;
                if (differentMachine)
                {
                    Console.WriteLine($"... Sending trace file: {filename}");
                    contents = File.ReadAllText(filename);
                }

                await this.Server.SendReceiveAsync(new TestTraceMessage("TestTraceMessage", this.Name, this._checkerConfiguration.TestingProcessId, filename, contents));
            }
        }

        /// <summary>
        /// Creates a task that pings the server with a heartbeat telling the server our current progress..
        /// </summary>
        private async void StartProgressMonitorTask()
        {
            while (!this.Terminating)
            {
                await Task.Delay(100);
                using (this.ProgressTask = new ProgressLock())
                {
                    await this.SendProgressMessage();
                }
            }
        }

        /// <summary>
        /// Sends the TestProgressMessage and if server cannot be reached, stop the testing.
        /// </summary>
        private async Task SendProgressMessage()
        {
            if (this.Server != null && !this.Terminating && this.Server.IsConnected)
            {
                double progress = 0.0; // todo: get this from the TestingEngine.
                try
                {
                    await this.Server.SendReceiveAsync(new TestProgressMessage("TestProgressMessage", this.Name, this._checkerConfiguration.TestingProcessId, progress));
                }
                catch (Exception)
                {
                    // can't contact the server, so perhaps it died, time to stop.
                    this.TestingEngine.Stop();
                }
            }
        }

        private void HandleServerMessage(TestServerMessage tsr)
        {
            if (tsr.Stop)
            {
                // server wants us to stop!
                if (this._checkerConfiguration.IsVerbose)
                {
                    this.StdOut.WriteLine($"... ### Client {this._checkerConfiguration.TestingProcessId} is being told to stop!");
                }

                this.TestingEngine.Stop();
                this.Terminating = true;
            }
        }

        internal class ProgressLock : IDisposable
        {
            private bool Disposed;
            private bool WaitingOnProgress;
            private readonly object SyncObject = new object();
            private readonly ManualResetEvent ProgressEvent = new ManualResetEvent(false);

            public ProgressLock()
            {
            }

            ~ProgressLock()
            {
                this.Dispose();
            }

            public void Wait(int timeout = 10000)
            {
                bool wait = false;
                lock (this.SyncObject)
                {
                    if (this.Disposed)
                    {
                        return;
                    }

                    this.WaitingOnProgress = true;
                    wait = true;
                }

                if (wait)
                {
                    this.ProgressEvent.WaitOne(timeout);
                }
            }

            public void Dispose()
            {
                lock (this.SyncObject)
                {
                    this.Disposed = true;
                    if (this.WaitingOnProgress)
                    {
                        this.ProgressEvent.Set();
                    }
                }

                GC.SuppressFinalize(this);
            }
        }
    }
}
