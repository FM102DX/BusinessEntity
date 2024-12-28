namespace BlazorServerWebLogger.Data
{
    public class LogEraserSettings
    {
        public int ErasePeriod { get; set; } // Время в миллисекундах
        public int LogsTargetCount { get; set; } // Целевое количество записей
    }
}
