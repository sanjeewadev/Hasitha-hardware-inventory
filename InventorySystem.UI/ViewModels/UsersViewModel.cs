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
    // Helper class for the Checkboxes
    public class PermissionItem : ViewModelBase
    {
        public string Name { get; set; } // "Dashboard", "POS", etc.
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
                OnPropertyChanged(nameof(IsEmployeeSelected)); // Show/Hide checkboxes
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
            // Define all the pages you want to control
            AvailablePermissions.Add(new PermissionItem { Name = "Dashboard", DisplayName = "📊 Dashboard / Analytics" });
            AvailablePermissions.Add(new PermissionItem { Name = "POS", DisplayName = "🛒 Point of Sale" });
            AvailablePermissions.Add(new PermissionItem { Name = "Catalog", DisplayName = "📦 Inventory Catalog" });
            AvailablePermissions.Add(new PermissionItem { Name = "Stock", DisplayName = "⚖️ Stock Adjustments" });
            AvailablePermissions.Add(new PermissionItem { Name = "Suppliers", DisplayName = "🚛 Supplier Management" });
            AvailablePermissions.Add(new PermissionItem { Name = "Settings", DisplayName = "⚙️ Settings & Backups" });
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

            // Reset Checkboxes
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

            // Load Permissions from DB string (e.g. "POS,Stock")
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

            // Build Permission String
            string permString = "";
            if (SelectedRole == UserRole.Admin)
            {
                permString = "ALL"; // Admins get everything
            }
            else
            {
                var active = AvailablePermissions.Where(p => p.IsChecked).Select(p => p.Name);
                permString = string.Join(",", active);
            }

            try
            {
                if (_editingId == 0) // NEW
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
                        Permissions = permString, // <--- SAVE PERMISSIONS
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
                        userToUpdate.Permissions = permString; // <--- UPDATE PERMISSIONS

                        if (!string.IsNullOrWhiteSpace(Password))
                            userToUpdate.PasswordHash = _authService.HashPassword(Password);

                        await _userRepo.UpdateAsync(userToUpdate);
                        StatusMessage = "✅ User updated!";
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
            user.IsActive = !user.IsActive;
            await _userRepo.UpdateAsync(user);
            LoadUsers();
        }
    }
}