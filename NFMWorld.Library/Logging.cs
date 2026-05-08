using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using RayTech.RayLog.MEL;

#pragma warning disable CA2254

namespace NFMWorldLibrary;

public static class Logging
{
    public const string SentryDsn = "https://baadef1bd7ebd872a30c292477d45ed6@sentry.puppykitty.racing/1";

    public static string Release { get; } = AppDomain.CurrentDomain.GetAssemblies()
        .FirstOrDefault(ass => ass.FullName?.StartsWith("NFMWorld,") == true)
        ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? "NFM-World dev";

    public static readonly ILoggerFactory LoggerFactory =
        Microsoft.Extensions.Logging.LoggerFactory.Create(builder => builder
            .AddConsole(options =>
            {
                options.FormatterName = RayLogConsoleFormatter.FormatterName;
            })
            .AddConsoleFormatter<RayLogConsoleFormatter, ConsoleFormatterOptions>()
            .AddSentry(o => o.Dsn = SentryDsn)
            .AddProvider(new NfmwLoggerProvider())
            .SetMinimumLevel(
#if DEBUG
                LogLevel.Trace
#else
                LogLevel.Debug
#endif
            )
        );

    private static readonly ILogger General = LoggerFactory.CreateLogger("general");

    public static void Info(string message) => General.LogInformation(message);
    public static void Warning(string message) => General.LogWarning(message);
    public static void Error(string message) => General.LogError(message);
    public static void Debug(string message) => General.LogDebug(message);
    public static void Info(object message)
    {
        if (General.IsEnabled(LogLevel.Information))
        {
            General.LogInformation("{Object}", message);
        }
    }
    public static void Warning(object message)
    {
        if (General.IsEnabled(LogLevel.Warning))
        {
            General.LogWarning("{Object}", message);
        }
    }
    public static void Error(object message)
    {
        if (General.IsEnabled(LogLevel.Error))
        {
            General.LogError("{Object}", message);
        }
    }
    public static void Debug(object message)
    {
        if (General.IsEnabled(LogLevel.Debug))
        {
            General.LogDebug("{Object}", message);
        }
    }

    #region InterpolatedStringHandler
    
    public static void Info([InterpolatedStringHandlerArgument] ref StructuredLoggingInformationInterpolatedStringHandler handler)
    {
        if (General.IsEnabled(LogLevel.Information))
        {
            handler.GetTemplateAndArguments(out var template, out var arguments);
            General.LogTrace(template, arguments);
        }
    }
    public static void Warning([InterpolatedStringHandlerArgument] ref StructuredLoggingWarningInterpolatedStringHandler handler)
    {
        if (General.IsEnabled(LogLevel.Warning))
        {
            handler.GetTemplateAndArguments(out var template, out var arguments);
            General.LogTrace(template, arguments);
        }
    }
    public static void Error([InterpolatedStringHandlerArgument] ref StructuredLoggingErrorInterpolatedStringHandler handler)
    {
        if (General.IsEnabled(LogLevel.Error))
        {
            handler.GetTemplateAndArguments(out var template, out var arguments);
            General.LogTrace(template, arguments);
        }
    }
    public static void Debug([InterpolatedStringHandlerArgument] ref StructuredLoggingDebugInterpolatedStringHandler handler)
    {
        if (General.IsEnabled(LogLevel.Debug))
        {
            handler.GetTemplateAndArguments(out var template, out var arguments);
            General.LogTrace(template, arguments);
        }
    }
    
    [InterpolatedStringHandler]
    public ref struct StructuredLoggingTraceInterpolatedStringHandler
    {
        private StructuredLoggingInterpolatedStringHandler _handler;

        public StructuredLoggingTraceInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new StructuredLoggingInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Trace, out isEnabled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string s) => _handler.AppendLiteral(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, name);

        // ReSharper disable once MethodOverloadWithOptionalParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, format, name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetTemplateAndArguments(out string template, out object?[] arguments) => _handler.GetTemplateAndArguments(out template, out arguments);
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingDebugInterpolatedStringHandler
    {
        private StructuredLoggingInterpolatedStringHandler _handler;

        public StructuredLoggingDebugInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new StructuredLoggingInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Debug, out isEnabled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string s) => _handler.AppendLiteral(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, name);

        // ReSharper disable once MethodOverloadWithOptionalParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, format, name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetTemplateAndArguments(out string template, out object?[] arguments) => _handler.GetTemplateAndArguments(out template, out arguments);
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingInformationInterpolatedStringHandler
    {
        private StructuredLoggingInterpolatedStringHandler _handler;

        public StructuredLoggingInformationInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new StructuredLoggingInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Information, out isEnabled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string s) => _handler.AppendLiteral(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, name);

        // ReSharper disable once MethodOverloadWithOptionalParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, format, name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetTemplateAndArguments(out string template, out object?[] arguments) => _handler.GetTemplateAndArguments(out template, out arguments);
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingWarningInterpolatedStringHandler
    {
        private StructuredLoggingInterpolatedStringHandler _handler;

        public StructuredLoggingWarningInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new StructuredLoggingInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Warning, out isEnabled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string s) => _handler.AppendLiteral(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, name);

        // ReSharper disable once MethodOverloadWithOptionalParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, format, name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetTemplateAndArguments(out string template, out object?[] arguments) => _handler.GetTemplateAndArguments(out template, out arguments);
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingErrorInterpolatedStringHandler
    {
        private StructuredLoggingInterpolatedStringHandler _handler;

        public StructuredLoggingErrorInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new StructuredLoggingInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Error, out isEnabled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string s) => _handler.AppendLiteral(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, name);

        // ReSharper disable once MethodOverloadWithOptionalParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, format, name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetTemplateAndArguments(out string template, out object?[] arguments) => _handler.GetTemplateAndArguments(out template, out arguments);
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingCriticalInterpolatedStringHandler
    {
        private StructuredLoggingInterpolatedStringHandler _handler;

        public StructuredLoggingCriticalInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new StructuredLoggingInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Critical, out isEnabled);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendLiteral(string s) => _handler.AppendLiteral(s);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, name);

        // ReSharper disable once MethodOverloadWithOptionalParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression("value")] string name = "") => _handler.AppendFormatted(value, format, name);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GetTemplateAndArguments(out string template, out object?[] arguments) => _handler.GetTemplateAndArguments(out template, out arguments);
    }


    #endregion
}

public class NfmwLoggerProvider : ILoggerProvider
{
    private static readonly ConcurrentQueue<(string message, string level)> OutputLog = new();
    
    public static IEnumerable<(string message, string level)> GetLogs()
    {
        return OutputLog;
    }

    public static void ClearMessages()
    {
        OutputLog.Clear();
    }

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new NfmwLogger(categoryName);
    }

    public class NfmwLogger(string categoryName) : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string message = formatter(state, exception);
            string level = logLevel switch
            {
                LogLevel.Trace => "debug",
                LogLevel.Debug => "debug",
                LogLevel.Information => "info",
                LogLevel.Warning => "warning",
                LogLevel.Error => "error",
                LogLevel.Critical => "error",
                _ => "default"
            };
            var formattedMessage = logLevel is LogLevel.Warning or LogLevel.Error or LogLevel.Critical
                ? $"[{categoryName.ToUpperInvariant()}] {message}"
                : message;
            OutputLog.Enqueue((formattedMessage, level));
            if (OutputLog.Count > 100) OutputLog.TryDequeue(out _); // Limit log size
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }
    }
}