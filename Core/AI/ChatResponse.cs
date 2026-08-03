namespace VDK_BookRental.Core.AI;

public sealed class ChatResponse
{
    public string Reply { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;

    public DateTime GeneratedAtUtc { get; set; }
}