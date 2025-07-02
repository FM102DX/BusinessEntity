using System.ComponentModel.DataAnnotations;
using BlazorServerWebLogger.Contracts;

namespace BlazorServerWebLogger.Data
{
    public class AppSettingsDbStorable : UserBaseEntity, IBaseEntity
    {
        public string SettingsDomain { get; set; }
        public string SettingsJsonData { get; set; }

    }
}
