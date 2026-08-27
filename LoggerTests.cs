using MooreLib.Logging;

public sealed class LoggerTests
{
    private const string InstanceIdProperty = Logger.ReservedPropertyPrefix + "InstanceId";
    private const string EntrySequenceProperty = Logger.ReservedPropertyPrefix + "EntrySequence";
    private const string ParentEntrySequenceProperty = Logger.ReservedPropertyPrefix + "ParentEntrySequence";
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
    public void CompletedExplicitEntryHandlesThrowInsteadOfFallingBack()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Entry");
        log.CompleteEntry(entry, "Done");
        var count = events.Count;

        Assert.Throws<InvalidOperationException>(() => log.Write(entry, "Result"));
        Assert.Throws<InvalidOperationException>(() => log.WriteLine(entry, "Result"));
        Assert.Throws<InvalidOperationException>(() => log.CompleteEntry(entry));
        Assert.Throws<InvalidOperationException>(() => log.CompleteEntryInline(entry, "Final"));
        Assert.Equal(count, events.Count);
    }

    [Fact]
    public void ForeignEntryHandlesAreRejectedForExplicitOperations()
    {
        var firstEvents = new List<Logger.PhysicalEmission>();
        var secondEvents = new List<Logger.PhysicalEmission>();
        using var first = CreateLogger(firstEvents);
        using var second = CreateLogger(secondEvents);
        using var foreign = first.BeginInfo("Foreign");

        Assert.Throws<ArgumentException>(() => second.BeginInfo(foreign, "Child"));
        Assert.Throws<ArgumentException>(() => second.Info(foreign, "Attached"));
        Assert.Throws<ArgumentException>(() => second.WriteLine(foreign, "Work"));
        Assert.Throws<ArgumentException>(() => second.CompleteEntry(foreign, "Done"));
        Assert.Throws<ArgumentException>(() => second.CompleteWithChild(foreign, LogLevel.Error, "Failure"));
        Assert.Empty(secondEvents);
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
            log.Info("Message", new LogProperty(EntrySequenceProperty, 42)));
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
        Assert.Equal(child.EntrySequence, emission.Properties[EntrySequenceProperty]);
        Assert.Equal(parent.EntrySequence, emission.Properties[ParentEntrySequenceProperty]);
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
        Assert.Equal(hiddenParent.EntrySequence, child.Properties[ParentEntrySequenceProperty]);
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
        Assert.Equal(entry.EntrySequence, interruption.EntrySequence);
        Assert.Equal(17, interruption.Properties["OperationId"]);
        Assert.Equal("Interrupted", interruption.Properties[EntryTypeProperty]);
        Assert.Equal(entry.EntrySequence, interruption.Properties[EntrySequenceProperty]);
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
    public void InvalidStandardErrorLevelFailsDuringConstruction()
    {
        var events = new List<Logger.PhysicalEmission>();
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateLogger(events, new LoggerOptions
        {
            MinimumStandardErrorLevel = (LogLevel)999,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        }));
    }

    [Fact]
    public void EmptyTreeMarkersDoNotGainTrailingSpacesAndBlankCompletionUsesClosureMarker()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.WriteLine(string.Empty);
        log.CompleteEntry(string.Empty);

        Assert.Equal("├", events[1].Message);
        Assert.Equal("┴", events[2].Message);
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
        Assert.Equal(parent.EntrySequence, continuation.EntrySequence);
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
        Assert.Equal(parent.EntrySequence, parentContinuation.EntrySequence);
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
    public void DisposalFailureStillDisposesBackendAndAllowsFileToBeReopened()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");

        try
        {
            var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            });
            log.EnableFileLogging(path);
            log.SetTestFlushHook(() => throw new IOException("simulated flush failure"));

            Assert.Throws<IOException>(() => log.Dispose());

            using var replacement = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Fatal,
                MinimumFileLevel = LogLevel.Trace,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            });
            replacement.EnableFileLogging(path);
            replacement.Info("REOPENED");
            replacement.DisableFileLogging();

            Assert.Contains("REOPENED", File.ReadAllText(path), StringComparison.Ordinal);
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
                ArchivePolicy = new FileArchivePolicy.Daily(maximumArchiveDays: 5)
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

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\r\nb")]
    public void WriteRecognizesEmbeddedPhysicalLineSeparatorsAndLeavesFinalFragmentOpen(string message)
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.Write(message);

        Assert.Equal(3, events.Count);
        Assert.Equal("├ a", events[1].Message);
        Assert.Equal(Logger.PhysicalOutputKind.FragmentLine, events[1].Kind);
        Assert.Equal("├ b", events[2].Message);
        Assert.Equal(Logger.PhysicalOutputKind.FragmentLineOpen, events[2].Kind);
    }

    [Theory]
    [InlineData("a\n")]
    [InlineData("a\r")]
    [InlineData("a\r\n")]
    public void WriteEndingWithPhysicalLineSeparatorMatchesWriteLine(string message)
    {
        var writeEvents = new List<Logger.PhysicalEmission>();
        using var writeLog = CreateLogger(writeEvents);
        using var writeEntry = writeLog.BeginInfo("Root");
        writeLog.Write(message);

        var writeLineEvents = new List<Logger.PhysicalEmission>();
        using var writeLineLog = CreateLogger(writeLineEvents);
        using var writeLineEntry = writeLineLog.BeginInfo("Root");
        writeLineLog.WriteLine("a");

        Assert.Equal(writeLineEvents.Count, writeEvents.Count);
        Assert.Equal(writeLineEvents[1].Message, writeEvents[1].Message);
        Assert.Equal(writeLineEvents[1].Kind, writeEvents[1].Kind);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\r\n")]
    public void WriteOfOnlyPhysicalLineSeparatorMatchesParameterlessWriteLine(string message)
    {
        var writeEvents = new List<Logger.PhysicalEmission>();
        using var writeLog = CreateLogger(writeEvents);
        using var writeEntry = writeLog.BeginInfo("Root");
        writeLog.Write(message);

        var writeLineEvents = new List<Logger.PhysicalEmission>();
        using var writeLineLog = CreateLogger(writeLineEvents);
        using var writeLineEntry = writeLineLog.BeginInfo("Root");
        writeLineLog.WriteLine();

        Assert.Equal(writeLineEvents.Count, writeEvents.Count);
        Assert.Equal(writeLineEvents[1].Message, writeEvents[1].Message);
        Assert.Equal(writeLineEvents[1].Kind, writeEvents[1].Kind);
    }

    [Fact]
    public void WritePreservesMultipleEmbeddedPhysicalLineSeparators()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.Write("a\n\nb");

        Assert.Equal(4, events.Count);
        Assert.Equal("├ a", events[1].Message);
        Assert.Equal(Logger.PhysicalOutputKind.FragmentLine, events[1].Kind);
        Assert.Equal("├", events[2].Message);
        Assert.Equal(Logger.PhysicalOutputKind.FragmentLine, events[2].Kind);
        Assert.Equal("├ b", events[3].Message);
        Assert.Equal(Logger.PhysicalOutputKind.FragmentLineOpen, events[3].Kind);
    }

    [Fact]
    public void WriteCannotTerminateTerminalInlineCompletionLine()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.CompleteEntryInline("Result: ");
        var count = events.Count;

        Assert.Throws<InvalidOperationException>(() => log.Write("SUCCESS\n"));
        Assert.Equal(count, events.Count);

        log.Write("SUCCESS");
        log.CompleteEntry();
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
    public void ConsoleCanStartDisabledAndBeEnabledProspectively()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            ConsoleLoggingEnabled = false,
            MinimumConsoleLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        log.Info("suppressed");
        Assert.Empty(events);

        log.EnableConsoleLogging();
        log.Info("visible");

        var emission = Assert.Single(events);
        Assert.Equal("visible", emission.Message);
        Assert.True(emission.ConsoleVisible);
    }

    [Fact]
    public void ConsoleCanBeDisabledWhileFileRemainsVisible()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Trace,
            MinimumFileLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        log.SetTestFileLoggingEnabled(true);
        log.DisableConsoleLogging();
        log.Info("file only");

        var emission = Assert.Single(events);
        Assert.False(emission.ConsoleVisible);
        Assert.True(emission.FileVisible);
    }

    [Fact]
    public void EnablingConsoleMidEntryDoesNotReplaySuppressedContent()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            ConsoleLoggingEnabled = false,
            MinimumConsoleLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        using var entry = log.BeginInlineInfo("Hidden begin - ");
        log.Write("Hidden progress ");
        Assert.Empty(events);

        log.EnableConsoleLogging();
        log.Write("Future");
        log.CompleteEntry();

        Assert.DoesNotContain(events, e => e.Message.Contains("Hidden", StringComparison.Ordinal));
        Assert.Contains(events, e => e.Message.Contains("Future", StringComparison.Ordinal));
    }

    [Fact]
    public void DisablingConsoleInterruptsOpenInlineLineBeforeFileOnlyContinuation()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events, new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Trace,
            MinimumFileLevel = LogLevel.Trace,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        });

        log.SetTestFileLoggingEnabled(true);
        using var entry = log.BeginInlineInfo("Working - ");
        log.Write("25% ");
        log.DisableConsoleLogging();
        log.Write("50%");
        log.CompleteEntry();

        var breakEvent = Assert.Single(events.Where(e => e.Kind == Logger.PhysicalOutputKind.ForcedLineBreak));
        Assert.True(breakEvent.ConsoleVisible);
        Assert.True(breakEvent.FileVisible);
        Assert.Contains(events, e => e.Message.Contains("↳ 50%", StringComparison.Ordinal) && !e.ConsoleVisible && e.FileVisible);
    }

    [Fact]
    public void StandardErrorThresholdDefaultsToError()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Trace,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false
            });

            log.Warn("WARN ROUTE");
            log.Error("ERROR ROUTE");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.Contains("WARN ROUTE", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR ROUTE", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("ERROR ROUTE", stderr.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("WARN ROUTE", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void StandardErrorThresholdCanRouteWarningsAndAboveToStandardError()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Trace,
                MinimumStandardErrorLevel = LogLevel.Warning,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false
            });

            log.Info("INFO ROUTE");
            log.Warn("WARN ROUTE");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.Contains("INFO ROUTE", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("WARN ROUTE", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("WARN ROUTE", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NullStandardErrorThresholdRoutesAllConsoleOutputToStandardOutput()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Trace,
                MinimumStandardErrorLevel = null,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false
            });

            log.Error("ERROR TO STDOUT");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        Assert.Contains("ERROR TO STDOUT", stdout.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("ERROR TO STDOUT", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RealLoggerCanRunFileOnlyWithConsoleDisabledFromConstruction()
    {
        var root = Path.Combine(Path.GetTempPath(), "MooreLib.Logger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "Application.log");
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            Console.SetError(stderr);

            using (var log = new Logger(new LoggerOptions
            {
                ConsoleLoggingEnabled = false,
                MinimumFileLevel = LogLevel.Trace,
                IncludeFileTimestamp = false,
                IncludeFileLogLevel = false,
                IncludeFileEntryMetadata = false
            }))
            {
                log.EnableFileLogging(path);
                log.Info("FILE ONLY");
                log.DisableFileLogging();
            }
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }

        try
        {
            Assert.Equal(string.Empty, stdout.ToString());
            Assert.Equal(string.Empty, stderr.ToString());
            Assert.Contains("FILE ONLY", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedConsoleDisableRetainsWorkingConsoleConfiguration()
    {
        var originalOut = Console.Out;
        using var stdout = new StringWriter();

        try
        {
            Console.SetOut(stdout);
            using var log = new Logger(new LoggerOptions
            {
                MinimumConsoleLevel = LogLevel.Trace,
                MinimumStandardErrorLevel = null,
                IncludeConsoleTimestamp = false,
                IncludeConsoleLogLevel = false
            });

            log.SetTestConfigurationApplyHook(_ => throw new InvalidOperationException("Injected console-disable failure."));
            Assert.Throws<InvalidOperationException>(() => log.DisableConsoleLogging());

            log.SetTestConfigurationApplyHook(null);
            log.Info("CONSOLE STILL ACTIVE");
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains("CONSOLE STILL ACTIVE", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FailedFileEnableKeepsConsoleOnlyStateAndCanBeRetried()
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
            Assert.False(File.Exists(path));

            log.SetTestConfigurationApplyHook(null);
            log.EnableFileLogging(path);
            log.Info("RETRY SUCCEEDED");
            log.DisableFileLogging();
            Assert.Contains("RETRY SUCCEEDED", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FailedFileDisableRetainsWorkingFileConfiguration()
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
    public void FailedFileSwitchRetainsOldDestinationAndCanLaterSwitch()
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

            log.Info("OLD DESTINATION STILL ACTIVE");

            log.SetTestConfigurationApplyHook(null);
            log.EnableFileLogging(pathB);

            Assert.Contains("OLD DESTINATION STILL ACTIVE", File.ReadAllText(pathA), StringComparison.Ordinal);
            log.Info("NEW DESTINATION ACTIVE");
            log.DisableFileLogging();

            Assert.Contains("NEW DESTINATION ACTIVE", File.ReadAllText(pathB), StringComparison.Ordinal);
            Assert.DoesNotContain("NEW DESTINATION ACTIVE", File.ReadAllText(pathA), StringComparison.Ordinal);
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

        Assert.Equal(parent.EntrySequence, emission.EntrySequence);
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

        Assert.Equal(parent.EntrySequence, emission.EntrySequence);
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

        Assert.NotEqual(parent.EntrySequence, detached.EntrySequence);
        Assert.Equal(parent.EntrySequence, parentWork.EntrySequence);
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

        Assert.All(aEvents, e => Assert.Equal(entryA.EntrySequence, e.EntrySequence));
        Assert.All(bEvents, e => Assert.Equal(entryB.EntrySequence, e.EntrySequence));
    }

    [Fact]
    public void NestedEntriesRenderAsAncestryAwareTree()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Updating PLC clock.");

        log.WriteLine("Connected.");

        using (var identity = log.BeginInfo(parent, "Reading controller identity."))
        {
            log.WriteLine(identity, "Product: ControlLogix");
            log.WriteLine(identity, "Program: MainProgram");
            log.CompleteEntry(identity, "Identity complete.");
        }

        using (var clock = log.BeginInfo(parent, "Synchronizing clock."))
        {
            log.WriteLine(clock, "Current drift: 1.42 seconds.");
            log.WriteLine(clock, "Writing controller time.");
            log.CompleteEntry(clock, "Clock synchronized.");
        }

        log.CompleteEntry(parent, "PLC clock update complete.");

        Assert.Equal("Updating PLC clock.", events[0].Message);
        Assert.Equal("├ Connected.", events[1].Message);

        var identityBegin = Assert.Single(events.Where(e => e.Message == "Reading controller identity."));
        Assert.Equal("├ ", identityBegin.Prefix);
        Assert.Contains(events, e => e.Message == "│ ├ Product: ControlLogix");
        Assert.Contains(events, e => e.Message == "│ ├ Program: MainProgram");
        Assert.Contains(events, e => e.Message == "│ └ Identity complete.");

        var clockBegin = Assert.Single(events.Where(e => e.Message == "Synchronizing clock."));
        Assert.Equal("├ ", clockBegin.Prefix);
        Assert.Contains(events, e => e.Message == "│ ├ Current drift: 1.42 seconds.");
        Assert.Contains(events, e => e.Message == "│ ├ Writing controller time.");
        Assert.Contains(events, e => e.Message == "│ └ Clock synchronized.");

        Assert.Equal("└ PLC clock update complete.", events[^1].Message);
    }

    [Fact]
    public void DeeplyNestedEntriesRenderOneActiveVerticalColumnPerAncestor()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var root = log.BeginInfo("Root");
        using var child = log.BeginInfo(root, "Child");
        using var grandchild = log.BeginInfo(child, "Grandchild");

        log.WriteLine(grandchild, "Work");
        log.CompleteEntry(grandchild, "Grandchild done");
        log.CompleteEntry(child, "Child done");
        log.CompleteEntry(root, "Root done");

        var childBegin = Assert.Single(events.Where(e => e.Message == "Child"));
        Assert.Equal("├ ", childBegin.Prefix);

        var grandchildBegin = Assert.Single(events.Where(e => e.Message == "Grandchild"));
        Assert.Equal("│ ├ ", grandchildBegin.Prefix);

        Assert.Contains(events, e => e.Message == "│ │ ├ Work");
        Assert.Contains(events, e => e.Message == "│ │ └ Grandchild done");
        Assert.Contains(events, e => e.Message == "│ └ Child done");
        Assert.Equal("└ Root done", events[^1].Message);
    }

    [Fact]
    public void CompletedAncestorDoesNotRenderVerticalContinuationForSurvivingChild()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        using var child = log.BeginInfo(parent, "Child");

        log.CompleteEntry(parent, "Parent done");
        log.WriteLine(child, "Still active");
        log.CompleteEntry(child, "Child done");

        Assert.Contains(events, e => e.Message == "└ Parent done");
        Assert.Contains(events, e => e.Message == "  ├ Still active");
        Assert.Equal("  └ Child done", events[^1].Message);
    }

    [Fact]
    public void NestedInterruptedInlineEntryResumesWithinItsAncestryColumn()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        using var child = log.BeginInlineInfo(parent, "Child - ");

        log.Write(child, "25% ");
        log.Warn("Interrupt");
        log.Write(child, "50%");
        log.CompleteEntry(child);
        log.CompleteEntry(parent, "Parent done");

        var resume = Assert.Single(events.Where(e => e.Message.Contains("↳ 50%", StringComparison.Ordinal)));
        Assert.Equal("│ ↳ 50%", resume.Message);
    }

    [Fact]
    public void MessageLessCompletionClosesVisibleTreeWithClosureMarker()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        using var child = log.BeginInfo(parent, "Child");

        log.CompleteEntry(child, "Child done");
        log.CompleteEntry(parent);

        var childBegin = Assert.Single(events.Where(e => e.Message == "Child"));
        Assert.Equal("├ ", childBegin.Prefix);
        Assert.Contains(events, e => e.Message == "│ └ Child done");
        Assert.Equal("┴", events[^1].Message);
    }

    [Fact]
    public void MessageLessCompletionWithoutVisibleTreeContentRemainsSilent()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInfo("Root");

        log.CompleteEntry(entry);

        Assert.Single(events);
        Assert.Equal("Root", events[0].Message);
    }

    [Fact]
    public void MessageLessInlineCompletionDoesNotAppendTreeClosureMarker()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var entry = log.BeginInlineInfo("Working - ");

        log.Write("done");
        log.CompleteEntry(entry);

        Assert.DoesNotContain(events, e => e.Message.Contains("┴", StringComparison.Ordinal));
    }

    [Fact]
    public void CompleteWithChildCreatesTerminalChildAndNestsMultilineDetail()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        log.WriteLine(parent, "Step");
        log.CompleteWithChild(
            parent,
            LogLevel.Error,
            "EXCEPTION\nType: SocketException\nMessage: Connection refused");

        Assert.Equal("Parent", events[0].Message);
        Assert.Equal("├ Step", events[1].Message);
        Assert.Contains(events, e => e.Message == "EXCEPTION" && e.Prefix == "└ ");
        Assert.Contains(events, e => e.Message == "  ├ Type: SocketException");
        Assert.Equal("  └ Message: Connection refused", events[^1].Message);
        Assert.Throws<InvalidOperationException>(() => log.WriteLine(parent, "Too late"));
    }

    [Fact]
    public void CompleteWithChildExceptionRendersExceptionDetailBeneathTerminalChild()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        Exception exception;
        try
        {
            throw new InvalidOperationException("failure");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        log.CompleteWithChild(parent, LogLevel.Error, "EXCEPTION", exception);

        var heading = Assert.Single(events.Where(e => e.Message == "EXCEPTION"));
        Assert.Equal("└ ", heading.Prefix);
        Assert.Same(exception, heading.Exception);
        Assert.Contains(events.Skip(1), e => e.Message.StartsWith("  ├ ", StringComparison.Ordinal));
        Assert.StartsWith("  └ ", events[^1].Message, StringComparison.Ordinal);
    }



    [Fact]
    public void LogEntryIsTheInternalStateObjectAndCarriesDirectParentReference()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        using var child = log.BeginWarn(parent, "Child");

        Assert.Same(parent, child.Parent);
        Assert.Equal(LogLevel.Warning, child.Level);
        Assert.Equal(parent.Depth + 1, child.Depth);
        Assert.True(child.IsActive);
        Assert.Null(typeof(Logger).GetNestedType("EntryRecord", System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void EntrySequencesAreMonotonicWithinLoggerAndCarryInstanceScopedMetadata()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");
        using var child = log.BeginInfo(parent, "Child");

        log.WriteLine(child, "Work");

        Assert.Equal(1, parent.EntrySequence);
        Assert.Equal(2, child.EntrySequence);

        var work = Assert.Single(events.Where(e => e.Message.Contains("Work", StringComparison.Ordinal)));
        Assert.True(work.InstanceId.HasValue);
        Assert.Equal(log.InstanceId, work.InstanceId.Value);
        Assert.Equal(child.EntrySequence, work.EntrySequence);
        Assert.Equal(log.InstanceId, Assert.IsType<Guid>(work.Properties[InstanceIdProperty]));
        Assert.Equal(child.EntrySequence, Assert.IsType<long>(work.Properties[EntrySequenceProperty]));
        Assert.Equal(parent.EntrySequence, Assert.IsType<long>(work.Properties[ParentEntrySequenceProperty]));
    }


    [Fact]
    public async Task ConcurrentEntryCreationProducesUniqueMonotonicSequences()
    {
        var events = new List<Logger.PhysicalEmission>();
        using var log = CreateLogger(events);
        using var parent = log.BeginInfo("Parent");

        var tasks = Enumerable.Range(0, 32)
            .Select(index => Task.Run(() => log.BeginInfo(parent, $"Child {index}")))
            .ToArray();

        var children = await Task.WhenAll(tasks);
        try
        {
            var sequences = children.Select(entry => entry.EntrySequence).OrderBy(value => value).ToArray();
            Assert.Equal(Enumerable.Range(2, 32).Select(value => (long)value).ToArray(), sequences);
        }
        finally
        {
            foreach (var child in children)
            {
                log.CompleteEntry(child, "Done");
                child.Dispose();
            }
        }
    }

    [Fact]
    public void LoggerInstanceIdsAreGloballyDistinctAndLogEntryDoesNotExposeLegacyIdProperty()
    {
        var firstEvents = new List<Logger.PhysicalEmission>();
        var secondEvents = new List<Logger.PhysicalEmission>();
        using var first = CreateLogger(firstEvents);
        using var second = CreateLogger(secondEvents);

        Assert.NotEqual(Guid.Empty, first.InstanceId);
        Assert.NotEqual(Guid.Empty, second.InstanceId);
        Assert.NotEqual(first.InstanceId, second.InstanceId);

        Assert.Null(typeof(LogEntry).GetProperty("Id"));
        Assert.NotNull(typeof(LogEntry).GetProperty(nameof(LogEntry.EntrySequence)));
    }

    [Fact]
    public void PublicExplicitEntryApisUseLogEntryHandlesAndTerminalChildHasDistinctName()
    {
        var publicMethods = typeof(Logger).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);

        var entryTargetingNames = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Logger.Write),
            nameof(Logger.WriteLine),
            nameof(Logger.WriteEvent),
            nameof(Logger.Trace),
            nameof(Logger.Debug),
            nameof(Logger.Info),
            nameof(Logger.Warn),
            nameof(Logger.Error),
            nameof(Logger.Fatal),
            nameof(Logger.BeginEntry),
            nameof(Logger.BeginInline),
            nameof(Logger.BeginTrace),
            nameof(Logger.BeginDebug),
            nameof(Logger.BeginInfo),
            nameof(Logger.BeginWarn),
            nameof(Logger.BeginError),
            nameof(Logger.BeginFatal),
            nameof(Logger.BeginInlineTrace),
            nameof(Logger.BeginInlineDebug),
            nameof(Logger.BeginInlineInfo),
            nameof(Logger.BeginInlineWarn),
            nameof(Logger.BeginInlineError),
            nameof(Logger.BeginInlineFatal),
            nameof(Logger.CompleteEntry),
            nameof(Logger.CompleteEntryInline),
            nameof(Logger.CompleteWithChild)
        };

        Assert.DoesNotContain(
            publicMethods.Where(m => entryTargetingNames.Contains(m.Name)),
            m => m.GetParameters().Any(p => p.ParameterType == typeof(long)));

        Assert.DoesNotContain(
            publicMethods.Where(m => m.Name == nameof(Logger.CompleteEntry)),
            m => m.GetParameters().Any(p => p.ParameterType == typeof(LogLevel)));

        Assert.Contains(
            publicMethods.Where(m => m.Name == nameof(Logger.CompleteWithChild)),
            m => m.GetParameters().Length >= 3
                 && m.GetParameters()[0].ParameterType == typeof(LogEntry)
                 && m.GetParameters()[1].ParameterType == typeof(LogLevel));
    }

    [Fact]
    public void MultipleLoggerInstancesCanCoexistWithoutProcessLocalDestinationOwnership()
    {
        var options = new LoggerOptions
        {
            MinimumConsoleLevel = LogLevel.Fatal,
            IncludeConsoleTimestamp = false,
            IncludeConsoleLogLevel = false
        };

        using var first = new Logger(options);
        using var second = new Logger(options);

        Assert.NotEqual(first.InstanceId, second.InstanceId);
    }

}
