using VDK_BookRental.Core.AI;

namespace VDK_BookRental.Infrastructure.AI;

public sealed class NoopEnterpriseChatService : IEnterpriseChatService
{
    public Task<ChatResponse> AskAsync(
        ChatRequest request,
        string requestId,
        CancellationToken cancellationToken)
    {
        throw new GeminiServiceException(
            "AI service is not configured. Please set Gemini options in configuration.");
    }
}
