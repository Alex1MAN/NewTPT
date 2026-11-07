namespace JWTAuthTemplate.Application.Exceptions;

/// <summary>Конфликт состояния (HTTP 409): дубликат, повторная операция, несовпадение данных.</summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
