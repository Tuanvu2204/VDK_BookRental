using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace VDK_BookRental.Filters;

public sealed class AdminExceptionFilter : IAsyncExceptionFilter
{
    private readonly ILogger<AdminExceptionFilter> _logger;
    private readonly ITempDataDictionaryFactory _tempDataFactory;

    public AdminExceptionFilter(ILogger<AdminExceptionFilter> logger, ITempDataDictionaryFactory tempDataFactory)
    {
        _logger = logger;
        _tempDataFactory = tempDataFactory;
    }

    public Task OnExceptionAsync(ExceptionContext context)
    {
        try
        {
            var ex = context.Exception;

            // Log detailed information
            _logger.LogError(ex, "Unhandled exception in AdminController: {Message}", ex.Message);

            // Set a friendly message for the user via TempData
            try
            {
                var tempData = _tempDataFactory.GetTempData(context.HttpContext);
                tempData["ErrorMessage"] = "Đã xảy ra lỗi khi thực hiện thao tác quản trị. Vui lòng thử lại hoặc liên hệ quản trị hệ thống.";
            }
            catch (Exception tdEx)
            {
                _logger.LogWarning(tdEx, "Could not set TempData in AdminExceptionFilter.");
            }

            // Redirect to admin index to avoid leaving user on a failed endpoint
            context.Result = new RedirectToActionResult("Index", "Admin", null);
            context.ExceptionHandled = true;
        }
        catch (Exception logEx)
        {
            // If the filter itself fails, write to Console as a last resort
            try { Console.Error.WriteLine(logEx.ToString()); } catch { }
        }

        return Task.CompletedTask;
    }
}
