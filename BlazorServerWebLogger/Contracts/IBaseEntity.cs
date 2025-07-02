namespace BlazorServerWebLogger.Contracts
{
    public interface IBaseEntity
    {
        Guid Id { get; set; }
        DateTime Timestamp { get; set; }
    }
}
