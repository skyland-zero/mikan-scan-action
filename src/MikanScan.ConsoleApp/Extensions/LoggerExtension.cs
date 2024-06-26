namespace Microsoft.Extensions.Logging;

public static class LoggerExtension
{
    public static void LogLine(this ILogger logger)
    {
        logger.Log(LogLevel.Information, "--------------------------------------------------------");
    }
}