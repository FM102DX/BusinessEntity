using BusinessEntity.Services;
using Microsoft.AspNetCore.Mvc;

namespace BusinessEntity.Controllers
{
    // Отдает embedded-файлы rich-text документа из технического storage.
    [ApiController]
    [Route("rich-document-files")]
    public class RichTextDocumentFilesController : ControllerBase
    {
        private readonly RichTextDocumentHelper _richTextDocumentHelper;

        public RichTextDocumentFilesController(RichTextDocumentHelper richTextDocumentHelper)
        {
            _richTextDocumentHelper = richTextDocumentHelper;
        }

        // Возвращает embedded-изображение rich-text документа по imageId и variant.
        [HttpGet("{documentId:guid}/images/{imageId}/{variant}")]
        public async Task<IActionResult> GetImage(Guid documentId, string imageId, string variant, CancellationToken cancellationToken)
        {
            var file = await _richTextDocumentHelper.GetRichTextEmbeddedFileAsync(documentId, imageId, variant, cancellationToken);
            if (file == null)
            {
                return NotFound();
            }

            return File(file.Content, file.ContentType, file.FileName);
        }

        // Загружает одно embedded-изображение rich-text документа.
        [HttpPost("{documentId:guid}/images")]
        [RequestSizeLimit(20L * 1024L * 1024L)]
        public async Task<IActionResult> UploadImage(Guid documentId, IFormFile file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length <= 0)
            {
                return BadRequest("Файл изображения не передан.");
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await _richTextDocumentHelper.SaveRichTextEmbeddedImageAsync(
                    documentId,
                    stream,
                    file.FileName,
                    file.ContentType,
                    cancellationToken);

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }
    }
}
