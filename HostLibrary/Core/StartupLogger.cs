using HostLibrary;
using Microsoft.Extensions.Logging;
using NLog.Web;
using System;

namespace HostLibrary.Core
{
    public static class StartupLogger
    {
        private static ILogger _logger;

        static StartupLogger()
        {
            _logger = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
            }).CreateLogger(typeof(Initial).Assembly.GetName().Name);
        }

        public static void Disable() => _logger = null;
        public static void LogInformation(string message, params object[] args) => _logger?.LogInformation(message, args);
        public static void LogWarning(string message, params object[] args) => _logger?.LogWarning(message, args);
        public static void LogError(string message, params object[] args) => _logger?.LogError(message, args);
        public static T LogError<T>(T exception, string message, params object[] args) where T : Exception
        {
            _logger?.LogError(exception, message, args);
            return exception;
        }
    }
}