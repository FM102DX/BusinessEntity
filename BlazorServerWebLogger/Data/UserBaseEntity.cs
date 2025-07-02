using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using BlazorServerWebLogger.Contracts;

namespace BlazorServerWebLogger.Data
{
    public abstract class UserBaseEntity : BaseEntity,IBaseEntity
    {
        public string UserName { get; set; }
    }
}
