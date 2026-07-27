using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Models;

namespace VDK_BookRental.Data
{
    public static class StaffAccountSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

            const string defaultPassword = "1111";

            var accounts = new List<User>
            {
                new User
                {
                    FullName = "Nguyễn Văn Tèo",
                    UserName = "admin",
                    Email = "nguyenhuynhtuanvu2309@gmail.com",
                    Phone = "0817857316",
                    Role = "Admin"
                },

                new User
                {
                    FullName = "Huỳnh Lê Trung Kiên",
                    UserName = "trungkien1",
                    Email = "trungkien1@vdkrental.local",
                    Phone = "0900000001",
                    Role = "Staff"
                },

                new User
                {
                    FullName = "Phan Nguyễn Hoàng Duy",
                    UserName = "hoangduy1",
                    Email = "hoangduy1@vdkrental.local",
                    Phone = "0900000002",
                    Role = "Staff"
                },

                new User
                {
                    FullName = "Phan Tiểu Vy",
                    UserName = "tieuvy1",
                    Email = "tieuvy1@vdkrental.local",
                    Phone = "0900000003",
                    Role = "Staff"
                }
            };

            foreach (var account in accounts)
            {
                var existingUser = await context.Users
                    .FirstOrDefaultAsync(u =>
                        u.UserName == account.UserName ||
                        u.Email == account.Email);

                if (existingUser == null)
                {
                    account.PasswordHash =
                        BCrypt.Net.BCrypt.HashPassword(defaultPassword);

                    account.IsLocked = false;
                    account.CreatedAt = DateTime.Now;

                    context.Users.Add(account);
                }
                else
                {
                    existingUser.FullName = account.FullName;
                    existingUser.UserName = account.UserName;
                    existingUser.Email = account.Email;
                    existingUser.Phone = account.Phone;
                    existingUser.Role = account.Role;
                    existingUser.IsLocked = false;

                    // Đặt lại mật khẩu thành 1111
                    existingUser.PasswordHash =
                        BCrypt.Net.BCrypt.HashPassword(defaultPassword);
                }
            }

            await context.SaveChangesAsync();
        }
    }
}