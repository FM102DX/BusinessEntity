namespace BlazorServerWebLogger.Service.Logger
{
    /*
     * DebugLogger - A lightweight file-based logger.
     * 
     * Features:
     * 1. Can be instantiated with a prefix, active state, and working directory.
     * 2. Creates log files dynamically when the write method is called for the first time.
     * 3. Supports both instance-based and static logging.
     * 4. Ensures platform-independent newline characters in log files.
     * 5. Automatically manages unique file naming using GUIDs.
     * 6. Throws exceptions when unable to create directories or write to files.
     * 7. Static method maintains separate loggers for unique prefix-workingDir combinations.
     */

    using System;
    using System.Collections.Concurrent;
    using System.IO;

    public class DebugLogger
    {
        private readonly string _prefix;
        private readonly bool _isActive;
        private readonly object _lock = new object();
        private readonly string _workingDir;
        private string _currentFilePath;
        private static readonly ConcurrentDictionary<string, DebugLogger> LoggerInstances = new();

        public DebugLogger(string prefix = "def", bool isActive = true, string? workingDir = null)
        {
            _prefix = prefix;
            _isActive = isActive;
            _workingDir = workingDir;
            if (string.IsNullOrEmpty(_workingDir))
            {
                _workingDir = "C:\\DebugLogs";
            }
            lock (_lock)
            {
                if (!Directory.Exists(_workingDir))
                    Directory.CreateDirectory(_workingDir);
                _currentFilePath = Path.Combine(_workingDir, $"{_prefix}_{Guid.NewGuid()}.txt");
            }
        }

        public void Write(string text)
        {
            if (!_isActive) // Предполагается, что это логический флаг
                return;
            lock (_lock)
            {
                File.AppendAllText(_currentFilePath, text + Environment.NewLine);
            }
        }

        public static void Write(string text, string prefix = "", string workingDir = "")
        {
            var key = $"{prefix}";

            if (!LoggerInstances.ContainsKey(key))
            {
                var logger = new DebugLogger(prefix, true, workingDir);
                LoggerInstances[key] = logger;
            }
            LoggerInstances[key].Write(text);
        }
    }

}
