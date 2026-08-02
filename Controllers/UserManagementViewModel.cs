namespace VDK_BookRental.ViewModels
{
    public class UserManagementPageViewModel
    {
        public string Search { get; set; } = string.Empty;
        public string RoleFilter { get; set; } = string.Empty;
        public string StatusFilter { get; set; } = string.Empty;
        public int CurrentUserId { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int LockedUsers { get; set; }
        public int CustomerUsers { get; set; }
        public int StaffUsers { get; set; }
        public int AdminUsers { get; set; }

        public List<UserManagementItemViewModel> Users { get; set; }
            = new List<UserManagementItemViewModel>();
    }

    public class UserManagementItemViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsLocked { get; set; }
        public int RentalCount { get; set; }
        public bool IsCurrentUser { get; set; }
    }
}