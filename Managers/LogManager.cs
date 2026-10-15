using System;

namespace Interstellar.Managers
{
    public enum Level
    {
        Info,
        Warning,
        Error,
        Debug
    }

    public static class LogManager
    {
        private static Action<Level, string> _sink;

        public static void SetLogger(Action<Level, string> sink) => _sink = sink;

        private static void Write(Level level, object log)
        {
            string msg = log?.ToString() ?? string.Empty;

            if (_sink is null)
            {
                UnityEngine.Debug.Log($"[{level}] {msg}");
                return;
            }

            _sink(level, msg);
        }

        public static void Log(object log) => Write(Level.Info, log);

        public static void LogError(object log) => Write(Level.Error, log);
    }
}
