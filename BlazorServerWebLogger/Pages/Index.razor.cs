using System.Collections.ObjectModel;
using BlazorServerWebLogger.Contracts;
using BlazorServerWebLogger.Data;
using BlazorServerWebLogger.Data.Services;
using Microsoft.AspNetCore.Components;
using SampleOnlineMall.WebLogger.Models;

namespace BlazorServerWebLogger.Pages
{
    public partial class Index : ComponentBase
    {
        public int TotalLogsCount { get; set; } = 0;
        public ObservableCollection<LogEntryDbStorable> LogEntries { get; set; } = new();
        public List<ServiceCodeFilter> ServiceCodes { get; set; } = new();
        public List<MessageTypeFilter> MessageTypes { get; set; } = new();
        public IEnumerable<LogEntryDbStorable> FilteredLogEntries => LogEntries
            .Where(entry =>
                ServiceCodes.Any(filter => filter.Selected && filter.ServiceCode == entry.ServiceCode) &&
                MessageTypes.Any(filter => filter.Selected && filter.MessageType == entry.MessageType));
        public LoggerMainViewSettings LoggerMainViewSettings { get; set; }

        [Inject]
        public LogReaderService LogReaderService { get; set; }
       // [Inject]
        //public IAsyncRepository<LoggerMainViewSettings> SettingsRepo { get; set; }

        private Timer _timer;

        protected override async Task OnInitializedAsync()
        {
            var data = await LogReaderService.ReadInitialAsync(n: 50);
            TotalLogsCount = data.Count;
            LogEntries = new ObservableCollection<LogEntryDbStorable>(data);
           // LoggerMainViewSettings = SettingsRepo.GetAllAsync(null, 1).Result.Items.FirstOrDefault();
            UpdateFilters();
            StateHasChanged();
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
                        LogEntries.Insert(0, entry);
                    }
                    TotalLogsCount = totalCount;
                    UpdateFilters();
                }
                StateHasChanged();
            });
        }

        private void UpdateFilters()
        {
            // Обновление ServiceCodes
            var existingServiceCodes = ServiceCodes;
            ServiceCodes = LogEntries
                .Select(entry => entry.ServiceCode)
                .Distinct()
                .Select(code => new ServiceCodeFilter { ServiceCode = code, Selected = true })
                .OrderBy(filter => filter.ServiceCode)
                .ToList();

            foreach (var filterItem in ServiceCodes)
            {
                var existingFilter = existingServiceCodes.FirstOrDefault(f => f.ServiceCode == filterItem.ServiceCode);
                if (existingFilter != null)
                {
                    filterItem.Selected = existingFilter.Selected;
                    //теперь если оно выключено в сеттингах, оно не должно включаться


                }
            }
            //пропускаем через сеттинги




            // Обновление MessageTypes
            var existingMessageTypes = MessageTypes;
            MessageTypes = LogEntries
                .Select(entry => entry.MessageType)
                .Distinct()
                .Select(type => new MessageTypeFilter { MessageType = type, Selected = true })
                .OrderBy(filter => filter.MessageType)
                .ToList();

            foreach (var filter in MessageTypes)
            {
                var existingFilter = existingMessageTypes.FirstOrDefault(f => f.MessageType == filter.MessageType);
                if (existingFilter != null)
                {
                    filter.Selected = existingFilter.Selected;
                }
            }


            
        }

        private async Task DeleteAllRecords()
        {
            await LogReaderService.DeleteAllLogsAsync();
            LogEntries.Clear();
            TotalLogsCount = 0;
            UpdateFilters();
            StateHasChanged();
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }

        public class ServiceCodeFilter
        {
            public string ServiceCode { get; set; }
            public bool Selected { get; set; }
        }

        public class MessageTypeFilter
        {
            public string MessageType { get; set; }
            public bool Selected { get; set; }
        }
    }
}
