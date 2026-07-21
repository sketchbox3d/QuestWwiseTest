using System;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sketchbox.Logging
{
    /// <summary>
    /// Severity levels, ordered. Messages below <see cref="Log.MinimumLevel"/> are discarded
    /// before any string formatting occurs.
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Off = 4
    }

    /// <summary>
    /// Receives log records. Implement to route output somewhere other than the Unity console.
    /// </summary>
    public interface ILogSink
    {
        void Write(LogLevel level, string channel, string message, Object context);
        void WriteException(Exception exception, Object context);
    }

    /// <summary>
    /// Default sink. Forwards to the Unity console, preserving the click-to-select context object.
    /// </summary>
    public sealed class UnityConsoleSink : ILogSink
    {
        public void Write(LogLevel level, string channel, string message, Object context)
        {
            string line = string.IsNullOrEmpty(channel) ? message : "[" + channel + "] " + message;

            switch (level)
            {
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(line, context);
                    break;
                case LogLevel.Warn:
                    UnityEngine.Debug.LogWarning(line, context);
                    break;
                default:
                    UnityEngine.Debug.Log(line, context);
                    break;
            }
        }

        public void WriteException(Exception exception, Object context)
        {
            UnityEngine.Debug.LogException(exception, context);
        }
    }

    /// <summary>
    /// Project-wide logging facade.
    ///
    /// Call sites use this instead of UnityEngine.Debug so that severity filtering, channel
    /// tagging and output routing are decided in one place rather than at ~12,000 individual
    /// call sites. The default configuration forwards to the Unity console, so behaviour matches
    /// UnityEngine.Debug until a different sink is installed.
    ///
    /// Verbose levels are compiled out of release player builds: Debug and Info are marked
    /// [Conditional], so in a non-development build the calls and their argument expressions are
    /// removed by the compiler.
    /// </summary>
    public static class Log
    {
        private static ILogSink _sink = new UnityConsoleSink();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static LogLevel _minimumLevel = LogLevel.Debug;
#else
        private static LogLevel _minimumLevel = LogLevel.Info;
#endif

        /// <summary>Messages below this level are discarded. Defaults to Debug in the editor and
        /// development builds, Info otherwise.</summary>
        public static LogLevel MinimumLevel
        {
            get { return _minimumLevel; }
            set { _minimumLevel = value; }
        }

        /// <summary>Replaces the output sink. Passing null restores the Unity console sink.</summary>
        public static void SetSink(ILogSink sink)
        {
            _sink = sink ?? new UnityConsoleSink();
        }

        public static bool IsEnabled(LogLevel level)
        {
            return level >= MinimumLevel && MinimumLevel != LogLevel.Off;
        }

        // Debug and Info are stripped from release player builds.
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Trace(object message, Object context = null)
        {
            Write(LogLevel.Debug, null, message, context);
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void TraceFormat(string format, params object[] args)
        {
            WriteFormat(LogLevel.Debug, null, null, format, args);
        }

        public static void Info(object message)
        {
            Write(LogLevel.Info, null, message, null);
        }

        public static void Info(object message, Object context)
        {
            Write(LogLevel.Info, null, message, context);
        }

        public static void InfoFormat(string format, params object[] args)
        {
            WriteFormat(LogLevel.Info, null, null, format, args);
        }

        public static void InfoFormat(Object context, string format, params object[] args)
        {
            WriteFormat(LogLevel.Info, null, context, format, args);
        }

        public static void Warn(object message)
        {
            Write(LogLevel.Warn, null, message, null);
        }

        public static void Warn(object message, Object context)
        {
            Write(LogLevel.Warn, null, message, context);
        }

        public static void WarnFormat(string format, params object[] args)
        {
            WriteFormat(LogLevel.Warn, null, null, format, args);
        }

        public static void WarnFormat(Object context, string format, params object[] args)
        {
            WriteFormat(LogLevel.Warn, null, context, format, args);
        }

        public static void Error(object message)
        {
            Write(LogLevel.Error, null, message, null);
        }

        public static void Error(object message, Object context)
        {
            Write(LogLevel.Error, null, message, context);
        }

        public static void ErrorFormat(string format, params object[] args)
        {
            WriteFormat(LogLevel.Error, null, null, format, args);
        }

        public static void ErrorFormat(Object context, string format, params object[] args)
        {
            WriteFormat(LogLevel.Error, null, context, format, args);
        }

        /// <summary>Assertion failures are reported at Error level.</summary>
        public static void Assertion(object message)
        {
            Write(LogLevel.Error, "assert", message, null);
        }

        public static void Assertion(object message, Object context)
        {
            Write(LogLevel.Error, "assert", message, context);
        }

        public static void AssertionFormat(string format, params object[] args)
        {
            WriteFormat(LogLevel.Error, "assert", null, format, args);
        }

        public static void AssertionFormat(Object context, string format, params object[] args)
        {
            WriteFormat(LogLevel.Error, "assert", context, format, args);
        }

        public static void Exception(Exception exception)
        {
            Exception(exception, null);
        }

        public static void Exception(Exception exception, Object context)
        {
            if (MinimumLevel == LogLevel.Off)
            {
                return;
            }

            if (exception == null)
            {
                Write(LogLevel.Error, null, "Log.Exception called with a null exception.", context);
                return;
            }

            _sink.WriteException(exception, context);
        }

        /// <summary>Returns a logger that tags every message with <paramref name="channel"/>.</summary>
        public static ChannelLog Channel(string channel)
        {
            return new ChannelLog(channel);
        }

        internal static void Write(LogLevel level, string channel, object message, Object context)
        {
            if (!IsEnabled(level))
            {
                return;
            }

            _sink.Write(level, channel, message == null ? "null" : message.ToString(), context);
        }

        internal static void WriteFormat(LogLevel level, string channel, Object context, string format, object[] args)
        {
            if (!IsEnabled(level))
            {
                return;
            }

            string message;
            try
            {
                message = args == null || args.Length == 0 ? format : string.Format(format, args);
            }
            catch (FormatException)
            {
                // A malformed format string must not take down the caller.
                message = format;
            }

            _sink.Write(level, channel, message, context);
        }
    }

    /// <summary>Logger bound to a fixed channel name. Obtain via <see cref="Log.Channel"/>.</summary>
    public struct ChannelLog
    {
        private readonly string _channel;

        public ChannelLog(string channel)
        {
            _channel = channel;
        }

        public void Info(object message, Object context = null)
        {
            Log.Write(LogLevel.Info, _channel, message, context);
        }

        public void Warn(object message, Object context = null)
        {
            Log.Write(LogLevel.Warn, _channel, message, context);
        }

        public void Error(object message, Object context = null)
        {
            Log.Write(LogLevel.Error, _channel, message, context);
        }
    }
}
