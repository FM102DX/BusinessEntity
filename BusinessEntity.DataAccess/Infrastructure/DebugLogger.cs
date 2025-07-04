namespace BusinessEntity.DataAccess.Infrastructure;

using System;
using System.Collections.Concurrent;
using System.IO;

public class DebugLogger
{
    private readonly string _prefix;
    private readonly bool _isActive;
    private readonly object _lock = new();
    private readonly string _workingDir;
    private string _currentFilePath;
    private readonly bool _logToConsole;
    private static readonly ConcurrentDictionary<string, DebugLogger> LoggerInstances = new();

    public DebugLogger(string prefix = "def", bool isActive = true, string workingDir = "", bool logToConsole = false)
    {
        _prefix = prefix;
        _isActive = isActive;
        _workingDir = workingDir;
        _logToConsole = logToConsole;
        if (!logToConsole)
        {
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
    }

    public void Write(string text)
    {
        if (!_isActive)
            return;

        if (!_logToConsole)
        {
            lock (_lock)
            {
                File.AppendAllText(_currentFilePath, text + Environment.NewLine);
            }
        }
        else
        {
            lock (_lock)
            {
                Console.WriteLine(text);
            }
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