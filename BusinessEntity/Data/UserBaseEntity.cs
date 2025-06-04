using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using BusinessEntity.Contracts;

namespace BusinessEntity.Data
{
    public abstract class UserBaseEntity : BaseEntity,IBaseEntity
    {
        public string UserName { get; set; }
    }
}
