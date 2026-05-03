using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.RichText;
using BusinessEntity.Components;
using BusinessEntity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using ReactiveUI;

namespace BusinessEntity.Pages
{
    public partial class RichTextDocumentPage : IDisposable
    {
        [Parameter] public Guid Id { get; set; }

        [Inject] public RichTextDocumentHelper RichTextDocumentHelper { get; set; } = default!;
        [Inject] public RichTextDocumentImportService ImportService { get; set; } = default!;
        [Inject] public RichTextDocumentSettingsService RichTextDocumentSettingsService { get; set; } = default!;
        [Inject] public IMessageBus MessageBus { get; set; } = default!;

        private BusinessEntity.Core.Classes.BusinessEntity? Entity;
        private RichTextDocument? Manifest;
        private RichTextDocumentChunkWindow? InitialChunkWindow { get; set; }
        private IReadOnlyList<RichTextDocumentOutlineNode> OutlineNodes { get; set; } = Array.Empty<RichTextDocumentOutlineNode>();
        private bool IsLoading { get; set; } = true;
        private bool IsInitialContentLoading { get; set; }
        private bool IsOutlineLoading { get; set; }
        private bool IsImporting { get; set; }
        private bool IsRebuildingTableOfContents { get; set; }
        private string? Error;
        private string? StatusMessage;
        private CancellationTokenSource? LoadCancellationTokenSource { get; set; }
        private long LoadVersion { get; set; }
        private const int OutlineChunkBatchSize = 5;

        protected override async Task OnParametersSetAsync()
        {
            await LoadAsync();
        }

        // Loads only the document shell synchronously; heavy content parts are loaded in background tasks.
        private async Task LoadAsync()
        {
            LoadCancellationTokenSource?.Cancel();
            LoadCancellationTokenSource?.Dispose();
            LoadCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = LoadCancellationTokenSource.Token;
            var version = ++LoadVersion;

            IsLoading = true;
            IsInitialContentLoading = false;
            IsOutlineLoading = false;
            Error = null;
            InitialChunkWindow = null;
            OutlineNodes = Array.Empty<RichTextDocumentOutlineNode>();

            try
            {
                var shell = await RichTextDocumentHelper.GetRichTextDocumentShellAsync(Id, cancellationToken);
                if (shell == null)
                {
                    Entity = null;
                    Manifest = null;
                    Error = "Rich-text документ не найден.";
                    return;
                }

                Entity = shell.Entity;
                Manifest = shell.Manifest;
                IsInitialContentLoading = true;
                IsOutlineLoading = true;
                _ = LoadInitialChunkWindowAsync(Id, version, cancellationToken);
                _ = LoadOutlineAsync(Id, version, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // A newer document navigation superseded this load.
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                if (version == LoadVersion)
                {
                    IsLoading = false;
                }
            }
        }

        private async Task LoadInitialChunkWindowAsync(Guid documentId, long version, CancellationToken cancellationToken)
        {
            try
            {
                var richTextDocumentSettings = await RichTextDocumentSettingsService.GetSettingsAsync(cancellationToken);
                var chunkWindow = await RichTextDocumentHelper.GetChunkWindowAsync(
                    documentId,
                    0,
                    richTextDocumentSettings.GetInitialChunkCount(),
                    cancellationToken);

                await InvokeAsync(() =>
                {
                    if (version != LoadVersion || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    InitialChunkWindow = chunkWindow;
                    IsInitialContentLoading = false;
                    StateHasChanged();
                });
            }
            catch (OperationCanceledException)
            {
                // A newer document navigation superseded this load.
            }
            catch (Exception ex)
            {
                await InvokeAsync(() =>
                {
                    if (version != LoadVersion || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Error = ex.Message;
                    IsInitialContentLoading = false;
                    StateHasChanged();
                });
            }
        }

        private async Task LoadOutlineAsync(Guid documentId, long version, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var tableOfContents in RichTextDocumentHelper.GetTableOfContentsBatchesAsync(
                    documentId,
                    OutlineChunkBatchSize,
                    cancellationToken))
                {
                    var outlineNodes = tableOfContents.Select(MapTableOfContentsNode).ToList();

                    await InvokeAsync(() =>
                    {
                        if (version != LoadVersion || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        OutlineNodes = outlineNodes;
                        StateHasChanged();
                    });
                }

                await InvokeAsync(() =>
                {
                    if (version != LoadVersion || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    IsOutlineLoading = false;
                    StateHasChanged();
                });
            }
            catch (OperationCanceledException)
            {
                // A newer document navigation superseded this load.
            }
            catch (Exception ex)
            {
                await InvokeAsync(() =>
                {
                    if (version != LoadVersion || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Error = ex.Message;
                    IsOutlineLoading = false;
                    StateHasChanged();
                });
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

        // Пересоздаёт сохранённые chunk-properties содержания и обновляет HTML-cache на странице.
        private async Task OnRebuildTableOfContentsAsync()
        {
            if (Entity == null)
            {
                return;
            }

            IsRebuildingTableOfContents = true;
            StatusMessage = "Пересоздание содержания...";
            Error = null;

            try
            {
                var tableOfContents = await RichTextDocumentHelper.RebuildTableOfContentsAsync(Id);
                OutlineNodes = tableOfContents.Select(MapTableOfContentsNode).ToList();

                var richTextDocumentSettings = await RichTextDocumentSettingsService.GetSettingsAsync();
                InitialChunkWindow = await RichTextDocumentHelper.GetChunkWindowAsync(
                    Id,
                    0,
                    richTextDocumentSettings.GetInitialChunkCount());

                StatusMessage = "Содержание пересоздано.";
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                StatusMessage = null;
            }
            finally
            {
                IsRebuildingTableOfContents = false;
            }
        }

        private async Task OnEditorSavedAsync(RichTextDocumentEditorSaveRequest request)
        {
            Error = null;

            try
            {
                var titleResult = await RichTextDocumentHelper.SaveRichTextDocumentTitleAsync(Id, request.Title);
                Entity = titleResult.Entity;
                if (Manifest != null)
                {
                    Manifest.Name = titleResult.Title;
                    Manifest.LastModifiedDate = Entity.LastModifiedDate;
                }

                if (titleResult.TitleChanged)
                {
                    MessageBus.SendMessage(new EntityUpdatedMessage(Entity));
                }

                var statusParts = new List<string>();
                if (request.SavedChunkCount > 0)
                {
                    statusParts.Add($"Сохранено чанков: {request.SavedChunkCount}");
                }

                if (titleResult.TitleChanged)
                {
                    statusParts.Add("название сохранено");
                }

                StatusMessage = statusParts.Count == 0
                    ? "Нет изменений для сохранения."
                    : string.Join("; ", statusParts) + ".";

                IsOutlineLoading = true;
                await foreach (var tableOfContents in RichTextDocumentHelper.GetTableOfContentsBatchesAsync(
                    Id,
                    OutlineChunkBatchSize))
                {
                    OutlineNodes = tableOfContents.Select(MapTableOfContentsNode).ToList();
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                StatusMessage = null;
            }
            finally
            {
                IsOutlineLoading = false;
            }
        }

        // Converts storage-backed rich-text table-of-contents entries into UI outline nodes.
        private static RichTextDocumentOutlineNode MapTableOfContentsNode(RichTextDocumentTableOfContentsEntry entry)
        {
            return new RichTextDocumentOutlineNode
            {
                HeadingId = entry.Anchor,
                ChunkSortOrder = entry.ChunkSortOrder,
                Title = entry.Title,
                Level = entry.Level,
                IsExpanded = true,
                Children = entry.Children.Select(MapTableOfContentsNode).ToList()
            };
        }

        public void Dispose()
        {
            LoadCancellationTokenSource?.Cancel();
            LoadCancellationTokenSource?.Dispose();
        }
    }
}
