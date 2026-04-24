using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Services;
using System.Text.RegularExpressions;

namespace BusinessEntity.Services
{
    // Инкапсулирует бизнес-операции управления пространствами.
    public class SpaceHelper
    {
        // Требует минимум 5 непробельных символов в начале, далее разрешает только буквы, цифры, "_" и пробелы.
        private static readonly Regex SpaceNameRegex = new(
            @"^[\p{L}\p{Nd}_]{5}[\p{L}\p{Nd}_ ]*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Основной helper бизнес-сущностей.
        private readonly BusinessEntityHelper _businessEntityHelper;

        // Подключает helper бизнес-сущностей для CRUD операций с пространствами.
        public SpaceHelper(BusinessEntityHelper businessEntityHelper)
        {
            _businessEntityHelper = businessEntityHelper;
        }

        /// <summary>
        /// Получает одно пространство по идентификатору.
        /// </summary>
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> GetSpaceByIdAsync(Guid spaceId)
        {
            var entity = await _businessEntityHelper.GetBusinessEntityById(spaceId);
            return entity?.EntityType == BusinessEntityTypeEnum.Space ? entity : null;
        }

        /// <summary>
        /// Возвращает все пространства.
        /// </summary>
        public async Task<IReadOnlyList<BusinessEntity.Core.Classes.BusinessEntity>> GetSpacesAsync(CancellationToken ct = default)
        {
            var spaces = await _businessEntityHelper.GetSpacesAsync();
            return spaces
                .OrderBy(x => x.CreatedDate)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Создает новое пространство.
        /// </summary>
        public async Task<BusinessEntity.Core.Classes.BusinessEntity> CreateSpaceAsync(string name, CancellationToken ct = default)
        {
            var normalizedName = NormalizeName(name);
            return await _businessEntityHelper.CreateBusinessEntity(BusinessEntityTypeEnum.Space, normalizedName);
        }

        /// <summary>
        /// Переименовывает пространство.
        /// </summary>
        public async Task<BusinessEntity.Core.Classes.BusinessEntity?> RenameSpaceAsync(Guid spaceId, string newName, CancellationToken ct = default)
        {
            var space = await GetRequiredSpaceAsync(spaceId, ct);
            var normalizedName = NormalizeName(newName);
            return await _businessEntityHelper.RenameEntity(space.Id, normalizedName, ct);
        }

        /// <summary>
        /// Удаляет пространство вместе с его поддеревом.
        /// </summary>
        public async Task DeleteSpaceAsync(Guid spaceId, CancellationToken ct = default)
        {
            var space = await GetRequiredSpaceAsync(spaceId, ct);
            var deleteResult = await _businessEntityHelper.RemoveBusinessEntityPermanently(space.Id, ct);
            if (!deleteResult.success)
            {
                var message = deleteResult.messages.Count > 0
                    ? string.Join(Environment.NewLine, deleteResult.messages)
                    : $"Не удалось удалить пространство '{space.Name}'.";
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Нормализует и проверяет имя пространства.
        /// </summary>
        private static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Имя пространства не может быть пустым.", nameof(name));
            }

            var normalizedName = name.Trim();
            if (!SpaceNameRegex.IsMatch(normalizedName))
            {
                throw new ArgumentException(
                    "Имя пространства должно начинаться минимум с 5 символов. Допустимы только буквы, цифры, символ '_' и пробелы.",
                    nameof(name));
            }

            return normalizedName;
        }

        /// <summary>
        /// Загружает пространство и проверяет его тип.
        /// </summary>
        private async Task<BusinessEntity.Core.Classes.BusinessEntity> GetRequiredSpaceAsync(Guid spaceId, CancellationToken ct)
        {
            var space = await GetSpaceByIdAsync(spaceId);
            if (space == null)
            {
                throw new InvalidOperationException($"Пространство с id '{spaceId}' не найдено.");
            }

            return space;
        }
    }
}
