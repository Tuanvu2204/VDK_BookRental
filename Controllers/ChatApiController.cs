using Microsoft.AspNetCore.Mvc;
using VDK_BookRental.Core.AI;

namespace VDK_BookRental.Controllers;

[ApiController]
[Route("api/chat")]
public sealed class ChatApiController : ControllerBase
{
    private readonly IEnterpriseChatService _chatService;
    private readonly ILogger<ChatApiController> _logger;

    public ChatApiController(
        IEnterpriseChatService chatService,
        ILogger<ChatApiController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType(
        typeof(ChatResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ValidationProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> Post(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        var requestId =
            HttpContext.TraceIdentifier;

        _logger.LogInformation(
            "Nhận yêu cầu AI Chat. RequestId: {RequestId}",
            requestId);

        var response =
            await _chatService.AskAsync(
                request,
                requestId,
                cancellationToken);

        return Ok(response);
    }
}