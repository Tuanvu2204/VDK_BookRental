namespace VDK_BookRental.Infrastructure.AI;

public sealed class GeminiServiceException : Exception
{
    public GeminiServiceException()
    {
    }

    public GeminiServiceException(string message)
        : base(message)
    {
    }

    public GeminiServiceException(
        string message,
        Exception innerException)
        : base(message, innerException)
    {
    }
}