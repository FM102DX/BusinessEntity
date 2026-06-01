using BusinessEntity.MiniApps.ActivityMiniApp.Contracts;
using Microsoft.AspNetCore.Components;

namespace BusinessEntity.MiniApps.ActivityMiniApp.Components;

/// <summary>
/// Отображает один существующий комментарий и inline-форму ответа.
/// </summary>
public partial class BusinessEntityCommentItem : ComponentBase
{
    [Parameter] public BusinessEntityCommentRecord Record { get; set; } = new();
    [Parameter] public bool CanReply { get; set; } = true;
    [Parameter] public EventCallback OnReplySubmitted { get; set; }

    private bool IsReplying { get; set; }
    private string IndentStyle => $"margin-left: {Math.Clamp(Record.DisplayDepth, 0, 3) * 1.25:0.##}rem;";
    private string FormattedDate => Record.CreatedDate.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

    // Переключает видимость inline-редактора ответа.
    private void ToggleReply()
    {
        IsReplying = !IsReplying;
    }

    // Закрывает редактор ответа и сообщает родителю, что список нужно перечитать.
    private async Task HandleReplySubmittedAsync()
    {
        IsReplying = false;
        await OnReplySubmitted.InvokeAsync();
    }
}
