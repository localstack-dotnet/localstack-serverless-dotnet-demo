namespace LocalStack.Services.ServiceDefaults.Logging;

/// <summary>
/// A Serilog sink that writes log events to the console atomically to prevent
/// character-by-character fragmentation in Lambda Test Tool environments.
/// </summary>
internal sealed class AtomicConsoleSink : ILogEventSink, IDisposable
{
    private readonly ITextFormatter _formatter;
    private readonly TextWriter _output;
    private readonly object _syncRoot = new();
    private bool _disposed;

    public AtomicConsoleSink(ITextFormatter formatter, TextWriter? output = null)
    {
        _formatter = formatter ?? throw new ArgumentNullException(nameof(formatter));
        _output = output ?? Console.Out;
    }

    public void Emit(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        switch (_disposed)
        {
            case true:
                throw new ObjectDisposedException(nameof(AtomicConsoleSink));
            default:
                try
                {
                    // Format the entire log message in memory first
                    using var stringWriter = new StringWriter();
                    _formatter.Format(logEvent, stringWriter);
                    var formattedMessage = stringWriter.ToString();

                    // Ensure the message ends with a newline for proper line boundaries
                    if (!formattedMessage.EndsWith(Environment.NewLine))
                    {
                        formattedMessage += Environment.NewLine;
                    }

                    // Write atomically with lock to prevent interleaving
                    lock (_syncRoot)
                    {
                        // Use WriteLine to ensure proper line termination and buffering
                        _output.Write(formattedMessage);
                        _output.Flush(); // Force immediate flush to underlying stream
                    }
                }
                catch (Exception ex)
                {
                    // Fallback: Write directly to Console.Error if formatting/writing fails
                    // This ensures we don't lose logs completely
                    Console.Error.WriteLine($"[AtomicConsoleSink Error] Failed to write log: {ex.Message}");
                    Console.Error.WriteLine($"[AtomicConsoleSink] Original log level: {logEvent.Level}");
                }

                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_syncRoot)
        {
            _output.Flush();
            _disposed = true;
        }
    }
}