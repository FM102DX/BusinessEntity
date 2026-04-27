using System.Text.Json.Serialization;

// Технические модели блоков rich-text документа.
namespace BusinessEntity.Core.RichText
{
    // Блок rich-text документа в нормализованном MVP-формате.
    public class RichTextBlock
    {
        // Тип блока: paragraph / heading / image.
        [JsonPropertyName("kind")]
        public string Kind { get; set; } = "paragraph";

        // Уровень заголовка для heading-блоков.
        [JsonPropertyName("level")]
        public int Level { get; set; }

        // Inline-HTML содержимое paragraph/heading блока.
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
    }
}
