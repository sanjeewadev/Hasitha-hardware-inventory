using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class PermissionItem : ViewModelBase
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        private bool _isChecked;
        public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged(); } }
    }

    public class UsersViewModel : ViewModelBase
    {
        private readonly IUserRepository _userRepo;
        private readonly AuthenticationService _authService;

        public ObservableCollection<User> Users { get; } = new();

        // --- Permissions UI ---
        public ObservableCollection<PermissionItem> AvailablePermissions { get; } = new();
        public bool IsEmployeeSelected => SelectedRole == UserRole.Employee;

        // --- Editor State ---
        private string _editorTitle = "Create New User";
        public string EditorTitle { get => _editorTitle; set { _editorTitle = value; OnPropertyChanged(); } }

        // --- Notifications ---
        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        private bool _isErrorMessage;
        public bool IsErrorMessage { get => _isErrorMessage; set { _isErrorMessage = value; OnPropertyChanged(); } }

        // --- Form Inputs ---
        private int _editingId;
        private string _fullName = "";
        public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); } }

        private string _username = "";
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }

        private string _password = "";
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        private UserRole _selectedRole = UserRole.Employee;
        public UserRole SelectedRole
        {
            get => _selectedRole;
            set
            {
                _selectedRole = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEmployeeSelected));
            }
        }

        public ObservableCollection<UserRole> Roles { get; } = new() { UserRole.Admin, UserRole.Employee };

        // --- Commands ---
        public ICommand EditUserCommand { get; }
        public ICommand ToggleActiveCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ResetCommand { get; }

        public UsersViewModel(IUserRepository userRepo, AuthenticationService authService)
        {
            _userRepo = userRepo;
            _authService = authService;

            EditUserCommand = new RelayCommand<User>(OpenEdit);
            ToggleActiveCommand = new RelayCommand<User>(async (u) => await ToggleActive(u));
            SaveCommand = new RelayCommand(async () => await SaveUser());
            ResetCommand = new RelayCommand(ResetForm);

            InitializePermissions();
            LoadUsers();
        }

        private void InitializePermissions()
        {
            // FIX: Expanded to match your actual Left Navigation Bar
            AvailablePermissions.Add(new PermissionItem { Name = "POS", DisplayName = "🛒 Point of Sale" });
            AvailablePermissions.Add(new PermissionItem { Name = "TodaySales", DisplayName = "🎯 Today's Sales" });
            AvailablePermissions.Add(new PermissionItem { Name = "Credit", DisplayName = "💳 Credit / Debtors" });
            AvailablePermissions.Add(new PermissionItem { Name = "Returns", DisplayName = "↩️ Sales Returns" });
            AvailablePermissions.Add(new PermissionItem { Name = "Suppliers", DisplayName = "🚛 Suppliers & Stock In" });
            AvailablePermissions.Add(new PermissionItem { Name = "StockAdjust", DisplayName = "⚖️ Adjust (Remove) Stock" });
            AvailablePermissions.Add(new PermissionItem { Name = "Products", DisplayName = "📦 View/Create Products" });
            AvailablePermissions.Add(new PermissionItem { Name = "Reports", DisplayName = "📊 My Report & History" });
        }

        private async void LoadUsers()
        {
            Users.Clear();
            var list = await _userRepo.GetAllAsync();
            foreach (var user in list.OrderBy(u => u.Username)) Users.Add(user);
        }

        private void ResetForm()
        {
            _editingId = 0;
            FullName = "";
            Username = "";
            Password = "";
            SelectedRole = UserRole.Employee;

            foreach (var p in AvailablePermissions) p.IsChecked = false;

            EditorTitle = "Create New User";
            StatusMessage = "";
        }

        private void OpenEdit(User user)
        {
            if (user == null) return;

            _editingId = user.Id;
            FullName = user.FullName;
            Username = user.Username;
            SelectedRole = user.Role;
            Password = "";

            var userPerms = (user.Permissions ?? "").Split(',');
            foreach (var p in AvailablePermissions)
            {
                p.IsChecked = userPerms.Contains(p.Name);
            }

            EditorTitle = $"Edit User: {user.Username}";
            StatusMessage = "";
        }

        private async Task SaveUser()
        {
            StatusMessage = "";
            IsErrorMessage = false;

            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Username))
            {
                StatusMessage = "⚠️ Missing fields.";
                IsErrorMessage = true;
                return;
            }

            string permString = "";
            if (SelectedRole == UserRole.Admin)
            {
                permString = "ALL";
            }
            else
            {
                var active = AvailablePermissions.Where(p => p.IsChecked).Select(p => p.Name);
                permString = string.Join(",", active);
            }

            try
            {
                if (_editingId == 0) // CREATE
                {
                    if (Users.Any(u => u.Username.Equals(Username, StringComparison.OrdinalIgnoreCase)))
                    {
                        StatusMessage = "⚠️ Username taken.";
                        IsErrorMessage = true;
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(Password))
                    {
                        StatusMessage = "⚠️ Password required.";
                        IsErrorMessage = true;
                        return;
                    }

                    var newUser = new User
                    {
                        Username = Username,
                        PasswordHash = _authService.HashPassword(Password),
                        FullName = FullName,
                        Role = SelectedRole,
                        IsActive = true,
                        Permissions = permString,
                        CreatedAt = DateTime.Now
                    };

                    await _userRepo.AddAsync(newUser);
                    StatusMessage = "✅ User created!";
                    await Task.Delay(1000);
                    ResetForm();
                    LoadUsers();
                }
                else // UPDATE
                {
                    var userToUpdate = Users.FirstOrDefault(u => u.Id == _editingId);
                    if (userToUpdate != null)
                    {
                        userToUpdate.FullName = FullName;
                        userToUpdate.Role = SelectedRole;
                        userToUpdate.Username = Username;
                        userToUpdate.Permissions = permString;

                        if (!string.IsNullOrWhiteSpace(Password))
                            userToUpdate.PasswordHash = _authService.HashPassword(Password);

                        await _userRepo.UpdateAsync(userToUpdate);

                        // FIX: Ensure form resets and grid updates after edit
                        StatusMessage = "✅ User updated!";
                        await Task.Delay(1000);
                        ResetForm();
                        LoadUsers();
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error: {ex.Message}";
                IsErrorMessage = true;
            }
        }

        private async Task ToggleActive(User user)
        {
            if (user == null) return;

            // Prevent the Admin from blocking themselves
            if (user.Role == UserRole.Admin && user.Id == _editingId)
            {
                MessageBox.Show("You cannot block your own admin account.", "Security Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            user.IsActive = !user.IsActive;
            await _userRepo.UpdateAsync(user);
            LoadUsers(); // Refresh grid to show status change
        }
    }
}