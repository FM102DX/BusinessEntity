using System.Collections.ObjectModel;
using BlazorServerWebLogger.Data.Services;
using Microsoft.AspNetCore.Components;
using SampleOnlineMall.WebLogger.Models;

namespace BlazorServerWebLogger.Pages
{
public partial class Index : ComponentBase
{
    public int TotalLogsCount { get; set; } = 0;
    public ObservableCollection<LogEntryDbStorable> LogEntries { get; set; } = new();
    //public List<LogEntryDbStorable> PagedLogEntries => LogEntries.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
    public IEnumerable<int> SizeArr => (new int[] { 20, 40, 60, 80, 100 }) as IEnumerable<int>;
    //public int CurrentPage { get; set; } = 1;
    //public int PageSize { get; set; } = 25;

    [Inject]
    public LogReaderService LogReaderService { get; set; }

    private Timer _timer;

    protected override async Task OnInitializedAsync()
    {
        var data = await LogReaderService.ReadInitialAsync(n: 50);
        TotalLogsCount = data.Count;
        LogEntries = new ObservableCollection<LogEntryDbStorable>(data);

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
                    LogEntries.Insert(0, entry); // Новые записи добавляются сверху
                }

                TotalLogsCount = totalCount;
                StateHasChanged();
            }
        });
    }

    public void OnPageChanged(int page)
    {
        //CurrentPage = page;
        StateHasChanged();
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}}