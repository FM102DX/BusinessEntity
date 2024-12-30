using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using BlazorServerWebLogger.Contracts;

namespace BlazorServerWebLogger.Data
{
    public class BaseEntity : IBaseEntity
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
        public DateTime Timestamp { get; set; }
    }
}
