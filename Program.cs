using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VDK_BookRental.Core.AI;
using VDK_BookRental.Data;
using VDK_BookRental.Infrastructure.AI;
using VDK_BookRental.Infrastructure.Errors;

var builder = WebApplication.CreateBuilder(args);

// =============================================================
// CẤU HÌNH CHUNG
// =============================================================

const long maximumUploadSize =
    25L * 1024 * 1024;

// =============================================================
// MVC + GLOBAL EXCEPTION HANDLER
// =============================================================

builder.Services.AddControllersWithViews();

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<
    GlobalExceptionHandler>();

// =============================================================
// DATABASE
//
// Không tự động:
// - EnsureCreated
// - EnsureDeleted
// - Migrate
//
// Vì vậy không thay đổi database khi khởi động.
// =============================================================

var connectionString =
    builder.Configuration.GetConnectionString(
        "DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "Không tìm thấy ConnectionStrings:DefaultConnection " +
        "trong appsettings.json.");
}

builder.Services.AddDbContext<AppDbContext>(
    options =>
    {
        options.UseSqlServer(
            connectionString,
            sqlServerOptions =>
            {
                sqlServerOptions.CommandTimeout(60);
            });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors();
        }
    });

// =============================================================
// CACHE
//
// AddMemoryCache:
// Dịch vụ AI dùng để cache danh mục sách.
//
// AddDistributedMemoryCache:
// Session sử dụng.
// =============================================================

builder.Services.AddMemoryCache();

builder.Services.AddDistributedMemoryCache();

// =============================================================
// SESSION
// =============================================================

builder.Services.AddSession(
    options =>
    {
        options.IdleTimeout =
            TimeSpan.FromHours(2);

        options.Cookie.Name =
            ".VDKBookRental.Session";

        options.Cookie.HttpOnly =
            true;

        options.Cookie.IsEssential =
            true;

        options.Cookie.SameSite =
            SameSiteMode.Lax;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;
    });

// =============================================================
// GEMINI OPTIONS
//
// ApiKey:
// - Đọc từ User Secrets.
//
// Model, BaseUrl, TimeoutSeconds...:
// - Đọc từ appsettings.json.
// =============================================================

builder.Services
    .AddOptions<GeminiOptions>()
    .Bind(
        builder.Configuration.GetSection(
            GeminiOptions.SectionName))
    .ValidateDataAnnotations();

// =============================================================
// GEMINI TYPED HTTP CLIENT
//
// Tách AddHttpClient và ConfigureHttpClient để tránh
// nhầm overload Func<HttpClient, IServiceProvider, TImplementation>.
// =============================================================

builder.Services
    .AddHttpClient<
        IEnterpriseChatService,
        GeminiEnterpriseChatService>()
    .ConfigureHttpClient(
        (
            IServiceProvider serviceProvider,
            HttpClient httpClient
        ) =>
        {
            var geminiOptions =
                serviceProvider
                    .GetRequiredService<
                        IOptions<GeminiOptions>>()
                    .Value;

            if (string.IsNullOrWhiteSpace(
                    geminiOptions.BaseUrl))
            {
                throw new InvalidOperationException(
                    "Gemini:BaseUrl chưa được cấu hình.");
            }

            var normalizedBaseUrl =
                geminiOptions.BaseUrl
                    .Trim()
                    .TrimEnd('/') + "/";

            if (!Uri.TryCreate(
                    normalizedBaseUrl,
                    UriKind.Absolute,
                    out var baseAddress))
            {
                throw new InvalidOperationException(
                    "Gemini:BaseUrl không phải URL hợp lệ.");
            }

            if (geminiOptions.TimeoutSeconds < 5)
            {
                throw new InvalidOperationException(
                    "Gemini:TimeoutSeconds phải từ 5 giây trở lên.");
            }

            httpClient.BaseAddress =
                baseAddress;

            httpClient.Timeout =
                TimeSpan.FromSeconds(
                    geminiOptions.TimeoutSeconds);

            httpClient.DefaultRequestHeaders
                .Accept
                .Clear();

            httpClient.DefaultRequestHeaders
                .Accept
                .Add(
                    new MediaTypeWithQualityHeaderValue(
                        "application/json"));

            httpClient.DefaultRequestHeaders
                .UserAgent
                .Clear();

            httpClient.DefaultRequestHeaders
                .UserAgent
                .ParseAdd(
                    "VDK-BookRental-AI/1.0");
        });

// =============================================================
// UPLOAD FILE
// =============================================================

builder.Services.Configure<FormOptions>(
    options =>
    {
        options.MultipartBodyLengthLimit =
            maximumUploadSize;

        options.ValueLengthLimit =
            1024 * 1024;

        options.MultipartHeadersLengthLimit =
            64 * 1024;

        options.MultipartBoundaryLengthLimit =
            256;
    });

builder.WebHost.ConfigureKestrel(
    options =>
    {
        options.Limits.MaxRequestBodySize =
            maximumUploadSize;

        options.Limits.RequestHeadersTimeout =
            TimeSpan.FromSeconds(60);

        options.Limits.KeepAliveTimeout =
            TimeSpan.FromMinutes(2);
    });

// =============================================================
// BUILD
// =============================================================

var app =
    builder.Build();

// =============================================================
// GLOBAL EXCEPTION HANDLING
//
// GlobalExceptionHandler:
// - API trả JSON ProblemDetails.
// - MVC chuyển sang trang lỗi.
// =============================================================

app.UseExceptionHandler();

app.UseStatusCodePagesWithReExecute(
    "/Error/StatusCode",
    "?code={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// =============================================================
// HTTP PIPELINE
// =============================================================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// Session phải chạy trước controller.
app.UseSession();

app.UseAuthorization();

// =============================================================
// ROUTES
// =============================================================

// Attribute Routing:
// POST /api/chat
app.MapControllers();

// Conventional MVC Routing.
app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Books}/{action=Index}/{id?}");

// =============================================================
// RUN
// =============================================================

app.Run();