using BusinessEntity.MiniApps.MediaServerMiniApp.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace BusinessEntity.Controllers;

// API физической отдачи и upload-а файлов MediaServerMiniApp.
[ApiController]
[Route("media-server-files")]
public sealed class MediaServerFilesController : ControllerBase
{
    private readonly IMediaServerService _mediaServerService;

    public MediaServerFilesController(IMediaServerService mediaServerService)
    {
        _mediaServerService = mediaServerService;
    }

    [HttpGet("videos/{videoId:guid}/original")]
    public async Task<IActionResult> GetVideo(Guid videoId, CancellationToken cancellationToken)
    {
        var file = await _mediaServerService.GetVideoFileAsync(videoId, cancellationToken);
        if (file == null)
        {
            return NotFound();
        }

        return PhysicalFile(
            file.PhysicalPath,
            file.ContentType,
            file.FileName,
            enableRangeProcessing: true);
    }

    [HttpPost("videos")]
    [RequestSizeLimit(2L * 1024L * 1024L * 1024L)]
    public async Task<IActionResult> UploadVideo(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length <= 0)
        {
            return BadRequest("Файл видео не передан.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _mediaServerService.UploadVideoAsync(
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
