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

// Nếu project của bạn có GlobalExceptionHandler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// =============================================================
// DATABASE
//
// Không tự động tạo/xóa/update database.
// Migration thực hiện thủ công bằng Package Manager Console.
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
// =============================================================

// Chatbot / dữ liệu tạm
builder.Services.AddMemoryCache();

// Session sử dụng distributed memory cache
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
// GEMINI OPTIONS
// =============================================================

builder.Services
    .AddOptions<GeminiOptions>()
    .Bind(
        builder.Configuration.GetSection(
            GeminiOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

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
// BUILD APPLICATION
// =============================================================

var app = builder.Build();

// =============================================================
// LOG CẤU HÌNH GEMINI
//
// Chỉ log Model và BaseUrl.
// TUYỆT ĐỐI không log API Key.
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

app.UseExceptionHandler();

app.UseStatusCodePagesWithReExecute(
    "/Error/StatusCode",
    "?code={0}");

// =============================================================
// HTTPS
// =============================================================

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// =============================================================
// STATIC FILE
// =============================================================

app.UseStaticFiles();

// =============================================================
// ROUTING
// =============================================================

app.UseRouting();

// =============================================================
// SESSION
//
// Phải chạy sau Routing và trước Controller.
// =============================================================

app.UseSession();

// =============================================================
// AUTHORIZATION
// =============================================================

app.UseAuthorization();

// =============================================================
// ROUTES
// =============================================================

// -------------------------------------------------------------
// 1. ATTRIBUTE ROUTING
//
// Ví dụ:
// POST /api/chat
// -------------------------------------------------------------

app.MapControllers();

// -------------------------------------------------------------
// 2. ADMIN AREA
//
// Ví dụ:
// /Admin
// /Admin/Books
// /Admin/Books/Create
// /Admin/Books/Edit/5
//
// PHẢI đặt Area route trước Default route.
// -------------------------------------------------------------

app.MapControllerRoute(
    name: "areas",
    pattern:
        "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// -------------------------------------------------------------
// 3. MVC DEFAULT
//
// Trang mặc định:
// /Books
// -------------------------------------------------------------

app.MapControllerRoute(
    name: "default",
    pattern:
        "{controller=Books}/{action=Index}/{id?}");

// =============================================================
// RUN
// =============================================================

app.Run();