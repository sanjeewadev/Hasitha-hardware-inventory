using InventorySystem.Core.Entities;
using InventorySystem.Core.Enums;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InventorySystem.Infrastructure.Services
{
    // This class implements INotifyPropertyChanged so the UI can update automatically
    // when the user logs in or out.
    public class SessionManager : INotifyPropertyChanged
    {
        private static SessionManager? _instance;
        public static SessionManager Instance => _instance ??= new SessionManager();

        private User? _currentUser;

        // The currently logged-in user
        public User? CurrentUser
        {
            get => _currentUser;
            private set
            {
                _currentUser = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLoggedIn));
                OnPropertyChanged(nameof(IsAdmin));
                OnPropertyChanged(nameof(Username));
                OnPropertyChanged(nameof(UserRoleDisplay));
            }
        }

        // Helpers for UI Binding
        public bool IsLoggedIn => CurrentUser != null;

        public string Username => CurrentUser?.Username ?? "Guest";

        public string UserRoleDisplay => CurrentUser?.Role.ToString() ?? "";

        // Role Checks
        public bool IsAdmin => CurrentUser != null &&
                             (CurrentUser.Role == UserRole.Admin || CurrentUser.Role == UserRole.SuperAdmin);

        public bool IsSuperAdmin => CurrentUser?.Role == UserRole.SuperAdmin;

        // Methods
        public void Login(User user)
        {
            CurrentUser = user;
        }

        public void Logout()
        {
            CurrentUser = null;
        }

        // Implementation of INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}