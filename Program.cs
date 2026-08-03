using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;

var builder = WebApplication.CreateBuilder(args);

// =============================================================
// CẤU HÌNH CHUNG
// =============================================================

const long maximumUploadSize = 25L * 1024 * 1024;

// =============================================================
// MVC
// =============================================================

builder.Services.AddControllersWithViews();

// =============================================================
// DATABASE
// Không tự động tạo, xóa hoặc cập nhật database.
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
            });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableDetailedErrors();
        }
    });

// =============================================================
// SESSION
// =============================================================

builder.Services.AddDistributedMemoryCache();

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
// XỬ LÝ LỖI
// =============================================================

app.UseExceptionHandler("/Error/ServerError");

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

app.UseSession();

app.UseAuthorization();

// =============================================================
// ROUTE
// =============================================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Books}/{action=Index}/{id?}");

// =============================================================
// RUN
// =============================================================

app.Run();