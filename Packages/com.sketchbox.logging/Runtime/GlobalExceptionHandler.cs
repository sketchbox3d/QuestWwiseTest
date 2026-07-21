using System;
using UnityEngine;
#if NET_4_6 || NET_STANDARD_2_0
using System.Threading.Tasks;
#endif

namespace Sketchbox.Logging
{
    /// <summary>
    /// Installs process-wide handlers for failures that would otherwise be reported
    /// inconsistently or, in the case of faulted Tasks, swallowed entirely.
    ///
    /// Covers three sources:
    ///   - uncaught exceptions on the Unity player loop (Application.logMessageReceived)
    ///   - uncaught exceptions on non-Unity threads (AppDomain.UnhandledException)
    ///   - faulted Tasks whose exception is never observed (TaskScheduler.UnobservedTaskException),
    ///     where the scripting runtime provides System.Threading.Tasks
    ///
    /// Installation is automatic via RuntimeInitializeOnLoadMethod; no scene object or manual
    /// bootstrap call is required. Handlers only report. They deliberately do not suppress or
    /// swallow, so existing crash behaviour is unchanged.
    /// </summary>
    public static class GlobalExceptionHandler
    {
        private static bool _installed;
        private static readonly object InstallLock = new object();

        /// <summary>Raised after an unhandled failure is reported. Use to forward to crash
        /// reporting or telemetry. Subscriber exceptions are caught and ignored.</summary>
        public static event Action<Exception> UnhandledFailure;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install()
        {
            lock (InstallLock)
            {
                if (_installed)
                {
                    return;
                }

                _installed = true;
            }

            Application.logMessageReceived += OnUnityLogMessage;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainException;
#if NET_4_6 || NET_STANDARD_2_0
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
#endif

            Log.Info("Global exception handler installed.");
        }

        /// <summary>Removes the handlers. Provided for tests and for hosts that manage their
        /// own lifetime; normal play mode does not need to call this.</summary>
        public static void Uninstall()
        {
            lock (InstallLock)
            {
                if (!_installed)
                {
                    return;
                }

                _installed = false;
            }

            Application.logMessageReceived -= OnUnityLogMessage;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainException;
#if NET_4_6 || NET_STANDARD_2_0
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
#endif
        }

        private static void OnUnityLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            // Report only. The exception has already reached the Unity console by this point,
            // so re-logging it through Log would duplicate the entry.
            Notify(new UnhandledPlayerLoopException(condition, stackTrace));
        }

        private static void OnAppDomainException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception
                                  ?? new Exception("Non-Exception thrown: " + args.ExceptionObject);

            Log.Error("Unhandled exception on a background thread. Terminating: " + args.IsTerminating);
            Log.Exception(exception);
            Notify(exception);
        }

#if NET_4_6 || NET_STANDARD_2_0
        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            Log.Error("Faulted Task was never observed.");
            Log.Exception(args.Exception);
            Notify(args.Exception);

            // Mark observed so the default policy does not escalate on finalization.
            args.SetObserved();
        }
#endif

        private static void Notify(Exception exception)
        {
            Action<Exception> handler = UnhandledFailure;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(exception);
            }
            catch (Exception subscriberFailure)
            {
                // A failing crash reporter must not itself crash the failure path.
                Log.Error("UnhandledFailure subscriber threw: " + subscriberFailure.Message);
            }
        }
    }

    /// <summary>
    /// Wraps an exception reported through Application.logMessageReceived, where only the
    /// formatted message and stack trace survive.
    /// </summary>
    public sealed class UnhandledPlayerLoopException : Exception
    {
        public UnhandledPlayerLoopException(string message, string stackTrace)
            : base(message)
        {
            PlayerLoopStackTrace = stackTrace;
        }

        public string PlayerLoopStackTrace { get; private set; }

        public override string StackTrace
        {
            get { return PlayerLoopStackTrace ?? base.StackTrace; }
        }
    }
}
