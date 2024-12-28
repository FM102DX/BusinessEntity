using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.Models;

namespace SampleOnlineMall.WebLogger.Models
{
    /// <summary>
    /// Класс для представления лог-сообщения в базе данных.
    /// </summary>
    public class LogEntryDbStorable
    {
        /// <summary>
        /// Уникальный идентификатор записи лога (GUID).
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public Guid Id { get; set; }

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

