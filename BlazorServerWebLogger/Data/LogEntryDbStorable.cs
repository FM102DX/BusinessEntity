using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BlazorServerWebLogger.Contracts;
using BlazorServerWebLogger.Data;

namespace BusinessEntity.WebLogger.Models
{
    /// <summary>
    /// Класс для представления лог-сообщения в базе данных.
    /// </summary>
    public class LogEntryDbStorable : BaseEntity, IBaseEntity
    {
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

