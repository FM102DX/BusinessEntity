namespace BusinessEntity.Services;

public sealed class RichTextDocumentPanelMessage
{
    public Guid BusinessEntityId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public sealed class RichTextDocumentMessagePanelService
{
    private readonly List<RichTextDocumentPanelMessage> _messages = new();

    public event Action? Changed;

    public IReadOnlyList<RichTextDocumentPanelMessage> Messages => _messages;

    public void Clear()
    {
        if (_messages.Count == 0)
        {
            return;
        }

        _messages.Clear();
        Changed?.Invoke();
    }

    public void Add(Guid businessEntityId, string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _messages.Insert(0, new RichTextDocumentPanelMessage
        {
            BusinessEntityId = businessEntityId,
            Text = message.Trim(),
            CreatedAt = DateTime.Now
        });

        if (_messages.Count > 12)
        {
            _messages.RemoveRange(12, _messages.Count - 12);
        }

        Changed?.Invoke();
    }
}
