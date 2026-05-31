namespace BusinessEntity.Services.RichTextPaste
{
    // Детектор формата clipboard-буфера без опоры на расширение файла.
    public interface IRichTextClipboardPasteDetector
    {
        RichTextClipboardPasteSource? Detect(RichTextClipboardPasteRequest request);
    }
}
