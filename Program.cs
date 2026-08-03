using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VDK_BookRental.Core.AI;
using VDK_BookRental.Data;
using VDK_BookRental.Infrastructure.AI;
using VDK_BookRental.Infrastructure.Errors;

var builder = WebApplication.CreateBuilder(args);

// =============================================================
// CẤU HÌNH CHUNG
// =============================================================

const long maximumUploadSize = 25L * 1024 * 1024;

// =============================================================
// MVC + XỬ LÝ LỖI TOÀN CỤC
// =============================================================

builder.Services.AddControllersWithViews();

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// =============================================================
// DATABASE
//
// Không tự động tạo, xóa hoặc cập nhật database.
// Không dùng EnsureCreated, EnsureDeleted hoặc Migrate tự động.
// =============================================================

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

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

                sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors();
        }
    });

// =============================================================
// CACHE
//
// MemoryCache được chatbot dùng để lưu tạm danh sách sách.
// DistributedMemoryCache được Session sử dụng.
// =============================================================

builder.Services.AddMemoryCache();

builder.Services.AddDistributedMemoryCache();

// =============================================================
// SESSION
// =============================================================

builder.Services.AddSession(
    options =>
    {
        options.IdleTimeout = TimeSpan.FromHours(2);

        options.Cookie.Name = ".VDKBookRental.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.Cookie.SecurePolicy =
            CookieSecurePolicy.SameAsRequest;
    });

// =============================================================
// CẤU HÌNH GEMINI
//
// Model, BaseUrl và thông số lấy từ appsettings.json.
// ApiKey lấy từ User Secrets.
// =============================================================

builder.Services
    .AddOptions<GeminiOptions>()
    .Bind(
        builder.Configuration.GetSection(
            GeminiOptions.SectionName))
    .ValidateDataAnnotations();

// =============================================================
// GEMINI HTTP CLIENT
// =============================================================

builder.Services.AddHttpClient<
    IEnterpriseChatService,
    GeminiEnterpriseChatService>(
    (serviceProvider, httpClient) =>
    {
        var geminiOptions =
            serviceProvider
                .GetRequiredService<IOptions<GeminiOptions>>()
                .Value;

        if (string.IsNullOrWhiteSpace(geminiOptions.BaseUrl))
        {
            throw new InvalidOperationException(
                "Gemini:BaseUrl chưa được cấu hình.");
        }

        var normalizedBaseUrl =
            geminiOptions.BaseUrl.TrimEnd('/') + "/";

        if (!Uri.TryCreate(
                normalizedBaseUrl,
                UriKind.Absolute,
                out var baseAddress))
        {
            throw new InvalidOperationException(
                "Gemini:BaseUrl không phải địa chỉ hợp lệ.");
        }

        httpClient.BaseAddress = baseAddress;

        httpClient.Timeout =
            TimeSpan.FromSeconds(
                geminiOptions.TimeoutSeconds);

        httpClient.DefaultRequestHeaders.Accept.Clear();

        httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/json"));

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
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

var app = builder.Build();

// =============================================================
// HIỂN THỊ MODEL GEMINI ĐANG ĐƯỢC CẤU HÌNH
//
// Không ghi API key ra log.
// =============================================================

var configuredGeminiModel =
    app.Configuration[
        $"{GeminiOptions.SectionName}:Model"];

var configuredGeminiBaseUrl =
    app.Configuration[
        $"{GeminiOptions.SectionName}:BaseUrl"];

app.Logger.LogInformation(
    "Gemini đang sử dụng Model: {Model}; BaseUrl: {BaseUrl}",
    configuredGeminiModel,
    configuredGeminiBaseUrl);

// =============================================================
// XỬ LÝ LỖI
// =============================================================

// GlobalExceptionHandler xử lý lỗi API và trả ProblemDetails JSON.
app.UseExceptionHandler();

app.UseStatusCodePagesWithReExecute(
    "/Error/StatusCode",
    "?code={0}");

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// =============================================================
// HTTP PIPELINE
// =============================================================

app.UseStaticFiles();

app.UseRouting();

// Session phải chạy trước controller.
app.UseSession();

app.UseAuthorization();

// =============================================================
// ROUTES
// =============================================================

// Hỗ trợ Attribute Routing, ví dụ:
// POST /api/chat
app.MapControllers();

// Route mặc định cho các controller MVC.
app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Books}/{action=Index}/{id?}");

// =============================================================
// RUN
// =============================================================

app.Run();