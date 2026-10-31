namespace ProductCatalog.Api.Contracts;

public sealed class PreconditionException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
