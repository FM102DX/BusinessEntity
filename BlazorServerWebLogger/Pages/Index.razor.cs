using System.Collections.ObjectModel;
using BlazorServerWebLogger.Contracts;
using BlazorServerWebLogger.Data;
using BlazorServerWebLogger.Data.Messages;
using BlazorServerWebLogger.Data.Services;
using DynamicData;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ReactiveUI;
using SampleOnlineMall.WebLogger.Models;

namespace BlazorServerWebLogger.Pages
{
    public partial class Index : ComponentBase, IDisposable
    {
        public int TotalLogsCount { get; set; } = 0;
        public ObservableCollection<LogEntryDbStorable> LogEntries { get; set; } = new();
        public List<FilterItem> ServiceCodeFilter { get; set; } = new();
        public List<FilterItem> MessageTypeFilter { get; set; } = new();
        public IEnumerable<LogEntryDbStorable> FilteredLogEntries => LogEntries
            .Where(entry =>
                ServiceCodeFilter.Any(filter => filter.Selected && filter.Code == entry.ServiceCode) &&
                MessageTypeFilter.Any(filter => filter.Selected && filter.Code == entry.MessageType));
        public LoggerMainViewSettings LoggerMainViewSettings { get; set; }

        [Inject]
        public LogReaderService LogReaderService { get; set; }
        [Inject]
        public AppSettingsManager SettingsManager { get; set; }

        public string SavedCatsDisp => LoggerMainViewSettings?.DisplayedCats;
        public string SavedCatsNonDisp => LoggerMainViewSettings?.NonDisplayedCats;
        public string SavedMsgTypesDisp => LoggerMainViewSettings?.DisplayedMessageTypes;
        public string SavedMsgTypesNonDisp => LoggerMainViewSettings?.NonDisplayedMessageTypes;

        public LogEntryDbStorable SelectedLogEntry { get; set; }

        private Timer _timer;

        protected override async Task OnInitializedAsync()
        {
            // Загружаем начальные данные логов
            var data = await LogReaderService.ReadInitialAsync(n: 50);
            TotalLogsCount = data.Count;
            LogEntries = new ObservableCollection<LogEntryDbStorable>(data);

            // Загружаем настройки пользователя
            LoggerMainViewSettings = await SettingsManager.LoadSettings();
            UpdateFilters();

            // Обновляем состояние интерфейса
            StateHasChanged();

            // Запускаем таймер для обновления данных
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

        private async Task UpdateFilters()
        {

            // Обновление ServiceCodes
            ServiceCodeFilter = UpdateFilter(ServiceCodeFilter,
                s => s.ServiceCode,
                s => new FilterItem() { Code = s, Selected = GetIsServiceCodeSelected(ServiceCodeFilter, s, LoggerMainViewSettings.NonDisplayedCats) });

            // Обновление MessageTypes
            MessageTypeFilter = UpdateFilter(MessageTypeFilter,
                s => s.MessageType,
                s => new FilterItem() { Code = s, Selected = GetIsServiceCodeSelected(MessageTypeFilter, s, LoggerMainViewSettings.NonDisplayedMessageTypes) });

            // Обновляем настройки на основе выбранных фильтров
            StateHasChanged();
        }

        private List<FilterItem> UpdateFilter(List<FilterItem>? currentFilter, Func<LogEntryDbStorable, string> selector1, Func<string, FilterItem> selector2)
        {
            //эта функция апдейтит состояние выбранного фильтра
            //Теперь надо этот фильтра перебрать, и для каждой позиции понять, чекнута или нет
            return LogEntries
            .Select(selector1)
            .Distinct()
            .Select(selector2)
            .OrderBy(filter => filter.Code)
            .ToList();
        }
        private bool GetIsServiceCodeSelected(List<FilterItem>? currentFilter, string code, string nonDisplayedStr)
        {
            //эта функция возвращает, выделен конктетный пункт меню или нет
            var filterTmp = currentFilter;
            var currentFilterItem = filterTmp.FirstOrDefault(f => f.Code == code);
            var currentFilterItemExists = currentFilterItem != null;
            var isNonDisplayed = !string.IsNullOrEmpty(nonDisplayedStr) &&
                                 nonDisplayedStr.Split(";").ToList().Contains(code);

            if (isNonDisplayed)
            {
                //если он в списке неотоборажаемых, то в любом случае false
                return false;
            }
            else if (!isNonDisplayed && currentFilterItemExists)
            {
                //если его нет в списке неоторражаемых, то настройки текущего элемента, которые были перед обновлением
                return currentFilterItem.Selected;
            }
            else
            {
                //для совсем новых элементов true, т.к. их никто не выключал
                return true;
            }
        }

        private async Task OnLogGenerationToggle(bool newState)
        {
            // Обновляем состояние и сохраняем настройки
            LoggerMainViewSettings.LogGenerationIsOn = newState;

            // Отправляем сообщение через Messenger
            MessageBus.Current.SendMessage(new LogsGenOnOffMessage { NewState = newState });

            // Сохраняем изменения в базе данных через AppSettingsManager
            await SettingsManager.SaveSettings(LoggerMainViewSettings);
        }

        private async Task DeleteAllRecords()
        {
            await LogReaderService.DeleteAllLogsAsync();
            LogEntries.Clear();
            TotalLogsCount = 0;
            UpdateFilters();
            StateHasChanged();
        }


        // Метод для сохранения ServiceCode фильтров
        private async Task SaveSrvCodesFilterAsync(FilterItem serviceFilterItem, bool newValue)
        {
            await SaveFilterAsync(
                serviceFilterItem,
                newValue,
                () => LoggerMainViewSettings.DisplayedCats,
                value => LoggerMainViewSettings.DisplayedCats = value,
                () => LoggerMainViewSettings.NonDisplayedCats,
                value => LoggerMainViewSettings.NonDisplayedCats = value);
        }

        // Метод для сохранения MessageType фильтров
        private async Task SaveMsgTypeFilterAsync(FilterItem serviceFilterItem, bool newValue)
        {
            await SaveFilterAsync(
                serviceFilterItem,
                newValue,
                () => LoggerMainViewSettings.DisplayedMessageTypes,
                value => LoggerMainViewSettings.DisplayedMessageTypes = value,
                () => LoggerMainViewSettings.NonDisplayedMessageTypes,
                value => LoggerMainViewSettings.NonDisplayedMessageTypes = value);
        }

        // Универсальный метод для сохранения фильтров
        private async Task SaveFilterAsync(
            FilterItem serviceFilterItem,
            bool newValue,
            Func<string> getDisplayedItems,
            Action<string> setDisplayedItems,
            Func<string> getNonDisplayedItems,
            Action<string> setNonDisplayedItems)
        {
            // Обновляем состояние элемента фильтра
            serviceFilterItem.Selected = newValue;

            if (newValue)
            {
                // Если состояние изменено с false на true
                setDisplayedItems(getDisplayedItems() + serviceFilterItem.Code + ";");
                setNonDisplayedItems(RemoveAllOccurrences(getNonDisplayedItems(), serviceFilterItem.Code + ";"));
            }
            else
            {
                // Если состояние изменено с true на false
                setNonDisplayedItems(getNonDisplayedItems() + serviceFilterItem.Code + ";");
                setDisplayedItems(RemoveAllOccurrences(getDisplayedItems(), serviceFilterItem.Code + ";"));
            }

            // Сохраняем изменения в базе данных через AppSettingsManager
            await SettingsManager.SaveSettings(LoggerMainViewSettings);
            StateHasChanged();

            // Логируем сохранение (опционально)
            Console.WriteLine("Settings saved.");
        }

        private void OnRowSelect(LogEntryDbStorable logEntry)
        {
            SelectedLogEntry = logEntry;
            //Console.WriteLine($"Выбранная запись: {SelectedLogEntry.ServiceCode} - {SelectedLogEntry.MessageType}");
        }

        public void Dispose()
        {
            // Очищаем таймер при завершении работы компонента
            _timer?.Dispose();
        }

        // Вспомогательные классы для фильтров
        public class FilterItem
        {
            public string Code { get; set; }
            public bool Selected { get; set; }
        }


        public static string RemoveAllOccurrences(string source, string substring)
        {
            // Если исходная строка или подстрока == null,
            // возвращаем исходную строку (без изменений).
            if (source == null || substring == null)
            {
                return source;
            }

            // Если длина подстроки больше длины строки => неуспешно
            if (substring.Length > source.Length)
            {
                return source;
            }

            // Если подстрока и исходная строка одной длины => 
            //   - если полностью совпадают, возвращаем пустую строку
            //   - иначе не найдём вхождений => возвращаем исходную
            if (substring.Length == source.Length)
            {
                if (source == substring)
                {
                    return string.Empty;
                }
                return source;
            }

            // Проверяем, найдётся ли подстрока вообще
            if (!source.Contains(substring))
            {
                // Нет вхождений => операция неуспешна
                return source;
            }

            // Если хотя бы одно вхождение есть — удаляем все вхождения
            // используя Replace(substring, "")
            string result = source.Replace(substring, "");
            return result;
        }

    }
}
