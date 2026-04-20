namespace BusinessEntity.DataAccess.Classes;

/// <summary>
/// Простейший результат операции, не зависящий от внешних пакетов.
/// </summary>
public record CommonOperationResult(bool Success, string Message)
{
    public static CommonOperationResult Ok(string message = "") => new(true, message);
    public static CommonOperationResult Fail(string message = "") => new(false, message);
} 