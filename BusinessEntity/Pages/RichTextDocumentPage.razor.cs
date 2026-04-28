using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.RichText;
using BusinessEntity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BusinessEntity.Pages
{
    public partial class RichTextDocumentPage
    {
        [Parameter] public Guid Id { get; set; }

        [Inject] public RichTextDocumentHelper RichTextDocumentHelper { get; set; } = default!;
        [Inject] public RichTextDocumentImportService ImportService { get; set; } = default!;

        private BusinessEntity.Core.Classes.BusinessEntity? Entity;
        private RichTextDocument? Manifest;
        private string HtmlContent { get; set; } = string.Empty;
        private bool IsLoading { get; set; } = true;
        private bool IsImporting { get; set; }
        private string? Error;
        private string? StatusMessage;

        protected override async Task OnParametersSetAsync()
        {
            await LoadAsync();
        }

        // Загружает rich-text документ и собирает readonly HTML из сохраненных чанков.
        private async Task LoadAsync()
        {
            IsLoading = true;
            Error = null;

            try
            {
                var snapshot = await RichTextDocumentHelper.GetRichTextDocumentSnapshotAsync(Id);
                if (snapshot == null)
                {
                    Entity = null;
                    Manifest = null;
                    HtmlContent = string.Empty;
                    Error = "Rich-text документ не найден.";
                    return;
                }

                Entity = snapshot.Entity;
                Manifest = snapshot.Manifest;
                HtmlContent = string.Join(Environment.NewLine, snapshot.Chunks.Select(chunk => chunk.HtmlCache ?? string.Empty));
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Импортирует выбранный пользователем файл в rich-text документ.
        private async Task OnImportSelectedAsync(InputFileChangeEventArgs args)
        {
            if (Entity == null)
            {
                return;
            }

            var file = args.File;
            if (file == null)
            {
                return;
            }

            IsImporting = true;
            StatusMessage = $"Импорт файла '{file.Name}'...";
            Error = null;

            try
            {
                await using var stream = file.OpenReadStream(maxAllowedSize: 20 * 1024 * 1024);
                var importResult = await ImportService.ImportAsync(file.Name, stream);

                var manifest = Manifest ?? new RichTextDocument();
                manifest.Tag = string.IsNullOrWhiteSpace(importResult.Manifest.Tag)
                    ? manifest.Tag
                    : importResult.Manifest.Tag;
                manifest.ContentStorage = importResult.Manifest.ContentStorage;
                manifest.EditorFormat = importResult.Manifest.EditorFormat;
                manifest.ChunkPolicy = importResult.Manifest.ChunkPolicy;
                manifest.EmbeddedFileStorage = importResult.Manifest.EmbeddedFileStorage;
                manifest.SupportsImages = importResult.Manifest.SupportsImages;

                // Импорт больше не заменяет документ целиком: новый контент добавляется в конец текущего содержимого.
                await RichTextDocumentHelper.AppendImportedContentAsync(
                    Entity,
                    manifest,
                    importResult.Chunks,
                    importResult.Files);

                StatusMessage = $"Импорт файла '{file.Name}' добавлен в конец документа.";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                StatusMessage = null;
            }
            finally
            {
                IsImporting = false;
            }
        }
    }
}
