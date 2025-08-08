using SampleOnlineMall.WebLogger.Services;
using System.Text.RegularExpressions;

namespace BusinessEntity.Core.Services
{
    /// <summary>
    /// Утилитарный класс для валидации имен бизнес-сущностей
    /// </summary>
    public class EntityNameValidator
    {
        private readonly IWebLoggerService _webLogger;

        public EntityNameValidator(IWebLoggerService webLogger)
        {
            _webLogger = webLogger ?? throw new ArgumentNullException(nameof(webLogger));
        }

        public bool IsValidEntityName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Обрезаем пробелы в начале и конце
            var trimmedName = name.Trim();

            if (string.IsNullOrEmpty(trimmedName))
                return false;

            // Проверяем, что все символы допустимы:
            // любые буквы, цифры, пробелы, подчёркивания, дефисы
            var allowedCharsPattern = @"^[\p{L}\p{Nd}\s_-]+$";
            if (!Regex.IsMatch(trimmedName, allowedCharsPattern))
                return false;

            // Проверяем, что есть хотя бы одна буква (любой алфавит)
            var hasLetterPattern = @"\p{L}";
            if (!Regex.IsMatch(trimmedName, hasLetterPattern))
                return false;

            return true;
        }
    }
}
