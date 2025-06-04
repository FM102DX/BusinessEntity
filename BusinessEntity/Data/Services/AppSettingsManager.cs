using BusinessEntity.Contracts;
using Newtonsoft.Json;
using SampleOnlineMall.Service;
using SampleOnlineMall.WebLogger.Models;


namespace BusinessEntity.Data.Services
{
    //класс для управления настройками, которые пользователь меняет в рантайме.
    //это положение гуи - элементов, отображение вкладок, вкл-выкл категорий контента и т.п.
    public class AppSettingsManager
    {
        private IAsyncRepository<AppSettingsDbStorable> _repo;

        public AppSettingsManager(IRepositoryFactory<AppSettingsDbStorable> repositoryFactory)
        {
            _repo = repositoryFactory.GetRepository();
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
            try
            {
                // 1. Проверка аргумента
                if (settings == null)
                    throw new ArgumentNullException(nameof(settings));

                // 2. Сериализация в JSON
                string jsonData = JsonConvert.SerializeObject(settings, Formatting.Indented);

                // 3. Получение или создание записи
                var item = await GetSettingsRecord() ?? new AppSettingsDbStorable
                {
                    UserName = "Admin" // или другие поля по умолчанию
                };
                bool isNew = (item.Id == Guid.Empty);
                if (!isNew)
                    item = await _repo.GetByIdOrNullAsync(item.Id); //получаем сущность, которая уже трекается

                // 4. Обновляем поля
                item.SettingsDomain = nameof(LoggerMainViewSettings);
                item.SettingsJsonData = jsonData;

                // 5. Вставка или обновление
                

                if (isNew)
                {
                    await _repo.InsertAsync(item);
                }
                else
                {
                    await _repo.UpdateAsync(item);
                }

                // 6. Возвращаем успешный результат
                return new CommonOperationResult
                {
                    Success = true,
                    Message = "Настройки успешно сохранены."
                };
            }
            catch (Exception ex)
            {
                // Логируем ошибку в консоль
                Console.WriteLine($"Ошибка в SaveSettings: {ex.Message}");

                // Если есть вложенная ошибка (InnerException), выводим и её
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"InnerException: {ex.InnerException.Message}");
                }

                // 7. Возвращаем результат с информацией об ошибке
                return new CommonOperationResult
                {
                    Success = false,
                    Message = $"Ошибка при сохранении настроек: {ex.Message}" +
                              (ex.InnerException != null ? $" | Inner: {ex.InnerException.Message}" : string.Empty)
                };
            }
        }


    }
}
