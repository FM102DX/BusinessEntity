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
    }
}
