using Microsoft.AspNetCore.Components;
using SampleOnlineMall.WebLogger.Models;

namespace BusinessEntity.Components
{
    public partial class LogMessageViewer : ComponentBase
    {
        [Parameter]
        public LogEntryDbStorable LogEntry { get; set; } = default!;
    }
} 