using MooreLib.Logging;

public sealed class PortableLoggerTests
{
    [Fact]
    public void LoggerCanBeConstructedAndUsed()
    {
        using var log = new Logger(new LoggerOptions
        {
            ConsoleLoggingEnabled = false
        });

        log.Info("Portable logger smoke test.");
    }
}