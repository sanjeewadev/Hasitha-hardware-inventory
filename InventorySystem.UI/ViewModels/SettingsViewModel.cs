using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
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
        private const string CloudConfig = "cloud_history.txt"; // Stores last upload time

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
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            CreateBackupCommand = new RelayCommand(CreateBackup);
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

            // Default
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
                BackupFolderPath = dialog.FolderName;
                SaveSettings();
                RefreshList();
            }
        }

        private void RefreshList()
        {
            Backups.Clear();
            var files = BackupService.GetBackups(BackupFolderPath);
            foreach (var f in files) Backups.Add(f);
        }

        private void CreateBackup()
        {
            try
            {
                BackupService.CreateBackup(BackupFolderPath);
                RefreshList();
                MessageBox.Show("Backup created successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        private void RestoreBackup(BackupFile file)
        {
            var result1 = MessageBox.Show(
                $"You are about to restore data from:\n'{file.FileName}'\n\nThis will OVERWRITE current data. Continue?",
                "Confirm Restore (Step 1/2)", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result1 == MessageBoxResult.Yes)
            {
                var result2 = MessageBox.Show(
                    "⚠️ FINAL WARNING ⚠️\n\nThe application will close immediately after restore.\nAre you absolutely sure?",
                    "Final Confirmation (Step 2/2)", MessageBoxButton.YesNo, MessageBoxImage.Error);

                if (result2 == MessageBoxResult.Yes)
                {
                    try
                    {
                        BackupService.RestoreBackup(file.FullPath);
                        MessageBox.Show("Restore successful! Application will now close.");
                        Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Restore Failed: {ex.Message}");
                    }
                }
            }
        }

        private void DeleteBackup(BackupFile file)
        {
            if (MessageBox.Show($"Delete '{file.FileName}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                BackupService.DeleteBackup(file.FullPath);
                RefreshList();
            }
        }

        // --- 3. CLOUD BACKUP ACTIONS ---

        // Manual Test Button
        private async Task TestCloudUpload()
        {
            try
            {
                if (Backups.Count == 0)
                {
                    BackupService.CreateBackup(BackupFolderPath);
                    RefreshList();
                }

                var latestFile = Backups.FirstOrDefault();
                if (latestFile != null)
                {
                    if (MessageBox.Show("Start Upload? Browser may open.", "Cloud Sync", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        await GoogleDriveService.UploadBackupAsync(latestFile.FullPath);
                        MessageBox.Show("✅ Upload Successful!");

                        // Update the timer file so it doesn't run again automatically immediately
                        File.WriteAllText(CloudConfig, DateTime.Now.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Upload Failed: {ex.Message}");
            }
        }

        // --- 4. AUTOMATIC BACKGROUND CHECK (Called by App.xaml.cs) ---
        public async Task CheckAndRunAutoBackup()
        {
            try
            {
                // 1. Check last run time
                DateTime lastRun = DateTime.MinValue;
                if (File.Exists(CloudConfig))
                {
                    DateTime.TryParse(File.ReadAllText(CloudConfig), out lastRun);
                }

                double hoursSince = (DateTime.Now - lastRun).TotalHours;

                // 2. Only run if > 12 hours passed
                if (hoursSince >= 12)
                {
                    // Ensure we have a local backup to send
                    // (Optional: Create a fresh one right now if you want strict 12h snapshots)
                    BackupService.CreateBackup(BackupFolderPath);
                    var files = BackupService.GetBackups(BackupFolderPath);
                    var latest = files.FirstOrDefault();

                    if (latest != null)
                    {
                        // 3. Upload (Silent Mode - No MessageBox)
                        await GoogleDriveService.UploadBackupAsync(latest.FullPath);

                        // 4. Update timer
                        File.WriteAllText(CloudConfig, DateTime.Now.ToString());
                    }
                }
            }
            catch
            {
                // Silent fail (e.g. no internet). Will try again next startup.
            }
        }
    }
}