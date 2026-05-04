using BusinessEntity.Core.RichText;
using BusinessEntity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentView : ComponentBase
    {
        [Parameter] public string EntityName { get; set; } = string.Empty;
        [Parameter] public Guid BusinessEntityId { get; set; }
        [Parameter] public RichTextDocumentChunkWindow? InitialChunkWindow { get; set; }
        [Parameter] public bool IsInitialContentLoading { get; set; }
        [Parameter] public bool IsOutlineLoading { get; set; }
        [Parameter] public bool IsBusy { get; set; }
        [Parameter] public bool IsRebuildingTableOfContents { get; set; }
        [Parameter] public string? StatusMessage { get; set; }
        [Parameter] public IReadOnlyList<RichTextDocumentOutlineNode>? OutlineNodes { get; set; }
        [Parameter] public EventCallback<InputFileChangeEventArgs> OnImportSelected { get; set; }
        [Parameter] public EventCallback OnRebuildTableOfContents { get; set; }
        [Parameter] public EventCallback<RichTextDocumentEditorSaveRequest> OnEditorSaved { get; set; }

        private RichTextDocumentEditView? EditView { get; set; }
        private bool IsEditMode { get; set; }
        private bool IsSaving { get; set; }
        private string EditableEntityName { get; set; } = string.Empty;
        private string? TitleValidationMessage { get; set; }
        private RichTextDocumentViewportPosition? EditInitialPosition { get; set; }
        private RichTextDocumentViewportPosition? ReadInitialPosition { get; set; }

        protected override void OnParametersSet()
        {
            if (!IsEditMode)
            {
                EditableEntityName = RichTextDocumentHelper.FilterRichTextDocumentTitle(EntityName);
                TitleValidationMessage = null;
            }
        }

        private Task HandleEditRequestedAsync(RichTextDocumentViewportPosition? visiblePosition)
        {
            EditInitialPosition = visiblePosition;
            ReadInitialPosition = null;
            IsEditMode = true;
            EditableEntityName = RichTextDocumentHelper.FilterRichTextDocumentTitle(EntityName);
            TitleValidationMessage = null;
            return Task.CompletedTask;
        }

        private Task HandleTitleChangedAsync(string? value)
        {
            EditableEntityName = RichTextDocumentHelper.FilterRichTextDocumentTitle(value);
            TitleValidationMessage = string.IsNullOrWhiteSpace(EditableEntityName)
                ? "Название не может быть пустым."
                : null;
            return Task.CompletedTask;
        }

        private async Task HandleSaveAsync()
        {
            await SaveEditorChangesAsync();
        }

        private async Task HandleReadModeAsync(RichTextDocumentViewportPosition? visiblePosition)
        {
            if (await SaveEditorChangesAsync())
            {
                ReadInitialPosition = visiblePosition;
                IsEditMode = false;
                EditView = null;
                EditInitialPosition = null;
                TitleValidationMessage = null;
            }
        }

        private async Task<bool> SaveEditorChangesAsync()
        {
            if (IsSaving || EditView == null)
            {
                return false;
            }

            try
            {
                IsSaving = true;
                TitleValidationMessage = null;

                try
                {
                    EditableEntityName = RichTextDocumentHelper.NormalizeRichTextDocumentTitle(EditableEntityName);
                }
                catch (ArgumentException ex)
                {
                    TitleValidationMessage = ex.Message;
                    return false;
                }

                var savedCount = await EditView.SaveAsync();
                await OnEditorSaved.InvokeAsync(new RichTextDocumentEditorSaveRequest
                {
                    SavedChunkCount = savedCount,
                    Title = EditableEntityName
                });

                return true;
            }
            finally
            {
                IsSaving = false;
            }
        }
    }
}
