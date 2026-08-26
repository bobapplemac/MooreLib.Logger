using MooreLib;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using MooreLib.Logging;

namespace MooreLib.Logging.Tests;

public sealed class LoggerTests
{
    private const string EntryIdProperty = Logger.ReservedPropertyPrefix + "EntryId";
    private const string ParentEntryIdProperty = Logger.ReservedPropertyPrefix + "ParentEntryId";
    private const string EntryTypeProperty = Logger.ReservedPropertyPrefix + "EntryType";

    private static Logger CreateLogger(List<Logger.PhysicalEmission> emissions) =>
        CreateLogger(emissions, new LoggerOptions
        {
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false,
            MessageSeparator = " "
        });

    private static Logger CreateLogger(
        List<Logger.PhysicalEmission> emissions,
        LoggerOptions options) =>
        new(options, emissions.Add);

    [Fact]
    public void BasicMultilineEntryUsesContinuationAndTerminalMarkers()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Deploying.");

        log.WriteLine("Validated.");
        log.WriteLine("Downloaded.");
        log.CompleteEntry("Done.");

        Assert.Equal("Deploying.", events[0].Message);
        Assert.Equal("├ Validated.", events[1].Message);
        Assert.Equal("├ Downloaded.", events[2].Message);
        Assert.Equal("└ Done.", events[3].Message);
    }

    [Fact]
    public void InlineInterruptionPermanentlyClosesOldPhysicalLineAndResumes()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("Connecting - ");
        log.Write("25% ");

        log.Warn("Retry required");
        log.Write("50% ");
        log.CompleteEntry("100%");

        Assert.Contains(events, e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak);
        Assert.Contains(events, e => e.Message.Contains("↳ 50% ", StringComparison.Ordinal));
    }

    [Fact]
    public void InvalidExplicitEntryIdsThrowInsteadOfFallingBack()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        Assert.Throws<InvalidOperationException>(() => log.Write(123456, "Result"));
        Assert.Throws<InvalidOperationException>(() => log.WriteLine(123456, "Result"));
        Assert.Throws<InvalidOperationException>(() => log.CompleteEntry(123456));
        Assert.Throws<InvalidOperationException>(() => log.CompleteEntryInline(123456, "Final"));
        Assert.Empty(events);
    }

    [Fact]
    public void InvalidExplicitParentIdsThrowForBeginsAndAttachedEvents()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        Assert.Throws<InvalidOperationException>(() => log.BeginInfo(123456, "Child"));
        Assert.Throws<InvalidOperationException>(() => log.Info(123456, "Attached"));
        Assert.Empty(events);
    }

    [Fact]
    public void ExistingChildRemainsUsableAfterParentCompletes()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        using var child = log.BeginInfo(parent, "Child");

        log.CompleteEntry(parent);
        log.WriteLine(child, "Child is still active.");
        log.CompleteEntry(child, "Done.");

        Assert.Contains(events, e => e.Message.Contains("Child is still active.", StringComparison.Ordinal));
        Assert.Contains(events, e => e.Message.Contains("Done.", StringComparison.Ordinal));
    }

    [Fact]
    public void CompletedParentRejectsNewChildren()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        log.CompleteEntry(parent);

        Assert.Throws<InvalidOperationException>(() => { log.BeginInfo(parent, "Too late"); });
    }

    [Fact]
    public void EntryHandleDisposalIsIdempotentAfterExplicitCompletion()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        var entry = log.BeginInfo("Operation");
        log.CompleteEntry(entry, "Done");

        entry.Dispose();
        entry.Dispose();
    }

    [Fact]
    public void OneShotMultilineMessagesUseTheSamePhysicalPipelineWithoutExceptions()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        log.Info("one\r\ntwo\nthree\rfour");

        Assert.Equal(4, events.Count);
        Assert.Equal("one", events[0].Message);
        Assert.Equal("├ ", events[1].Prefix);
        Assert.Equal("two", events[1].Message);
        Assert.Equal("└ ", events[3].Prefix);
        Assert.Equal("four", events[3].Message);
    }

    [Fact]
    public void CompleteEntryInlineAllowsOnlyInlineWritesBeforeCompletion()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Validation");

        log.CompleteEntryInline("Result: ");
        log.Write("SUCCESS");
        Assert.Throws<InvalidOperationException>(() => log.WriteLine("Not allowed"));
        log.CompleteEntry();

        Assert.Contains(events, e => e.Message.Contains("└ Result: ", StringComparison.Ordinal));
    }

    [Fact]
    public void ReservedPropertyNamespaceIsRejected()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        Assert.Throws<ArgumentException>(() =>
            log.Info("Message", new LogProperty(EntryIdProperty, 42)));
    }

    [Fact]
    public void ChildPropertiesInheritAndOverrideParentProperties()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo(
            "Parent",
            new LogProperty("Environment", "Production"),
            new LogProperty("Shared", "Parent"));
        using var child = log.BeginInfo(
            parent,
            "Child",
            new LogProperty("Shared", "Child"));

        log.WriteLine(child, "Work", new LogProperty("Detail", 42));

        var emission = events[^1];
        Assert.Equal("Production", emission.Properties["Environment"]);
        Assert.Equal("Child", emission.Properties["Shared"]);
        Assert.Equal(42, emission.Properties["Detail"]);
        Assert.Equal(child.Id, emission.Properties[EntryIdProperty]);
        Assert.Equal(parent.Id, emission.Properties[ParentEntryIdProperty]);
    }

    [Fact]
    public void MultipleInterruptionsAlwaysResumeOnFreshPhysicalLines()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("A - ");

        log.Info("B");
        log.Write("A2 ");
        log.Warn("C");
        log.Write("A3 ");
        log.CompleteEntry("DONE");

        Assert.Equal(2, events.Count(e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak));
        Assert.Equal(2, events.Count(e => e.Message.Contains("↳ ", StringComparison.Ordinal)));
    }

    [Fact]
    public void MultilineExceptionUsesSameContinuationPipelineAndRetainsException()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        Exception exception;

        try
        {
            throw new InvalidOperationException("failure");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        log.Error("Operation failed", exception);

        Assert.True(events.Count >= 3);
        Assert.Equal("Operation failed", events[0].Message);
        Assert.Same(exception, events[0].Exception);

        Assert.Equal("├ ", events[1].Prefix);
        Assert.Equal("└ ", events[^1].Prefix);
    }

    [Fact]
    public void StructuredPropertiesCanBeUsedWithoutSubclassing()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        log.Info(
            "Connected",
            new LogProperty("Address", "10.0.0.1"),
            new LogProperty("Program", "Main"));

        var emission = Assert.Single(events);
        Assert.Equal("10.0.0.1", emission.Properties["Address"]);
        Assert.Equal("Main", emission.Properties["Program"]);
    }

    [Fact]
    public void SuppressedOneShotDoesNotInterruptVisibleInlineEntry()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Info,
            MinimumFileLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        using var entry = log.BeginInlineInfo("Working... ");
        log.Write("25% ");
        log.Trace("Very detailed diagnostic.");
        log.Write("50%");
        log.CompleteEntry();

        Assert.DoesNotContain(events, e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak);
        Assert.DoesNotContain(events, e => e.Level == LogLevel.Trace);
        Assert.DoesNotContain(events, e => e.Message.Contains("↳", StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressedAttachedChildDoesNotInterruptParent()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Info,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        using var parent = log.BeginInlineInfo("Parent - ");
        log.Write("A ");
        log.Trace(parent, "Hidden child");
        log.Write("B");
        log.CompleteEntry();

        Assert.DoesNotContain(events, e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak);
        Assert.DoesNotContain(events, e => e.Message.Contains("Hidden child", StringComparison.Ordinal));
    }

    [Fact]
    public void SuppressedLogicalEntryStillExistsForVisibleChildContext()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Info,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        using var hiddenParent = log.BeginTrace("Hidden parent");
        log.Error(hiddenParent, "Visible child");
        log.CompleteEntry(hiddenParent);

        var child = Assert.Single(events.Where(e => e.Message.Contains("Visible child", StringComparison.Ordinal)));
        Assert.Equal(hiddenParent.Id, child.Properties[ParentEntryIdProperty]);
    }

    [Fact]
    public void FilteringMatrixReportsConsoleAndFileVisibility()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Info,
            MinimumFileLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        log.SetTestFileLoggingEnabled(true);
        log.Trace("file only");
        log.Info("both");

        Assert.False(events[0].ConsoleVisible);
        Assert.True(events[0].FileVisible);
        Assert.True(events[1].ConsoleVisible);
        Assert.True(events[1].FileVisible);
    }

    [Fact]
    public void FilteringMatrixCanProduceConsoleOnlyOutput()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Trace,
            MinimumFileLevel = LogLevel.Info,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        log.SetTestFileLoggingEnabled(true);
        log.Trace("console only");

        var emission = Assert.Single(events);
        Assert.True(emission.ConsoleVisible);
        Assert.False(emission.FileVisible);
    }

    [Fact]
    public void FileOnlyCompetingEventStillInterruptsSharedPhysicalStream()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Info,
            MinimumFileLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });
        log.SetTestFileLoggingEnabled(true);

        using var entry = log.BeginInlineInfo("Working - ");
        log.Write("25% ");
        log.Trace("file-only diagnostic");
        log.Write("50%");
        log.CompleteEntry();

        Assert.Contains(events, e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak);
        Assert.Contains(events, e => e.Level == LogLevel.Trace && !e.ConsoleVisible && e.FileVisible);
        Assert.Contains(events, e => e.Message.Contains("↳ 50%", StringComparison.Ordinal));
    }

    [Fact]
    public void ForcedInterruptionCarriesOwnerPropertiesAndInterruptedMetadata()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo(
            "Operation - ",
            new LogProperty("OperationId", 17));

        log.Info("Other");

        var interruption = Assert.Single(events.Where(e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak));
        Assert.Equal(entry.Id, interruption.EntryId);
        Assert.Equal(17, interruption.Properties["OperationId"]);
        Assert.Equal("Interrupted", interruption.Properties[EntryTypeProperty]);
        Assert.Equal(entry.Id, interruption.Properties[EntryIdProperty]);
    }

    [Fact]
    public void ResumedInlineWriteCarriesResumeMetadata()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("Operation - ");

        log.Warn("Interrupt");
        log.Write("Resumed");
        log.CompleteEntry();

        var resume = Assert.Single(events.Where(e => e.Message.Contains("↳ Resumed", StringComparison.Ordinal)));
        Assert.Equal("Resume", resume.Properties[EntryTypeProperty]);
    }

    [Fact]
    public void ResumedCompletionCarriesResumeEndMetadata()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("Operation - ");

        log.Warn("Interrupt");
        log.CompleteEntry("Done");

        var resumeEnd = Assert.Single(events.Where(e => e.Message.Contains("↳ Done", StringComparison.Ordinal)));
        Assert.Equal("ResumeEnd", resumeEnd.Properties[EntryTypeProperty]);
    }

    [Fact]
    public void InvalidTimestampFormatFailsDuringConstruction()
    {
        var events = new List<Logger.PhysicalEmission>();
        Assert.Throws<ArgumentException>(() => CreateLogger(events, new LoggerOptions
        {
            TimestampFormat = "yyyy-MM-dd '",
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        }));
    }

    [Theory]
    [InlineData(-2, false)]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    public void ArchiveCountValidationAcceptsZeroAndUnlimited(int count, bool valid)
    {
        var events = new List<Logger.PhysicalEmission>();
        var options = new LoggerOptions
        {
            ArchivePolicy = new FileArchivePolicy.BySize(1024, count),
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        };

        if (valid)
        {
            using var log = CreateLogger(events, options);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateLogger(events, options));
        }
    }

    [Fact]
    public void EmptyTreeMarkersDoNotGainTrailingSpaces()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.WriteLine(string.Empty);
        log.CompleteEntry(string.Empty);

        Assert.Equal("├", events[1].Message);
        Assert.Equal("└", events[2].Message);
    }

    [Fact]
    public void EmptyResumeDoesNotGainTrailingWhitespace()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("Root - ");

        log.Info("Other");
        log.Write(string.Empty);
        log.CompleteEntry();

        Assert.Contains(events, e => e.Message.EndsWith("↳", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.Message.EndsWith("↳ ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AmbientEntryContextFlowsAcrossAwait()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        await Task.Yield();
        log.WriteLine("Still parent");
        log.CompleteEntry("Done");

        var continuation = Assert.Single(events.Where(e => e.Message.Contains("Still parent", StringComparison.Ordinal)));
        Assert.Equal(parent.Id, continuation.EntryId);
    }

    [Fact]
    public async Task AsyncChildContextsRestoreTheirOwnParentWithoutCorruptingCallerContext()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        async Task RunChild(string name)
        {
            using var child = log.BeginInfo(name);
            await Task.Yield();
            log.CompleteEntry("child done");
        }

        await Task.WhenAll(RunChild("A"), RunChild("B"));
        log.WriteLine("Parent still current");
        log.CompleteEntry("Parent done");

        var parentContinuation = Assert.Single(events.Where(e => e.Message.Contains("Parent still current", StringComparison.Ordinal)));
        Assert.Equal(parent.Id, parentContinuation.EntryId);
    }

    [Fact]
    public async Task ConcurrentInterruptionCannotAppendRawFragmentAfterCompetingEvent()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("A - ");
        using var firstWritten = new ManualResetEventSlim(false);
        using var otherWritten = new ManualResetEventSlim(false);

        var taskA = Task.Run(() =>
        {
            log.Write(entry, "25% ");
            firstWritten.Set();
            Assert.True(otherWritten.Wait(TimeSpan.FromSeconds(5)));
            log.Write(entry, "50%");
            log.CompleteEntry(entry);
        });

        var taskB = Task.Run(() =>
        {
            Assert.True(firstWritten.Wait(TimeSpan.FromSeconds(5)));
            log.Info("Other event");
            otherWritten.Set();
        });

        await Task.WhenAll(taskA, taskB);

        var otherIndex = events.FindIndex(e => e.Message.Contains("Other event", StringComparison.Ordinal));
        var resumeIndex = events.FindIndex(e => e.Message.Contains("↳ 50%", StringComparison.Ordinal));
        Assert.True(otherIndex >= 0);
        Assert.True(resumeIndex > otherIndex);
    }

    [Fact]
    public async Task ExistingChildSurvivesConcurrentParentCompletion()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        using var child = log.BeginInfo(parent, "Child");
        using var go = new ManualResetEventSlim(false);

        var completeParent = Task.Run(() =>
        {
            go.Wait();
            log.CompleteEntry(parent);
        });
        var useChild = Task.Run(() =>
        {
            go.Wait();
            log.WriteLine(child, "Still active");
            log.CompleteEntry(child, "Done");
        });

        go.Set();
        await Task.WhenAll(completeParent, useChild);

        Assert.Contains(events, e => e.Message.Contains("Still active", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => log.BeginInfo(parent, "New child"));
    }

    [Fact]
    public void WrapperAwareDispatchReportsApplicationCallsite()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);
        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Info,
                ConsoleLayout = "${callsite:className=true:methodName=true}|${message}",
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false
            });

            log.Info("CALLSITE");
            var rendered = writer.ToString();
            Assert.True(rendered.Contains(nameof(WrapperAwareDispatchReportsApplicationCallsite), StringComparison.Ordinal));
            Assert.False(rendered.Contains("MooreLib.Logger.Write", StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void EntryHandleDisposalAfterLoggerDisposalIsHarmless()
    {
        var events = new List<Logger.PhysicalEmission>();
        var log = CreateLogger(events);
        var entry = log.BeginInlineInfo("Active - ");
        log.Write("work");

        log.Dispose();
        entry.Dispose();

        Assert.Throws<ObjectDisposedException>(() => log.Info("too late"));
    }

    [Fact]
    public void DisposalFailureStillReleasesConsoleAndFileOwnership()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false
            });
            log.EnableFileLogging(path);
            log.SetTestFlushHook(() => throw new IOException("simulated flush failure"));

            Assert.Throws<IOException>(() => log.Dispose());

            using var replacement = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false
            });

            var registryProbe = Guid.NewGuid();
            DestinationOwnershipRegistry.AcquireFile(registryProbe, path);
            DestinationOwnershipRegistry.ReleaseFile(registryProbe, path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DestinationRegistryNormalizesAndReleasesFileOwnership()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "app.log");

        try
        {
            var normalized = DestinationOwnershipRegistry.AcquireFile(first, path);
            Assert.Throws<InvalidOperationException>(() => DestinationOwnershipRegistry.AcquireFile(second, normalized));
            DestinationOwnershipRegistry.ReleaseFile(first, path);
            DestinationOwnershipRegistry.AcquireFile(second, normalized);
            DestinationOwnershipRegistry.ReleaseFile(second, normalized);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SizeRolloverOccursAtPhysicalLineBoundaryAndKeepsActivePathStable()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false,
                ArchivePolicy = new FileArchivePolicy.BySize(32, 5)
            });
            log.EnableFileLogging(path);
            log.Info(new string('A', 64));
            log.Info("SECOND");
            log.DisableFileLogging();

            Assert.True(File.Exists(path));
            Assert.True(File.ReadAllText(path).Contains("SECOND", StringComparison.Ordinal));
            Assert.NotEmpty(Directory.GetFiles(root).Where(file => !Path.GetFullPath(file).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SizeRolloverDoesNotSplitOpenInlinePhysicalLine()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");
        var firstPart = new string('A', 40);
        var secondPart = new string('B', 40);

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false,
                ArchivePolicy = new FileArchivePolicy.BySize(32, 5)
            });
            log.EnableFileLogging(path);
            using var entry = log.BeginInlineInfo(firstPart);
            log.Write(secondPart);
            log.CompleteEntry();
            log.Info("NEXT");
            log.DisableFileLogging();

            var files = Directory.GetFiles(root);
            var combinedLineFile = files.Single(file => File.ReadAllText(file).Contains(firstPart + secondPart, StringComparison.Ordinal));
            Assert.NotEqual(Path.GetFullPath(path), Path.GetFullPath(combinedLineFile));
            Assert.True(File.ReadAllText(path).Contains("NEXT", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ZeroArchiveRetentionIsAcceptedByRealFileTarget()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false,
                ArchivePolicy = new FileArchivePolicy.BySize(16, 0)
            });
            log.EnableFileLogging(path);
            log.Info(new string('A', 40));
            log.Info("SECOND");
            log.DisableFileLogging();

            Assert.True(File.Exists(path));
            Assert.True(File.ReadAllText(path).Contains("SECOND", StringComparison.Ordinal));
            Assert.Single(Directory.GetFiles(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FileEnableDisableDuringInlineOutputUsesInterruptionAndResumeSemantics()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Info,
                MinimumFileLevel = LogLevel.Trace,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            });

            using var entry = log.BeginInlineInfo("A - ");
            log.Write("1 ");
            log.EnableFileLogging(path);
            log.Write("2 ");
            log.DisableFileLogging();
            log.Write("3");
            log.CompleteEntry();

            var console = writer.ToString();
            Assert.True(console.Contains("↳ 2 ", StringComparison.Ordinal));
            Assert.True(console.Contains("↳ 3", StringComparison.Ordinal));

            var file = File.ReadAllText(path);
            Assert.True(file.Contains("↳ 2 ", StringComparison.Ordinal));
            Assert.False(file.Contains("↳ 3", StringComparison.Ordinal));
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReenablingSameFilePathDoesNotInterruptOpenLine()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            });
            log.EnableFileLogging(path);
            using var entry = log.BeginInlineInfo("A - ");
            log.Write("1 ");
            log.EnableFileLogging(path);
            log.Write("2");
            log.CompleteEntry();
            log.DisableFileLogging();

            var text = File.ReadAllText(path);
            Assert.True(text.Contains("A - 1 2", StringComparison.Ordinal));
            Assert.False(text.Contains("↳", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DailyRolloverWaitsForOpenInlineLineToFinish()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");
        var simulatedDate = new DateTime(2026, 8, 25);

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false,
                ArchivePolicy = new FileArchivePolicy.Daily(MaximumArchiveDays: 5)
            });
            log.SetTestRolloverProviders(currentDateProvider: () => simulatedDate);
            log.EnableFileLogging(path);

            using var entry = log.BeginInlineInfo("BEFORE - ");
            log.Write("still same line");
            simulatedDate = simulatedDate.AddDays(1);
            log.CompleteEntry();
            log.Info("AFTER");
            log.DisableFileLogging();

            Assert.True(File.ReadAllText(path).Contains("AFTER", StringComparison.Ordinal));
            var archives = Directory.GetFiles(root).Where(file => !Path.GetFullPath(file).Equals(Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)).ToArray();
            Assert.NotEmpty(archives);
            Assert.Contains(archives, file => File.ReadAllText(file).Contains("BEFORE - still same line", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    public static IEnumerable<object[]> PhysicalLineSplitCases()
    {
        yield return new object[] { "", new[] { "" } };
        yield return new object[] { "a", new[] { "a" } };
        yield return new object[] { "a\n", new[] { "a", "" } };
        yield return new object[] { "a\nb", new[] { "a", "b" } };
        yield return new object[] { "a\n\n", new[] { "a", "", "" } };
        yield return new object[] { "\n", new[] { "", "" } };
        yield return new object[] { "\n\n", new[] { "", "", "" } };
        yield return new object[] { "a\r", new[] { "a", "" } };
        yield return new object[] { "a\rb", new[] { "a", "b" } };
        yield return new object[] { "a\r\r", new[] { "a", "", "" } };
        yield return new object[] { "\r", new[] { "", "" } };
        yield return new object[] { "\r\r", new[] { "", "", "" } };
        yield return new object[] { "a\r\n", new[] { "a", "" } };
        yield return new object[] { "a\r\nb", new[] { "a", "b" } };
        yield return new object[] { "a\r\n\r\n", new[] { "a", "", "" } };
        yield return new object[] { "\r\n", new[] { "", "" } };
        yield return new object[] { "\r\n\r\n", new[] { "", "", "" } };
    }

    [Theory]
    [MemberData(nameof(PhysicalLineSplitCases))]
    public void SplitPhysicalLinesPreservesEveryPhysicalLineIncludingTrailingEmptyLines(
        string input,
        string[] expected)
    {
        Assert.Equal(expected, Logger.SplitPhysicalLines(input));
    }

    [Fact]
    public void OneShotMultilineOutputPreservesTrailingNewlineAsAnEmptyPhysicalLine()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        log.Info("a\n");

        Assert.Equal(2, events.Count);
        Assert.Equal("a", events[0].Message);
        Assert.Equal(string.Empty, events[1].Message);
        Assert.Equal("└", events[1].Prefix);
    }

    [Fact]
    public void WriteBlankLineBypassesConfiguredSeverityThresholds()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Fatal,
            MinimumFileLevel = LogLevel.Fatal,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        log.SetTestFileLoggingEnabled(true);
        log.WriteBlankLine();

        var blank = Assert.Single(events);
        Assert.Equal(Logger.PhysicalOutputKind.BlankLine, blank.Kind);
        Assert.True(blank.ConsoleVisible);
        Assert.True(blank.FileVisible);
        Assert.Equal(string.Empty, blank.Message);
    }

    [Fact]
    public void WriteBlankLineInterruptsOpenInlineEntryAndForcesResume()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("Working - ");
        log.Write("25% ");

        log.WriteBlankLine();
        log.Write("50%");
        log.CompleteEntry();

        var interruptionIndex = events.FindIndex(e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak);
        var blankIndex = events.FindIndex(e => e.Kind == Logger.PhysicalOutputKind.BlankLine);
        var resumeIndex = events.FindIndex(e => e.Message.Contains("↳ 50%", StringComparison.Ordinal));
        Assert.True(interruptionIndex >= 0);
        Assert.True(blankIndex > interruptionIndex);
        Assert.True(resumeIndex > blankIndex);
    }

    [Fact]
    public void WriteBlankLineHasNoStateEffectWhenNoDestinationIsEnabled()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        log.SetTestDestinationVisibility(consoleEnabled: false, fileEnabled: false);
        using var entry = log.BeginInlineInfo("Suppressed - ");

        log.WriteBlankLine();
        Assert.Empty(events);

        log.SetTestDestinationVisibility(consoleEnabled: true, fileEnabled: false);
        log.Write("Future");
        log.CompleteEntry();

        Assert.DoesNotContain(events, e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak);
        Assert.Contains(events, e => e.Message.Contains("Future", StringComparison.Ordinal));
    }

    [Fact]
    public void EnablingDestinationMidEntryDoesNotReplaySuppressedContent()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Info,
            MinimumFileLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        using var entry = log.BeginTrace("Hidden begin");
        log.Write("Hidden progress ");
        Assert.Empty(events);

        log.SetTestFileLoggingEnabled(true);
        log.WriteLine("Future file-visible output");
        log.CompleteEntry();

        Assert.DoesNotContain(events, e => e.Message.Contains("Hidden begin", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.Message.Contains("Hidden progress", StringComparison.Ordinal));
        Assert.Contains(events, e => e.Message.Contains("Future file-visible output", StringComparison.Ordinal));
    }

    [Fact]
    public void LoweringMinimumLevelMidEntryDoesNotReplaySuppressedContent()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Info,
            MinimumFileLevel = LogLevel.Info,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        using var entry = log.BeginTrace("Hidden begin");
        log.Write("Hidden progress ");
        Assert.Empty(events);

        log.SetTestMinimumLevels(LogLevel.Trace, LogLevel.Trace);
        log.WriteLine("Future visible output");
        log.CompleteEntry();

        Assert.DoesNotContain(events, e => e.Message.Contains("Hidden begin", StringComparison.Ordinal));
        Assert.DoesNotContain(events, e => e.Message.Contains("Hidden progress", StringComparison.Ordinal));
        Assert.Contains(events, e => e.Message.Contains("Future visible output", StringComparison.Ordinal));
    }

    [Fact]
    public void FailedFileEnableReleasesProspectiveOwnershipAndKeepsConsoleOnlyState()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace
            });
            log.SetTestConfigurationApplyHook(candidate =>
            {
                if (candidate is not null) throw new InvalidOperationException("Injected configuration failure.");
            });

            Assert.Throws<InvalidOperationException>(() => log.EnableFileLogging(path));

            var probe = Guid.NewGuid();
            DestinationOwnershipRegistry.AcquireFile(probe, path);
            DestinationOwnershipRegistry.ReleaseFile(probe, path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedFileDisableRetainsWorkingFileConfigurationAndOwnership()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            });
            log.EnableFileLogging(path);
            log.SetTestConfigurationApplyHook(candidate =>
            {
                if (candidate is null) throw new InvalidOperationException("Injected disable failure.");
            });

            Assert.Throws<InvalidOperationException>(() => log.DisableFileLogging());
            var probe = Guid.NewGuid();
            Assert.Throws<InvalidOperationException>(() => DestinationOwnershipRegistry.AcquireFile(probe, path));

            log.SetTestConfigurationApplyHook(null);
            log.Info("STILL ACTIVE");
            log.DisableFileLogging();
            Assert.Contains("STILL ACTIVE", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedFileSwitchRetainsOldDestinationAndReleasesNewClaim()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var pathA = Path.Combine(root, "A.log");
        var pathB = Path.Combine(root, "B.log");

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            });
            log.EnableFileLogging(pathA);
            var normalizedB = Path.GetFullPath(pathB);
            log.SetTestConfigurationApplyHook(candidate =>
            {
                if (candidate is not null && Path.GetFullPath(candidate) == normalizedB)
                    throw new InvalidOperationException("Injected switch failure.");
            });

            Assert.Throws<InvalidOperationException>(() => log.EnableFileLogging(pathB));

            var probeA = Guid.NewGuid();
            Assert.Throws<InvalidOperationException>(() => DestinationOwnershipRegistry.AcquireFile(probeA, pathA));

            var probeB = Guid.NewGuid();
            DestinationOwnershipRegistry.AcquireFile(probeB, pathB);
            DestinationOwnershipRegistry.ReleaseFile(probeB, pathB);

            log.SetTestConfigurationApplyHook(null);
            log.Info("OLD DESTINATION STILL ACTIVE");
            log.DisableFileLogging();
            Assert.Contains("OLD DESTINATION STILL ACTIVE", File.ReadAllText(pathA), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public void WriteBlankLineReachesRealFileBelowFileSeverityThreshold()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Fatal,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            });
            log.EnableFileLogging(path);
            log.WriteBlankLine();
            log.DisableFileLogging();

            Assert.Equal(Environment.NewLine, File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public void LogicalWriteLinePreservesTrailingNewlineAsAdditionalPhysicalContinuation()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.WriteLine("a\n");
        log.CompleteEntry("done");

        Assert.Contains(events, e => e.Message == "├ a");
        Assert.Contains(events, e => e.Message == "├");
        Assert.Equal("└ done", events[^1].Message);
    }

    [Fact]
    public void CompletionPreservesTrailingNewlineAsTerminalEmptyPhysicalLine()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.CompleteEntry("done\r\n");

        Assert.Equal("├ done", events[^2].Message);
        Assert.Equal("└", events[^1].Message);
    }

    [Fact]
    public async Task AmbientEntryContextFlowsIntoTaskRun()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        await Task.Run(() =>
        {
            log.WriteLine("Task work");
        });

        log.CompleteEntry("Done");

        var emission = Assert.Single(
            events.Where(e => e.Message.Contains("Task work", StringComparison.Ordinal)));

        Assert.Equal(parent.Id, emission.EntryId);
    }

    [Fact]
    public void AmbientEntryContextFlowsIntoNewThread()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        Exception? threadException = null;

        var thread = new Thread(() =>
        {
            try
            {
                log.WriteLine("Thread work");
            }
            catch (Exception ex)
            {
                threadException = ex;
            }
        });

        thread.Start();
        thread.Join();

        Assert.Null(threadException);

        log.CompleteEntry("Done");

        var emission = Assert.Single(
            events.Where(e => e.Message.Contains("Thread work", StringComparison.Ordinal)));

        Assert.Equal(parent.Id, emission.EntryId);
    }

    [Fact]
    public async Task SuppressedExecutionContextDoesNotFlowAmbientEntry()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        Task task;

        using (ExecutionContext.SuppressFlow())
        {
            task = Task.Run(() =>
            {
                log.WriteLine("Detached work");
            });
        }

        await task;

        log.WriteLine("Parent work");
        log.CompleteEntry("Done");

        var detached = Assert.Single(
            events.Where(e => e.Message.Contains("Detached work", StringComparison.Ordinal)));

        var parentWork = Assert.Single(
            events.Where(e => e.Message.Contains("Parent work", StringComparison.Ordinal)));

        Assert.NotEqual(parent.Id, detached.EntryId);
        Assert.Equal(parent.Id, parentWork.EntryId);
    }

    [Fact]
    public async Task ConcurrentExplicitEntryWritesRemainIndependentlyAddressable()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);

        using var parent = log.BeginInfo("Parent");
        using var entryA = log.BeginInfo(parent, "A");
        using var entryB = log.BeginInfo(parent, "B");

        using var start = new ManualResetEventSlim(false);

        var taskA = Task.Run(() =>
        {
            start.Wait();

            log.WriteLine(entryA, "A1");
            log.WriteLine(entryA, "A2");
            log.CompleteEntry(entryA, "A done");
        });

        var taskB = Task.Run(() =>
        {
            start.Wait();

            log.WriteLine(entryB, "B1");
            log.WriteLine(entryB, "B2");
            log.CompleteEntry(entryB, "B done");
        });

        start.Set();

        await Task.WhenAll(taskA, taskB);

        log.CompleteEntry(parent, "Parent done");

        var aEvents = events
            .Where(e =>
                e.Message.Contains("A1", StringComparison.Ordinal) ||
                e.Message.Contains("A2", StringComparison.Ordinal) ||
                e.Message.Contains("A done", StringComparison.Ordinal))
            .ToArray();

        var bEvents = events
            .Where(e =>
                e.Message.Contains("B1", StringComparison.Ordinal) ||
                e.Message.Contains("B2", StringComparison.Ordinal) ||
                e.Message.Contains("B done", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, aEvents.Length);
        Assert.Equal(3, bEvents.Length);

        Assert.All(aEvents, e => Assert.Equal(entryA.Id, e.EntryId));
        Assert.All(bEvents, e => Assert.Equal(entryB.Id, e.EntryId));
    }

}
