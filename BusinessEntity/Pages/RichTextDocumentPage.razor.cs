using BusinessEntity.Core.Classes;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.RichText;
using BusinessEntity.Components;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
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
        [Inject] public RichTextDocumentMessagePanelService MessagePanel { get; set; } = default!;
        [Inject] public IDataProviderConnector DataProviderConnector { get; set; } = default!;
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
        private int VersionsRefreshToken { get; set; }
        private int ViewedVersion { get; set; } = 1;
        private int LatestVersion { get; set; } = 1;
        private bool CanEditViewedVersion => ViewedVersion >= LatestVersion;
        private Guid _messagesForDocumentId;
        private Guid _loadedDocumentId;
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

            if (_messagesForDocumentId != Id)
            {
                MessagePanel.Clear();
                _messagesForDocumentId = Id;
            }

            if (_loadedDocumentId != Id)
            {
                ViewedVersion = 0;
                LatestVersion = 1;
                _loadedDocumentId = Id;
            }

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
                await RefreshVersionsAsync(cancellationToken);
                IsInitialContentLoading = true;
                IsOutlineLoading = true;
                _ = LoadInitialChunkWindowAsync(Id, version, ViewedVersion, cancellationToken);
                _ = LoadOutlineAsync(Id, version, ViewedVersion, cancellationToken);
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

        private async Task LoadInitialChunkWindowAsync(
            Guid documentId,
            long loadVersion,
            int documentVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                var richTextDocumentSettings = await RichTextDocumentSettingsService.GetSettingsAsync(cancellationToken);
                var chunkWindow = await RichTextDocumentHelper.GetChunkWindowAsync(
                    documentId,
                    0,
                    richTextDocumentSettings.GetInitialChunkCount(),
                    documentVersion,
                    cancellationToken);

                await InvokeAsync(() =>
                {
                    if (loadVersion != LoadVersion || cancellationToken.IsCancellationRequested)
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
                    if (loadVersion != LoadVersion || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Error = ex.Message;
                    IsInitialContentLoading = false;
                    StateHasChanged();
                });
            }
        }

        private async Task LoadOutlineAsync(
            Guid documentId,
            long loadVersion,
            int documentVersion,
            CancellationToken cancellationToken)
        {
            try
            {
                var richTextDocumentSettings = await RichTextDocumentSettingsService.GetSettingsAsync(cancellationToken);
                await foreach (var tableOfContents in RichTextDocumentHelper.GetTableOfContentsBatchesAsync(
                    documentId,
                    OutlineChunkBatchSize,
                    documentVersion,
                    richTextDocumentSettings.GetInitialChunkCount(),
                    cancellationToken))
                {
                    var outlineNodes = tableOfContents.Select(MapTableOfContentsNode).ToList();

                    await InvokeAsync(() =>
                    {
                        if (loadVersion != LoadVersion || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        OutlineNodes = outlineNodes;
                        StateHasChanged();
                    });
                }

                await InvokeAsync(() =>
                {
                    if (loadVersion != LoadVersion || cancellationToken.IsCancellationRequested)
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
                    if (loadVersion != LoadVersion || cancellationToken.IsCancellationRequested)
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
            if (Entity == null || !CanEditViewedVersion)
            {
                return;
            }

            var file = args.File;
            if (file == null)
            {
                return;
            }

            IsImporting = true;
            SetStatusMessage($"Импорт файла '{file.Name}'...");
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

                SetStatusMessage($"Импорт файла '{file.Name}' добавлен в конец документа.");
                VersionsRefreshToken++;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                SetStatusMessage(null);
            }
            finally
            {
                IsImporting = false;
            }
        }

        // Пересоздаёт сохранённые chunk-properties содержания и обновляет HTML-cache на странице.
        private async Task OnRebuildTableOfContentsAsync()
        {
            if (Entity == null || !CanEditViewedVersion)
            {
                return;
            }

            IsRebuildingTableOfContents = true;
            SetStatusMessage("Пересоздание содержания...");
            Error = null;

            try
            {
                var richTextDocumentSettings = await RichTextDocumentSettingsService.GetSettingsAsync();
                InitialChunkWindow = await RichTextDocumentHelper.GetChunkWindowAsync(
                    Id,
                    0,
                    richTextDocumentSettings.GetInitialChunkCount(),
                    ViewedVersion);

                await foreach (var tableOfContents in RichTextDocumentHelper.GetTableOfContentsBatchesAsync(
                    Id,
                    OutlineChunkBatchSize,
                    ViewedVersion,
                    richTextDocumentSettings.GetInitialChunkCount()))
                {
                    OutlineNodes = tableOfContents.Select(MapTableOfContentsNode).ToList();
                    await InvokeAsync(StateHasChanged);
                }

                SetStatusMessage("Содержание обновлено для загруженного окна документа.");
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                SetStatusMessage(null);
            }
            finally
            {
                IsRebuildingTableOfContents = false;
            }
        }

        private async Task OnEditorSavedAsync(RichTextDocumentEditorSaveRequest request)
        {
            if (!CanEditViewedVersion)
            {
                return;
            }

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

                SetStatusMessage(statusParts.Count == 0
                    ? "Нет изменений для сохранения."
                    : string.Join("; ", statusParts) + ".");
                VersionsRefreshToken++;
                await RefreshVersionsAsync();
                ViewedVersion = LatestVersion;

                IsOutlineLoading = true;
                var richTextDocumentSettings = await RichTextDocumentSettingsService.GetSettingsAsync();
                await foreach (var tableOfContents in RichTextDocumentHelper.GetTableOfContentsBatchesAsync(
                    Id,
                    OutlineChunkBatchSize,
                    ViewedVersion,
                    richTextDocumentSettings.GetInitialChunkCount()))
                {
                    OutlineNodes = tableOfContents.Select(MapTableOfContentsNode).ToList();
                    await InvokeAsync(StateHasChanged);
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                SetStatusMessage(null);
            }
            finally
            {
                IsOutlineLoading = false;
            }
        }

        private async Task OnVersionSelectedAsync(int version)
        {
            if (Entity == null || version <= 0 || version == ViewedVersion)
            {
                return;
            }

            LoadCancellationTokenSource?.Cancel();
            LoadCancellationTokenSource?.Dispose();
            LoadCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = LoadCancellationTokenSource.Token;
            var loadVersion = ++LoadVersion;

            ViewedVersion = Math.Min(version, LatestVersion);
            InitialChunkWindow = null;
            OutlineNodes = Array.Empty<RichTextDocumentOutlineNode>();
            IsInitialContentLoading = true;
            IsOutlineLoading = true;
            Error = null;
            await InvokeAsync(StateHasChanged);

            _ = LoadInitialChunkWindowAsync(Id, loadVersion, ViewedVersion, cancellationToken);
            _ = LoadOutlineAsync(Id, loadVersion, ViewedVersion, cancellationToken);
        }

        private async Task RefreshVersionsAsync(CancellationToken cancellationToken = default)
        {
            var versions = await DataProviderConnector.GetDataVersionsAsync(Id, cancellationToken);
            LatestVersion = versions.Count == 0
                ? Math.Max(Manifest?.Version ?? 1, 1)
                : versions.Max(x => x.Version <= 0 ? 1 : x.Version);

            if (ViewedVersion <= 0 || ViewedVersion > LatestVersion)
            {
                ViewedVersion = LatestVersion;
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

        private void SetStatusMessage(string? message)
        {
            StatusMessage = message;
            MessagePanel.Add(Id, message);
        }

        public void Dispose()
        {
            LoadCancellationTokenSource?.Cancel();
            LoadCancellationTokenSource?.Dispose();
        }
    }
}
