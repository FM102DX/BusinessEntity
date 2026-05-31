using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BusinessEntity.MiniApps.SampleDataMiniApp.Contracts;
using BusinessEntity.WebLogger.Services;
using Microsoft.Extensions.Hosting;

namespace BusinessEntity.MiniApps.SampleDataMiniApp.Internal
{
    // Читает строки из файла наполнения и отдаёт их по одной для тестовых документов.
    public class DataFillLineProvider : IDataFillLineProvider
    {
        private readonly IHostEnvironment _env;
        private readonly IWebLoggerService? _logger;
        private readonly object _lock = new();
        private List<string>? _lines;
        private int _index = 0;

        // Получает окружение приложения и логгер для загрузки файла наполнения.
        public DataFillLineProvider(IHostEnvironment env, IWebLoggerService? logger)
        {
            _env = env ?? throw new ArgumentNullException(nameof(env));
            _logger = logger;
        }

        // Возвращает следующую строку из источника наполнения.
        public Task<string> GetNextLineAsync(CancellationToken ct = default)
        {
            EnsureLoaded();
            lock (_lock)
            {
                if (_lines == null || _lines.Count == 0)
                {
                    return Task.FromResult(string.Empty);
                }

                var line = _lines[_index];
                _index = (_index + 1) % _lines.Count;
                return Task.FromResult(line);
            }
        }

        // Загружает файл наполнения один раз и подготавливает его к циклической выдаче строк.
        private void EnsureLoaded()
        {
            if (_lines != null)
            {
                return;
            }

            lock (_lock)
            {
                if (_lines != null)
                {
                    return;
                }

                var candidates = new[]
                {
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "Resources", "DataFill_01.txt")),
                    Path.GetFullPath(Path.Combine(_env.ContentRootPath, "..", "BusinessEntityData.Resources", "DataFill_01.txt")),
                    Path.GetFullPath(Path.Combine(_env.ContentRootPath, "BusinessEntityData.Resources", "DataFill_01.txt")),
                    Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "BusinessEntityData.Resources", "DataFill_01.txt"))
                };

                foreach (var candidate in candidates)
                {
                    _logger?.Debug($"[DataFill] Candidate: {candidate} exists={File.Exists(candidate)}");
                }

                string? path = candidates.FirstOrDefault(File.Exists);
                if (path == null)
                {
                    _logger?.Warning("Data fill source file not found. Falling back to built-in sample lines.");
                    _lines = new List<string>
                    {
                        "Добро пожаловать в демонстрационное пространство документов.",
                        "Этот документ был создан автоматическим сидером приложения.",
                        "Здесь можно хранить заметки, описания процессов и рабочие материалы.",
                        "Папки и документы созданы для демонстрации дерева пространства.",
                        "Если нужно, наполнение можно заменить данными из внешнего файла."
                    };
                    return;
                }

                var lines = File.ReadAllLines(path)
                    .Select(l => l.TrimEnd('\r', '\n'))
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                _lines = lines.Count > 0 ? lines : new List<string> { string.Empty };
                _logger?.Information($"DataFillLineProvider loaded {_lines.Count} lines from {path}");
            }
        }
    }
}
