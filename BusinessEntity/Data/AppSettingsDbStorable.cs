using System.ComponentModel.DataAnnotations;
using BusinessEntity.Contracts;

namespace BusinessEntity.Data
{
    public class AppSettingsDbStorable : UserBaseEntity, IBaseEntity
    {
        public string SettingsDomain { get; set; }
        public string SettingsJsonData { get; set; }

    }
}
