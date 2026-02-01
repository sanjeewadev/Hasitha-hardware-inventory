using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using System;
using System.Security.Authentication;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AuthenticationService _authService;
        private readonly SessionManager _sessionManager;

        private string _username = "";
        public string Username
        {
            get => _username;
            set { _username = value; OnPropertyChanged(); ErrorMessage = ""; } // Clear error when typing
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        private bool _isBusy; // To disable button while loading
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public ICommand LoginCommand { get; }
        public Action? CloseAction { get; set; }

        public LoginViewModel(AuthenticationService authService, SessionManager sessionManager)
        {
            _authService = authService;
            _sessionManager = sessionManager;

            LoginCommand = new RelayCommand<object>(async (p) => await ExecuteLogin(p));
        }

        private async Task ExecuteLogin(object? parameter)
        {
            if (IsBusy) return;

            var passwordBox = parameter as PasswordBox;
            string password = passwordBox?.Password ?? "";

            // 1. Validation Messages
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                ErrorMessage = "⚠️ Username and password are required.";
                return;
            }

            try
            {
                IsBusy = true; // Show loading state (optional visual)
                ErrorMessage = "Verifying credentials...";

                // 2. Attempt Login
                var user = await _authService.LoginAsync(Username, password);

                if (user != null)
                {
                    // Success
                    _sessionManager.Login(user);
                    CloseAction?.Invoke();
                }
                else
                {
                    // Security: Never say "User not found". Say "Invalid credentials".
                    ErrorMessage = "❌ Invalid username or password.";
                    passwordBox?.Clear();
                }
            }
            catch (AuthenticationException)
            {
                // 3. Handle Blocked User
                ErrorMessage = "⛔ Your account has been disabled.\nPlease contact the Administrator.";
            }
            catch (Exception ex)
            {
                // 4. Handle Database/System Errors
                ErrorMessage = $"❌ System Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}