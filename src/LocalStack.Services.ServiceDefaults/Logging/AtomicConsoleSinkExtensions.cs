namespace LocalStack.Services.ServiceDefaults.Logging;

/// <summary>
/// Extension methods for configuring the AtomicConsoleSink.
/// </summary>
public static class AtomicConsoleSinkExtensions
{
    private const string DefaultOutputTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// Writes log events to the console using atomic writes to prevent fragmentation.
    /// Particularly useful in Lambda Test Tool environments where standard Console sink
    /// can produce character-by-character output.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="outputTemplate">A message template describing the format used to write to the sink.</param>
    /// <param name="formatProvider">Supplies culture-specific formatting information, or null.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration AtomicConsole(
        this LoggerSinkConfiguration sinkConfiguration,
        string outputTemplate = DefaultOutputTemplate,
        IFormatProvider? formatProvider = null,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);

        var formatter = new MessageTemplateTextFormatter(outputTemplate, formatProvider);
        var sink = new AtomicConsoleSink(formatter);

        return sinkConfiguration.Sink(sink, restrictedToMinimumLevel);
    }

    /// <summary>
    /// Writes log events to the console using atomic writes with a custom formatter.
    /// </summary>
    /// <param name="sinkConfiguration">Logger sink configuration.</param>
    /// <param name="formatter">Custom text formatter for log events.</param>
    /// <param name="restrictedToMinimumLevel">The minimum level for events passed through the sink.</param>
    /// <returns>Configuration object allowing method chaining.</returns>
    public static LoggerConfiguration AtomicConsole(
        this LoggerSinkConfiguration sinkConfiguration,
        ITextFormatter formatter,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose)
    {
        ArgumentNullException.ThrowIfNull(sinkConfiguration);
        ArgumentNullException.ThrowIfNull(formatter);

        var sink = new AtomicConsoleSink(formatter);
        return sinkConfiguration.Sink(sink, restrictedToMinimumLevel);
    }
}