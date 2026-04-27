using BusinessEntity.Core.RichText;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace BusinessEntity.Components
{
    public partial class RichTextDocumentView : ComponentBase
    {
        [Parameter] public string EntityName { get; set; } = string.Empty;
        [Parameter] public string HtmlContent { get; set; } = string.Empty;
        [Parameter] public bool IsBusy { get; set; }
        [Parameter] public string? StatusMessage { get; set; }
        [Parameter] public EventCallback<InputFileChangeEventArgs> OnImportSelected { get; set; }

        // Пробрасывает событие выбора файла наружу, в page-level orchestration.
        private Task HandleImportSelected(InputFileChangeEventArgs args)
        {
            return OnImportSelected.InvokeAsync(args);
        }
    }
}
