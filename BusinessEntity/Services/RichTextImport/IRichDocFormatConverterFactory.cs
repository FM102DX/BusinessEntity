namespace BusinessEntity.Services.RichTextImport
{
    // Фабрика выбирает format-конвертер для импортируемого rich-document.
    public interface IRichDocFormatConverterFactory
    {
        IRichDocFormatConverter GetRequiredConverter(string fileExtension);
    }
}
