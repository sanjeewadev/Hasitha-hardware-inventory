using InventorySystem.UI.Commands;
using Microsoft.Win32; // Needed for Save/Open Dialogs
using System;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        // Path to your live database
        private readonly string _dbPath;

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }

        public SettingsViewModel()
        {
            // Find the database file relative to where the .exe is running
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");

            BackupCommand = new RelayCommand(BackupData);
            RestoreCommand = new RelayCommand(RestoreData);
        }

        private void BackupData()
        {
            if (!File.Exists(_dbPath))
            {
                MessageBox.Show("Database not found! Run the app once to create it.");
                return;
            }

            // 1. Open "Save File" Dialog
            var dialog = new SaveFileDialog
            {
                FileName = $"InventoryBackup_{DateTime.Now:yyyy_MM_dd_HHmm}", // Auto-name: InventoryBackup_2025_01_28_1030
                DefaultExt = ".db",
                Filter = "Database Files (*.db)|*.db"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // 2. Copy the file
                    File.Copy(_dbPath, dialog.FileName, true);
                    MessageBox.Show("Backup Successful!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Backup Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RestoreData()
        {
            // 1. Warning!
            if (MessageBox.Show("WARNING: This will replace your current data with the backup file.\n\n" +
                                "All current data will be lost!\n\n" +
                                "Do you want to continue?",
                                "Confirm Restore",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning) == MessageBoxResult.No)
            {
                return;
            }

            // 2. Open "Select File" Dialog
            var dialog = new OpenFileDialog
            {
                Filter = "Database Files (*.db)|*.db"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // 3. Overwrite current DB with Backup
                    File.Copy(dialog.FileName, _dbPath, true);

                    MessageBox.Show("Restore Successful!\n\nThe application will now close to apply changes.\nPlease restart it.",
                                    "Restart Required",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);

                    // 4. Force Close (Required to reload the new DB connection properly)
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Restore Failed: {ex.Message}\nMake sure the app is not running elsewhere.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}