using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using InventorySystem.Data.Repositories;
using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class UsersViewModel : ViewModelBase
    {
        private readonly IUserRepository _userRepo;
        private readonly AuthenticationService _authService;

        public ObservableCollection<User> Users { get; } = new();

        // --- Editor State ---
        private string _editorTitle = "Create New User";
        public string EditorTitle { get => _editorTitle; set { _editorTitle = value; OnPropertyChanged(); } }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); } }

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
        public UserRole SelectedRole { get => _selectedRole; set { _selectedRole = value; OnPropertyChanged(); } }

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

            LoadUsers();
        }

        private async void LoadUsers()
        {
            Users.Clear();
            var list = await _userRepo.GetAllAsync();
            // Sort by active status so blocked users go to bottom, or just by ID
            foreach (var user in list.OrderBy(u => u.Username))
            {
                Users.Add(user);
            }
        }

        private void ResetForm()
        {
            _editingId = 0;
            FullName = "";
            Username = "";
            Password = "";
            SelectedRole = UserRole.Employee;

            EditorTitle = "Create New User";
            StatusMessage = "";
            IsEditing = false;
        }

        private void OpenEdit(User user)
        {
            if (user == null) return;

            _editingId = user.Id;
            FullName = user.FullName;
            Username = user.Username;
            SelectedRole = user.Role;
            Password = ""; // Reset password field

            EditorTitle = $"Edit User: {user.Username}";
            StatusMessage = "";
            IsEditing = true;
        }

        private async Task SaveUser()
        {
            StatusMessage = "";
            IsErrorMessage = false;

            // Basic Validation
            if (string.IsNullOrWhiteSpace(FullName))
            {
                StatusMessage = "⚠️ Full Name is required.";
                IsErrorMessage = true;
                return;
            }
            if (string.IsNullOrWhiteSpace(Username))
            {
                StatusMessage = "⚠️ Username is required.";
                IsErrorMessage = true;
                return;
            }
            if (_editingId == 0 && string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "⚠️ Password is required for new users.";
                IsErrorMessage = true;
                return;
            }

            try
            {
                if (_editingId == 0) // ADD NEW
                {
                    if (Users.Any(u => u.Username.Equals(Username, StringComparison.OrdinalIgnoreCase)))
                    {
                        StatusMessage = "⚠️ Username already taken.";
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
                        CreatedAt = DateTime.Now
                    };

                    await _userRepo.AddAsync(newUser);
                    StatusMessage = "✅ User created successfully!";

                    await Task.Delay(1000);
                    ResetForm();
                    LoadUsers();
                }
                else // UPDATE EXISTING
                {
                    // Find existing tracked object
                    var userToUpdate = Users.FirstOrDefault(u => u.Id == _editingId);
                    if (userToUpdate != null)
                    {
                        userToUpdate.FullName = FullName;
                        userToUpdate.Role = SelectedRole;
                        userToUpdate.Username = Username;

                        if (!string.IsNullOrWhiteSpace(Password))
                        {
                            userToUpdate.PasswordHash = _authService.HashPassword(Password);
                        }

                        await _userRepo.UpdateAsync(userToUpdate);
                        StatusMessage = "✅ User updated successfully!";
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

            // Toggle
            user.IsActive = !user.IsActive;

            // Save to DB
            await _userRepo.UpdateAsync(user);

            // RELOAD LIST to update the UI Button Color
            LoadUsers();
        }
    }
}