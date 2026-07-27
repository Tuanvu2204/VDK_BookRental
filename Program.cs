using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;

var builder = WebApplication.CreateBuilder(args);

// =========================
// ĐĂNG KÝ DỊCH VỤ
// =========================

// MVC
builder.Services.AddControllersWithViews();

// Kết nối SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".VDKBookRental.Session";
});

var app = builder.Build();

// =========================
// CẤU HÌNH HTTP PIPELINE
// =========================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session phải đặt trước Authorization
app.UseSession();

app.UseAuthorization();

// =========================
// ROUTING
// =========================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// =========================
// TẠO/CẬP NHẬT TÀI KHOẢN
// =========================

// Chạy Seeder khi ứng dụng khởi động.
// Seeder sẽ tạo hoặc cập nhật tài khoản Admin và Staff,
// đồng thời đặt mật khẩu mặc định thành 1111.
await StaffAccountSeeder.SeedAsync(app.Services);

app.Run();