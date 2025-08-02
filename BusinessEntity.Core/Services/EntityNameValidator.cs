using System.Text.RegularExpressions;

namespace BusinessEntity.Core.Services
{
    /// <summary>
    /// Утилитарный класс для валидации имен бизнес-сущностей
    /// </summary>
    public static class EntityNameValidator
    {
        /// <summary>
        /// Проверяет, является ли имя сущности валидным
        /// </summary>
        /// <param name="name">Имя для проверки</param>
        /// <returns>true, если имя валидно</returns>
        public static bool IsValidEntityName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            // Обрезаем пробелы в начале и конце
            var trimmedName = name.Trim();
            
            if (string.IsNullOrEmpty(trimmedName))
                return false;

            // Проверяем, что все символы допустимы (кириллица, латиница, пробелы, _, -)
            var allowedCharsPattern = @"^[а-яёА-ЯЁa-zA-Z\s_\-]+$";
            if (!Regex.IsMatch(trimmedName, allowedCharsPattern))
                return false;

            // Проверяем, что есть хотя бы одна буква (кириллица или латиница)
            var hasLetterPattern = @"[а-яёА-ЯЁa-zA-Z]";
            if (!Regex.IsMatch(trimmedName, hasLetterPattern))
                return false;

            return true;
        }

        /// <summary>
        /// Нормализует имя сущности (обрезает пробелы)
        /// </summary>
        /// <param name="name">Имя для нормализации</param>
        /// <returns>Нормализованное имя</returns>
        public static string NormalizeName(string name)
        {
            return name?.Trim() ?? string.Empty;
        }
    }
}
