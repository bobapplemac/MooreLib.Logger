// SPDX-License-Identifier: MIT
// Copyright (c) 2026 Andrew J. Moore

using MooreLib.Logging;

internal class Program
{
    static async Task Main(string[] args)
    {
        using var log = new Logger(new LoggerOptions
        {
            LoggerName = "MooreLib.Logger.Demo",
            IncludeConsoleTimestamp = true,
            IncludeConsoleLogLevel = true,
            IncludeFileTimestamp = true,
            IncludeFileLogLevel = true,
            IncludeFileEntryMetadata = false,
            TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff",
            TimestampZone = LogTimestampZone.Local,
            MessageSeparator = " - ",
            ConsoleLoggingEnabled = true,
            MinimumConsoleLevel = LogLevel.Trace,
            MinimumStandardErrorLevel = LogLevel.Error,
            MinimumFileLevel = LogLevel.Trace,
            InlineResumePrefix = "↳ ",
            EntryIndentSize = 2,
            ArchivePolicy = new FileArchivePolicy.BySize(
                MaximumFileSizeBytes: 10 * 1024 * 1024,
                MaximumArchiveFiles: 5)
        });

        log.EnableFileLogging("MooreLib.Logger.Demo.log");

        log.Info("MooreLib.Logger demo starting.");
        log.WriteBlankLine();

        RunOneShotDemo(log);
        RunStructuredPropertyDemo(log);
        RunMultilineDemo(log);
        RunInlineDemo(log);
        RunNestedDemo(log);
        RunTreeClosureDemo(log);
        RunCompleteWithChildDemo(log);
        RunInterruptionDemo(log);
        RunExceptionDemo(log);
        RunConsoleDestinationDemo(log);

        await RunAsyncContextDemo(log);
        await RunConcurrentExplicitEntryDemo(log);

        log.WriteBlankLine();
        log.Info("MooreLib.Logger demo complete.");

        static void RunConsoleDestinationDemo(Logger log)
        {
            log.Info("Console destination control");
            log.DisableConsoleLogging();
            log.Info("This line is written to the file only.");
            log.EnableConsoleLogging();
            log.Info("Console logging re-enabled.");
            log.WriteBlankLine();
        }

        static void RunOneShotDemo(Logger log)
        {
            log.Info("One-shot logging");

            log.Trace("Trace message.");
            log.Debug("Debug message.");
            log.Info("Info message.");
            log.Warn("Warning message.");
            log.Error("Error message.");

            log.WriteBlankLine();
        }

        static void RunStructuredPropertyDemo(Logger log)
        {
            log.Info(
                "Connected to PLC.",
                new LogProperty("Address", "192.168.10.50"),
                new LogProperty("Program", "MainProgram"),
                new LogProperty("Slot", 0));

            using var entry = log.BeginInfo(
                "Processing controller.",
                new LogProperty("Address", "192.168.10.50"),
                new LogProperty("OperationId", Guid.NewGuid()));

            log.WriteLine(
                "Identity read.",
                new LogProperty("ControllerName", "PLC01"));

            log.CompleteEntry("Controller processing complete.");

            log.WriteBlankLine();
        }

        static void RunMultilineDemo(Logger log)
        {
            using var entry = log.BeginInfo("Deploying application.");

            log.WriteLine("Configuration validated.");
            log.WriteLine("Package downloaded.");
            log.WriteLine("Files installed.");
            log.CompleteEntry("Deployment complete.");

            log.WriteBlankLine();
        }

        static void RunInlineDemo(Logger log)
        {
            using var entry = log.BeginInlineInfo("Connecting to PLC - ");

            Thread.Sleep(150);
            log.Write("CONNECTED - ");

            Thread.Sleep(150);
            log.Write("PROGRAM: MainProgram - ");

            Thread.Sleep(150);
            log.CompleteEntry("SUCCESS");

            log.WriteBlankLine();
        }

        static void RunNestedDemo(Logger log)
        {
            using var parent = log.BeginInfo("Updating PLC clock.");

            log.WriteLine("Connected.");

            using (var identity = log.BeginInfo(parent, "Reading controller identity."))
            {
                log.WriteLine("Product: ControlLogix");
                log.WriteLine("Program: MainProgram");
                log.CompleteEntry(identity, "Identity complete.");
            }

            using (var clock = log.BeginInfo(parent, "Synchronizing clock."))
            {
                log.WriteLine("Current drift: 1.42 seconds.");
                log.WriteLine("Writing controller time.");
                log.CompleteEntry(clock, "Clock synchronized.");
            }

            log.CompleteEntry(parent, "PLC clock update complete.");

            log.WriteBlankLine();
        }

        static void RunTreeClosureDemo(Logger log)
        {
            using var parent = log.BeginInfo("Message-less tree completion demo.");

            using (var child = log.BeginInfo(parent, "Nested operation."))
            {
                log.WriteLine(child, "Work complete.");
                log.CompleteEntry(child, "Nested operation complete.");
            }

            // Because the parent has visible tree content but no terminal message, r16
            // emits a bare tree-closure marker (┴) instead of leaving the branch hanging.
            log.CompleteEntry(parent);
            log.WriteBlankLine();
        }

        static void RunCompleteWithChildDemo(Logger log)
        {
            using var parent = log.BeginInfo("Terminal-child completion demo.");
            log.WriteLine(parent, "Attempting connection.");

            var detail =
                "EXCEPTION" + Environment.NewLine +
                "Type: System.Net.Sockets.SocketException" + Environment.NewLine +
                "Message: Connection refused.";

            log.CompleteWithChild(parent, LogLevel.Error, detail);
            log.WriteBlankLine();
        }

        static void RunInterruptionDemo(Logger log)
        {
            using var entry = log.BeginInlineInfo("Downloading package - ");

            log.Write("25% ");
            log.Warn("Network latency detected.");

            log.Write("50% ");
            log.Write("75% ");
            log.CompleteEntry("100%");

            log.WriteBlankLine();
        }

        static void RunExceptionDemo(Logger log)
        {
            try
            {
                ThrowDemoException();
            }
            catch (Exception exception)
            {
                log.Error("Demonstration exception.", exception);
            }

            log.WriteBlankLine();

            static void ThrowDemoException()
            {
                throw new InvalidOperationException("Something went wrong during the demo.");
            }
        }

        static async Task RunAsyncContextDemo(Logger log)
        {
            using var parent = log.BeginInfo("AsyncLocal / ExecutionContext demo.");

            log.WriteLine("Before await.");

            await Task.Delay(100);

            log.WriteLine("After await.");

            await Task.Run(() =>
            {
                log.WriteLine("Ambient entry flowed into Task.Run.");
            });

            var threadCompleted = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var thread = new Thread(() =>
            {
                try
                {
                    log.WriteLine("Ambient entry flowed into a new Thread.");
                    threadCompleted.SetResult();
                }
                catch (Exception exception)
                {
                    threadCompleted.SetException(exception);
                }
            });

            thread.Start();
            await threadCompleted.Task;
            thread.Join();

            log.CompleteEntry(parent, "ExecutionContext demo complete.");

            log.WriteBlankLine();
        }

        static async Task RunConcurrentExplicitEntryDemo(Logger log)
        {
            using var parent = log.BeginInfo("Concurrent explicit-entry demo.");

            using var workerA = log.BeginInfo(
                parent,
                "Worker A",
                new LogProperty("Worker", "A"));

            using var workerB = log.BeginInfo(
                parent,
                "Worker B",
                new LogProperty("Worker", "B"));

            using var start = new ManualResetEventSlim(false);

            var taskA = Task.Run(async () =>
            {
                start.Wait();

                log.WriteLine(workerA, "A - step 1");
                await Task.Delay(120);
                log.WriteLine(workerA, "A - step 2");
                log.CompleteEntry(workerA, "A - complete");
            });

            var taskB = Task.Run(async () =>
            {
                start.Wait();

                log.WriteLine(workerB, "B - step 1");
                await Task.Delay(60);
                log.WriteLine(workerB, "B - step 2");
                log.CompleteEntry(workerB, "B - complete");
            });

            start.Set();
            await Task.WhenAll(taskA, taskB);

            log.CompleteEntry(parent, "Concurrent work complete.");
        }

    }
}
