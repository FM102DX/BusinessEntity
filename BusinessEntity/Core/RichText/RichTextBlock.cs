using System.Text.Json.Serialization;

// Технические модели блоков rich-text документа.
namespace BusinessEntity.Core.RichText
{
    // Блок rich-text документа в нормализованном MVP-формате.
    public class RichTextBlock
    {
        // Тип блока: paragraph / heading / image / video / table.
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "paragraph";

        // Уровень заголовка для heading-блоков.
        [JsonPropertyName("level")]
        public int Level { get; set; }

        // Inline-HTML содержимое paragraph/heading блока или безопасный table-html для table-блока.
        [JsonPropertyName("html")]
        public string Html { get; set; } = string.Empty;

        // Идентификатор embedded-изображения.
        [JsonPropertyName("imageId")]
        public string ImageId { get; set; } = string.Empty;

        // Вариант отображения embedded-изображения.
        [JsonPropertyName("displayVariant")]
        public string DisplayVariant { get; set; } = "original";

        // Альтернативный текст изображения.
        [JsonPropertyName("altText")]
        public string AltText { get; set; } = string.Empty;

        // Отображаемая ширина изображения в пикселях. 0 означает original/auto.
        [JsonPropertyName("width")]
        public int Width { get; set; }

        // Отображаемая высота изображения в пикселях. 0 означает original/auto.
        [JsonPropertyName("height")]
        public int Height { get; set; }

        // Идентификатор видео из общего мультимедиа-хранилища.
        [JsonPropertyName("videoId")]
        public string VideoId { get; set; } = string.Empty;

        // Отображаемое имя видео.
        [JsonPropertyName("videoTitle")]
        public string VideoTitle { get; set; } = string.Empty;
    }
}
