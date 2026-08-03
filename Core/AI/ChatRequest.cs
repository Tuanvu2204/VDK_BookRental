using System.ComponentModel.DataAnnotations;

namespace VDK_BookRental.Core.AI;

public sealed class ChatRequest
{
    [Required(ErrorMessage = "Vui lòng nhập nội dung cần hỏi.")]
    [StringLength(
        1200,
        MinimumLength = 1,
        ErrorMessage = "Nội dung phải có từ 1 đến 1200 ký tự.")]
    public string Message { get; set; } = string.Empty;

    public List<ChatHistoryItem> History { get; set; } = new();
}

public sealed class ChatHistoryItem
{
    [Required]
    [RegularExpression(
        "^(user|assistant)$",
        ErrorMessage = "Role chỉ được là user hoặc assistant.")]
    public string Role { get; set; } = "user";

    [Required]
    [StringLength(
        2000,
        ErrorMessage = "Nội dung lịch sử không được vượt quá 2000 ký tự.")]
    public string Content { get; set; } = string.Empty;
}