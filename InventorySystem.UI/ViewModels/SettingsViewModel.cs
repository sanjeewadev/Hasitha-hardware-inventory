using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace InventorySystem.UI.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private const string ConfigFile = "backup_config.txt";
        private const string CloudConfig = "cloud_history.txt";
        private const int MaxLocalBackups = 30; // Keep only the last 30 files

        private readonly BackupService _backupService;

        public ObservableCollection<BackupFile> Backups { get; } = new();

        private string _backupFolderPath = "";
        public string BackupFolderPath
        {
            get => _backupFolderPath;
            set { _backupFolderPath = value; OnPropertyChanged(); }
        }

        // Commands
        public ICommand BrowseFolderCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand TestCloudUploadCommand { get; }

        public SettingsViewModel()
        {
            var db = DatabaseService.CreateDbContext();
            _backupService = new BackupService(db);

            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            CreateBackupCommand = new RelayCommand(async () => await CreateBackup());
            RestoreCommand = new RelayCommand<BackupFile>(RestoreBackup);
            DeleteCommand = new RelayCommand<BackupFile>(DeleteBackup);
            TestCloudUploadCommand = new RelayCommand(async () => await TestCloudUpload());

            LoadSettings();
            RefreshList();
        }

        // --- 1. CONFIGURATION ---
        private void LoadSettings()
        {
            if (File.Exists(ConfigFile))
            {
                string savedPath = File.ReadAllText(ConfigFile).Trim();
                if (Directory.Exists(savedPath))
                {
                    BackupFolderPath = savedPath;
                    return;
                }
            }

            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InventoryBackups");
            if (!Directory.Exists(defaultPath)) Directory.CreateDirectory(defaultPath);
            BackupFolderPath = defaultPath;
            SaveSettings();
        }

        private void SaveSettings()
        {
            try { File.WriteAllText(ConfigFile, BackupFolderPath); } catch { }
        }

        // --- 2. LOCAL BACKUP ACTIONS ---
        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Backup Location",
                InitialDirectory = BackupFolderPath
            };

            if (dialog.ShowDialog() == true)
            {
                if (Directory.Exists(dialog.FolderName))
                {
                    BackupFolderPath = dialog.FolderName;
                    SaveSettings();
                    RefreshList();
                }
                else
                {
                    MessageBox.Show("The selected folder is invalid or inaccessible.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void RefreshList()
        {
            try
            {
                Backups.Clear();
                if (Directory.Exists(BackupFolderPath))
                {
                    // Sort by newest first
                    var files = _backupService.GetBackups(BackupFolderPath).OrderByDescending(f => f.FileName);
                    foreach (var f in files) Backups.Add(f);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load backups list.\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task CreateBackup()
        {
            try
            {
                await _backupService.CreateBackupAsync(BackupFolderPath);

                // Trigger Cleanup after creating new file
                PerformAutoCleanup();

                RefreshList();
                MessageBox.Show("Backup created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- NEW: AUTO CLEANUP LOGIC ---
        private void PerformAutoCleanup()
        {
            try
            {
                var allFiles = _backupService.GetBackups(BackupFolderPath)
                                             .OrderByDescending(f => f.FileName) // Assuming name contains date, or use creation time
                                             .ToList();

                if (allFiles.Count > MaxLocalBackups)
                {
                    // Identify files to remove (Skip the newest 30)
                    var filesToDelete = allFiles.Skip(MaxLocalBackups).ToList();

                    foreach (var file in filesToDelete)
                    {
                        try
                        {
                            _backupService.DeleteBackup(file.FullPath);
                        }
                        catch
                        {
                            // Ignored: If a file is locked, just skip it this time
                        }
                    }
                }
            }
            catch
            {
                // Silent fail for cleanup logic
            }
        }

        private void RestoreBackup(BackupFile file)
        {
            if (!File.Exists(file.FullPath))
            {
                MessageBox.Show("Backup file not found on disk. It may have been moved or deleted.", "File Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                RefreshList();
                return;
            }

            var result1 = MessageBox.Show(
                $"You are about to restore data from:\n'{file.FileName}'\n\nThis will OVERWRITE all current data. Continue?",
                "Confirm Restore (Step 1/2)", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result1 == MessageBoxResult.Yes)
            {
                var result2 = MessageBox.Show(
                    "⚠️ FINAL WARNING ⚠️\n\nThe application will RESTART immediately after restore.\nAny unsaved work will be lost.\n\nAre you absolutely sure?",
                    "Final Confirmation (Step 2/2)", MessageBoxButton.YesNo, MessageBoxImage.Error);

                if (result2 == MessageBoxResult.Yes)
                {
                    try
                    {
                        _backupService.RestoreBackup(file.FullPath);
                        MessageBox.Show("Restore successful! The application will now restart.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                        var exePath = Environment.ProcessPath;
                        if (exePath != null)
                        {
                            Process.Start(exePath);
                            Application.Current.Shutdown();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Restore Failed (Database might be locked): {ex.Message}\n\nTry closing the app and replacing 'inventory.db' manually.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DeleteBackup(BackupFile file)
        {
            if (MessageBox.Show($"Permanently delete backup '{file.FileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    _backupService.DeleteBackup(file.FullPath);
                    RefreshList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not delete file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // --- 3. CLOUD BACKUP ACTIONS ---
        private async Task TestCloudUpload()
        {
            try
            {
                if (Backups.Count == 0)
                {
                    if (MessageBox.Show("No local backups found. Create one now and upload it?", "No Backups", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        await CreateBackup();
                    }
                    else
                    {
                        return;
                    }
                }

                var latestFile = Backups.FirstOrDefault();
                if (latestFile != null)
                {
                    if (MessageBox.Show($"Upload latest backup ('{latestFile.FileName}') to Google Drive?\n\nA browser window may open for authentication.", "Cloud Sync", MessageBoxButton.OKCancel, MessageBoxImage.Information) == MessageBoxResult.OK)
                    {
                        await GoogleDriveService.UploadBackupAsync(latestFile.FullPath);
                        MessageBox.Show("✅ Upload Successful!\nFile is secure in Google Drive.", "Cloud Sync", MessageBoxButton.OK, MessageBoxImage.Information);

                        try { File.WriteAllText(CloudConfig, DateTime.Now.ToString()); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cloud Upload Failed.\n\nError: {ex.Message}\n\nCheck your internet connection.", "Upload Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // --- 4. AUTOMATIC BACKGROUND CHECK ---
        public async Task CheckAndRunAutoBackup()
        {
            try
            {
                DateTime lastRun = DateTime.MinValue;
                if (File.Exists(CloudConfig))
                {
                    DateTime.TryParse(File.ReadAllText(CloudConfig), out lastRun);
                }

                double hoursSince = (DateTime.Now - lastRun).TotalHours;

                // UPDATED: Run every 6 Hours
                if (hoursSince >= 6)
                {
                    await _backupService.CreateBackupAsync(BackupFolderPath);

                    // Run cleanup silently in background
                    PerformAutoCleanup();

                    var files = _backupService.GetBackups(BackupFolderPath);
                    var latest = files.OrderByDescending(f => f.FileName).FirstOrDefault();

                    if (latest != null)
                    {
                        await GoogleDriveService.UploadBackupAsync(latest.FullPath);
                        File.WriteAllText(CloudConfig, DateTime.Now.ToString());
                    }
                }
            }
            catch
            {
                // Silent fail for background tasks
            }
        }
    }
}