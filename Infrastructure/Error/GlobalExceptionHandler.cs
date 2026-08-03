using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using VDK_BookRental.Infrastructure.AI;

namespace VDK_BookRental.Infrastructure.Errors;

public sealed class GlobalExceptionHandler
    : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId =
            httpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Unhandled exception. TraceId: {TraceId}. " +
            "Path: {Path}",
            traceId,
            httpContext.Request.Path);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        if (!httpContext.Request.Path.StartsWithSegments(
                "/api"))
        {
            if (httpContext.Request.Path.StartsWithSegments(
                    "/Error"))
            {
                httpContext.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                httpContext.Response.ContentType =
                    "text/plain; charset=utf-8";

                await httpContext.Response.WriteAsync(
                    $"Đã xảy ra lỗi hệ thống. Mã lỗi: {traceId}",
                    cancellationToken);

                return true;
            }

            httpContext.Response.Redirect(
                "/Error/ServerError");

            return true;
        }

        var statusCode =
            exception switch
            {
                ArgumentException =>
                    StatusCodes.Status400BadRequest,

                GeminiServiceException =>
                    StatusCodes.Status503ServiceUnavailable,

                _ =>
                    StatusCodes.Status500InternalServerError
            };

        var title =
            statusCode switch
            {
                StatusCodes.Status400BadRequest =>
                    "Yêu cầu không hợp lệ",

                StatusCodes.Status503ServiceUnavailable =>
                    "Dịch vụ AI tạm thời không khả dụng",

                _ =>
                    "Đã xảy ra lỗi hệ thống"
            };

        var detail =
            exception switch
            {
                ArgumentException =>
                    exception.Message,

                GeminiServiceException =>
                    exception.Message,

                _ =>
                    "Hệ thống không thể xử lý yêu cầu. " +
                    "Vui lòng thử lại sau."
            };

        httpContext.Response.StatusCode =
            statusCode;

        httpContext.Response.ContentType =
            "application/problem+json";

        var problemDetails =
            new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance =
                    httpContext.Request.Path
            };

        problemDetails.Extensions["traceId"] =
            traceId;

        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);

        return true;
    }
}