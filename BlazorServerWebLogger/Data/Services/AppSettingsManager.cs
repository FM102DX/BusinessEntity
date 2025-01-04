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

        public async Task<LoggerMainViewSettings> LoadSettings()
        {
            var itemTmp = await _repo.GetAllAsync(f => f.SettingsDomain == nameof(LoggerMainViewSettings), 1);
            var item = itemTmp.Items.ToList().FirstOrDefault();
            if (item != null && string.IsNullOrWhiteSpace(item.SettingsJsonData))
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

            // Логика сохранения JSON-строки, например, в файл или базу данных
            // Здесь сохраняем в базу данных, используя ваш репозиторий
            var item = new AppSettingsDbStorable
            {
                SettingsDomain = nameof(LoggerMainViewSettings),
                SettingsJsonData = jsonData
            };

            // Сохранение в базу данных через репозиторий
            var result = await _repo.UpdateAsync(item);
            return result;
        }
    }
}
