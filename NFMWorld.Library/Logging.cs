using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NFMWorld.Sentry;
using RayTech.RayLog.MEL;
using ZLogger;

#pragma warning disable CA2254

namespace NFMWorldLibrary;

public static class Logging
{
    public const string SentryDsn = "https://576c8b7adc0c43208ec65b059d436085@glitchtip.puppykitty.racing/2";

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
            .AddZLoggerRollingFile((dt, index) => $"{dt:yyyy-MM-dd}_{index}.log", 1024 * 1024)
            .AddProvider(new NfmwLoggerProvider())
            .AddProvider(new NfmwSentryBreadcrumbLogger())
            .SetMinimumLevel(
#if DEBUG
                LogLevel.Trace
#else
                LogLevel.Debug
#endif
            )
        );

    private static readonly ILogger General = LoggerFactory.CreateLogger("general");

    public static void Info(string message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Info($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    public static void Warning(string message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Warning($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    public static void Error(string message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Error($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    public static void Debug(string message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Debug($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    public static void Info(object message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Info($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    public static void Warning(object message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Warning($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    public static void Error(object message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Error($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);
    public static void Debug(object message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
        => Debug($"{message}", memberName: memberName, filePath: filePath, lineNumber: lineNumber);

    #region InterpolatedStringHandler
    
    public static void Info([InterpolatedStringHandlerArgument] ref StructuredLoggingInformationInterpolatedStringHandler handler, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Information))
        {
            General.ZLog(LogLevel.Information, default, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Warning([InterpolatedStringHandlerArgument] ref StructuredLoggingWarningInterpolatedStringHandler handler, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Warning))
        {
            General.ZLog(LogLevel.Warning, default, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Error([InterpolatedStringHandlerArgument] ref StructuredLoggingErrorInterpolatedStringHandler handler, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Error))
        {
            General.ZLog(LogLevel.Error, default, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Debug([InterpolatedStringHandlerArgument] ref StructuredLoggingDebugInterpolatedStringHandler handler, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Debug))
        {
            General.ZLog(LogLevel.Debug, default, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }

    public static void Info([InterpolatedStringHandlerArgument] ref StructuredLoggingInformationInterpolatedStringHandler handler, EventId eventId, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Information))
        {
            General.ZLog(LogLevel.Information, eventId, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Warning([InterpolatedStringHandlerArgument] ref StructuredLoggingWarningInterpolatedStringHandler handler, EventId eventId, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Warning))
        {
            General.ZLog(LogLevel.Warning, eventId, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Error([InterpolatedStringHandlerArgument] ref StructuredLoggingErrorInterpolatedStringHandler handler, EventId eventId, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Error))
        {
            General.ZLog(LogLevel.Error, eventId, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Debug([InterpolatedStringHandlerArgument] ref StructuredLoggingDebugInterpolatedStringHandler handler, EventId eventId, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Debug))
        {
            General.ZLog(LogLevel.Debug, eventId, null, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }

    public static void Info([InterpolatedStringHandlerArgument] ref StructuredLoggingInformationInterpolatedStringHandler handler, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Information))
        {
            General.ZLog(LogLevel.Information, default, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Warning([InterpolatedStringHandlerArgument] ref StructuredLoggingWarningInterpolatedStringHandler handler, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Warning))
        {
            General.ZLog(LogLevel.Warning, default, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Error([InterpolatedStringHandlerArgument] ref StructuredLoggingErrorInterpolatedStringHandler handler, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Error))
        {
            General.ZLog(LogLevel.Error, default, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Debug([InterpolatedStringHandlerArgument] ref StructuredLoggingDebugInterpolatedStringHandler handler, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Debug))
        {
            General.ZLog(LogLevel.Debug, default, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }

    public static void Info([InterpolatedStringHandlerArgument] ref StructuredLoggingInformationInterpolatedStringHandler handler, EventId eventId, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Information))
        {
            General.ZLog(LogLevel.Information, eventId, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Warning([InterpolatedStringHandlerArgument] ref StructuredLoggingWarningInterpolatedStringHandler handler, EventId eventId, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Warning))
        {
            General.ZLog(LogLevel.Warning, eventId, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Error([InterpolatedStringHandlerArgument] ref StructuredLoggingErrorInterpolatedStringHandler handler, EventId eventId, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Error))
        {
            General.ZLog(LogLevel.Error, eventId, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }
    public static void Debug([InterpolatedStringHandlerArgument] ref StructuredLoggingDebugInterpolatedStringHandler handler, EventId eventId, Exception? exception, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        if (General.IsEnabled(LogLevel.Debug))
        {
            General.ZLog(LogLevel.Debug, eventId, exception, ref handler._handler, context, memberName, filePath, lineNumber);
        }
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingTraceInterpolatedStringHandler
    {
        internal ZLoggerInterpolatedStringHandler _handler;

        public StructuredLoggingTraceInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new ZLoggerInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Trace, out isEnabled);
        }

        public void AppendLiteral([ConstantExpected] string s)
            => _handler.AppendLiteral(s);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T? value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            where T : struct
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>((string, T) namedValue, int alignment = 0, string? format = null, string? _ = null)
            => _handler.AppendFormatted(namedValue, alignment, format);
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingDebugInterpolatedStringHandler
    {
        internal ZLoggerInterpolatedStringHandler _handler;

        public StructuredLoggingDebugInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new ZLoggerInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Debug, out isEnabled);
        }


        public void AppendLiteral([ConstantExpected] string s)
            => _handler.AppendLiteral(s);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T? value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            where T : struct
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>((string, T) namedValue, int alignment = 0, string? format = null, string? _ = null)
            => _handler.AppendFormatted(namedValue, alignment, format);
    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingInformationInterpolatedStringHandler
    {
        internal ZLoggerInterpolatedStringHandler _handler;

        public StructuredLoggingInformationInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new ZLoggerInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Information, out isEnabled);
        }

        public void AppendLiteral([ConstantExpected] string s)
            => _handler.AppendLiteral(s);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T? value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            where T : struct
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>((string, T) namedValue, int alignment = 0, string? format = null, string? _ = null)
            => _handler.AppendFormatted(namedValue, alignment, format);    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingWarningInterpolatedStringHandler
    {
        internal ZLoggerInterpolatedStringHandler _handler;

        public StructuredLoggingWarningInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new ZLoggerInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Warning, out isEnabled);
        }

        public void AppendLiteral([ConstantExpected] string s)
            => _handler.AppendLiteral(s);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T? value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            where T : struct
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>((string, T) namedValue, int alignment = 0, string? format = null, string? _ = null)
            => _handler.AppendFormatted(namedValue, alignment, format);    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingErrorInterpolatedStringHandler
    {
        internal ZLoggerInterpolatedStringHandler _handler;

        public StructuredLoggingErrorInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new ZLoggerInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Error, out isEnabled);
        }

        public void AppendLiteral([ConstantExpected] string s)
            => _handler.AppendLiteral(s);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T? value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            where T : struct
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>((string, T) namedValue, int alignment = 0, string? format = null, string? _ = null)
            => _handler.AppendFormatted(namedValue, alignment, format);    }

    [InterpolatedStringHandler]
    public ref struct StructuredLoggingCriticalInterpolatedStringHandler
    {
        internal ZLoggerInterpolatedStringHandler _handler;

        public StructuredLoggingCriticalInterpolatedStringHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            _handler = new ZLoggerInterpolatedStringHandler(literalLength, formattedCount, General, LogLevel.Critical, out isEnabled);
        }

        public void AppendLiteral([ConstantExpected] string s)
            => _handler.AppendLiteral(s);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(T? value, int alignment = 0, string? format = null, [CallerArgumentExpression("value")] string? argumentName = null)
            where T : struct
            => _handler.AppendFormatted(value, alignment, format, argumentName);

        public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>((string, T) namedValue, int alignment = 0, string? format = null, string? _ = null)
            => _handler.AppendFormatted(namedValue, alignment, format);    }


    #endregion
}

public class NfmwSentryBreadcrumbLogger : ILoggerProvider
{
    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new NfmwSentryBreadcrumbLoggerImpl(categoryName);
    }

    public class NfmwSentryBreadcrumbLoggerImpl(string categoryName) : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            SentrySdk.AddBreadcrumb(new Breadcrumb
            {
                Timestamp = DateTimeOffset.UtcNow,
                Message = message,
                Category = categoryName,
                Type = "default",
                Level = logLevel switch
                {
                    LogLevel.Trace or LogLevel.Debug => BreadcrumbLevel.Debug,
                    LogLevel.Information => BreadcrumbLevel.Info,
                    LogLevel.Warning => BreadcrumbLevel.Warning,
                    LogLevel.Error => BreadcrumbLevel.Error,
                    LogLevel.Critical => BreadcrumbLevel.Critical,
                    _ => BreadcrumbLevel.Debug
                }
            });
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            throw new NotImplementedException();
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            throw new NotImplementedException();
        }
    }
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