using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace VDK_BookRental.Controllers
{
    [Route("Error")]
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController>
            _logger;

        public ErrorController(
            ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        // =====================================================
        // LỖI 500
        // URL: /Error/ServerError
        // =====================================================

        [HttpGet("ServerError")]
        [HttpPost("ServerError")]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult ServerError()
        {
            var exceptionFeature =
                HttpContext.Features
                    .Get<IExceptionHandlerPathFeature>();

            if (exceptionFeature?.Error != null)
            {
                _logger.LogError(
                    exceptionFeature.Error,
                    "Lỗi hệ thống tại đường dẫn {Path}.",
                    exceptionFeature.Path);
            }

            Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            ViewBag.RequestPath =
                exceptionFeature?.Path ??
                HttpContext.Request.Path.Value ??
                "/";

            return View(
                "~/Views/Error/ServerError.cshtml");
        }

        // =====================================================
        // LỖI 404, 403...
        // URL: /Error/StatusCode?code=404
        // =====================================================

        [HttpGet("StatusCode")]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult HttpStatus(int code)
        {
            Response.StatusCode = code;

            var statusFeature =
                HttpContext.Features
                    .Get<IStatusCodeReExecuteFeature>();

            ViewBag.StatusCode = code;

            ViewBag.OriginalPath =
                statusFeature?.OriginalPath
                ?? HttpContext.Request.Path.Value
                ?? "/";

            ViewBag.Title =
                code switch
                {
                    400 => "Yêu cầu không hợp lệ",
                    401 => "Bạn chưa đăng nhập",
                    403 => "Bạn không có quyền truy cập",
                    404 => "Không tìm thấy trang",
                    405 => "Phương thức không được hỗ trợ",
                    _ => "Không thể xử lý yêu cầu"
                };

            ViewBag.Message =
                code switch
                {
                    400 =>
                        "Dữ liệu gửi lên không hợp lệ.",

                    401 =>
                        "Vui lòng đăng nhập để tiếp tục.",

                    403 =>
                        "Tài khoản không có quyền truy cập chức năng này.",

                    404 =>
                        "Đường dẫn bạn truy cập không tồn tại.",

                    405 =>
                        "Trang không hỗ trợ phương thức gửi hiện tại.",

                    _ =>
                        "Hệ thống không thể xử lý yêu cầu."
                };

            return View(
                "~/Views/Error/StatusCode.cshtml");
        }
    }
}