using Microsoft.AspNetCore.Components;
using BlazorServerWebLogger.Data.Services;
using SampleOnlineMall.WebLogger.Models;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace BlazorServerWebLogger.Pages
{
    public partial class Index : ComponentBase
    {
        public int TotalLogsCount { get; set; } = 0;
        public ObservableCollection<LogEntryDbStorable> LogEntries { get; set; } = new();

        [Inject]
        public LogReaderService LogReaderService { get; set; }

        private Timer _timer;

        protected override async Task OnInitializedAsync()
        {
            // Инициализация начального состояния\
            var data = await LogReaderService.ReadInitialAsync(50);
            TotalLogsCount = data.Count;
            LogEntries = new ObservableCollection<LogEntryDbStorable>(data);

            StateHasChanged();
            // Запуск таймера
            _timer = new Timer(async _ => await UpdateDataAsync(), null, 0, 500);
        }

        private async Task UpdateDataAsync()
        {
            var newEntries = await LogReaderService.ReadNewEntriesAsync(LogEntries.FirstOrDefault()?.Timestamp ?? DateTime.MinValue);
            var totalCount = await LogReaderService.GetTotalLogCount();
            await InvokeAsync(() =>
            {
                if (newEntries.Any())
                {

                    foreach (var entry in newEntries)
                    {
                        LogEntries.Insert(0, entry); // Новые записи добавляются сверху
                    }

                    TotalLogsCount = totalCount;
                    StateHasChanged();

                }
            });

        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}