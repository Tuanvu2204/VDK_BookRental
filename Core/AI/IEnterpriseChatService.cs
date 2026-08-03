namespace VDK_BookRental.Core.AI;

public interface IEnterpriseChatService
{
    Task<ChatResponse> AskAsync(
        ChatRequest request,
        string requestId,
        CancellationToken cancellationToken);
}