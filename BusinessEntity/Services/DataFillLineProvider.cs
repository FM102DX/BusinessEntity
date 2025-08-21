using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusinessEntity.Core.Contracts;
using Microsoft.Extensions.Hosting;
using SampleOnlineMall.WebLogger.Services;

namespace BusinessEntity.Services
{
    /// <summary>
    /// Reads lines from a relative text file and returns one line per call.
    /// Wraps around when reaching the end.
    /// </summary>
    public class DataFillLineProvider : IDataFillLineProvider
    {
        private readonly IHostEnvironment _env;
        private readonly IWebLoggerService? _logger;
        private readonly object _lock = new();
        private List<string>? _lines;
        private int _index = 0;

        public DataFillLineProvider(IHostEnvironment env, IWebLoggerService? logger)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger; // optional
        }

        public Task<string> GetNextLineAsync(CancellationToken ct = default)
        {
            EnsureLoaded();
            lock (_lock)
            {
                if (_lines == null || _lines.Count == 0)
                    return Task.FromResult(string.Empty);

                var line = _lines[_index];
                _index = (_index + 1) % _lines.Count;
                return Task.FromResult(line);
            }
        }

        private void EnsureLoaded()
        {
            if (_lines != null) return;

            lock (_lock)
            {
                if (_lines != null) return;

                // Try to resolve the file with relative paths from the content root
                var candidates = new[]
                {
                    Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "BusinessEntity.Resources", "DataFill_01.txt")),
                    Path.GetFullPath(Path.Combine(_env.ContentRootPath, "BusinessEntity.Resources", "DataFill_01.txt")),
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "BusinessEntity.Resources", "DataFill_01.txt"))
                };

                string? path = candidates.FirstOrDefault(File.Exists);
                if (path == null)
                {
                    throw new FileNotFoundException("Data fill source file not found via relative paths.", candidates.First());
                }

                var lines = File.ReadAllLines(path)
                                .Select(l => l.TrimEnd('\r', '\n'))
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .ToList();

                _lines = lines.Count > 0 ? lines : new List<string> { "" };
                _logger?.Information($"DataFillLineProvider loaded {_lines.Count} lines from {path}");
            }
        }
    }
}
