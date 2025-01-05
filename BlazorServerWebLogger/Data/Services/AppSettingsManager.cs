using BlazorServerWebLogger.Contracts;
using Newtonsoft.Json;
using SampleOnlineMall.Service;


namespace BlazorServerWebLogger.Data.Services
{
    //класс для управления настройками, которые пользователь меняет в рантайме.
    //это положение гуи - элементов, отображение вкладок, вкл-выкл категорий контента и т.п.
    public class AppSettingsManager
    {
        private IAsyncRepository<AppSettingsDbStorable> _repo;

        public AppSettingsManager(IAsyncRepository<AppSettingsDbStorable> repo)
        {
            _repo = repo;
        }

        private async Task<AppSettingsDbStorable?> GetSettingsRecord()
        {
            var tmp= await _repo.GetAllAsync(f => f.SettingsDomain == nameof(LoggerMainViewSettings) && f.UserName=="Admin", 1);
            var result = tmp.Items.FirstOrDefault();
            return result;
        }

        public async Task<LoggerMainViewSettings> LoadSettings()
        {
            var item = await GetSettingsRecord();
            if (item != null && !string.IsNullOrWhiteSpace(item.SettingsJsonData))
            {
                var result = JsonConvert.DeserializeObject<LoggerMainViewSettings>(item.SettingsJsonData);
                return result;
            }
            //если ничего нет, возвращаем пустой класс
            return new LoggerMainViewSettings();
        }

        public async Task<CommonOperationResult> SaveSettings(LoggerMainViewSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            // Сериализация объекта в строку JSON
            string jsonData = JsonConvert.SerializeObject(settings, Formatting.Indented);

            // Получение текущей записи из базы
            var item = await GetSettingsRecord() ?? new AppSettingsDbStorable
            {
                Id = Guid.Empty,
                UserName = "Admin"
            };

            // Обновление полей
            item.SettingsDomain = nameof(LoggerMainViewSettings);
            item.SettingsJsonData = jsonData;

            // Вставка или обновление записи
            return item.Id == Guid.Empty
                ? await _repo.InsertAsync(item)
                : await _repo.UpdateAsync(item);
        }
    }
}
