using System;
using System.ComponentModel.DataAnnotations;

namespace SampleOnlineMall.Service.WebLogging
{
    /// <summary>
    /// Класс для передачи лог-сообщений между сервисами.
    /// Используется для сериализации/десериализации логов при трансфере.
    /// </summary>
    public class LogEntryTransferDto
    {
        /// <summary>
        /// Дата и время создания лог-записи.
        /// </summary>
        [Required]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Код сервиса, отправившего лог-сообщение.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ServiceCode { get; set; }

        /// <summary>
        /// Тип сообщения (например, Info, Error, Warning).
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string MessageType { get; set; }

        /// <summary>
        /// Текст лог-сообщения.
        /// </summary>
        [Required]
        public string Message { get; set; }
    }
}

