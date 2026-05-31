namespace BlazorServerWebLogger.Data
{
    public class LogEraserSettings
    {
        public bool Enabled { get; set; } // Включить/выключить службу очистки
        public int ErasePeriod { get; set; } // Время в миллисекундах
        public int LogsTargetCount { get; set; } // Целевое количество записей
    }
}
