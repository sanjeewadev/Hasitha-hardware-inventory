using InventorySystem.Infrastructure.Services;
using InventorySystem.UI.Commands;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing.Printing;
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
        private const int MaxLocalBackups = 30;

        private readonly BackupService _backupService;

        // --- PROPERTIES ---
        public ObservableCollection<BackupFile> Backups { get; } = new();
        public ObservableCollection<string> InstalledPrinters { get; } = new();

        private string _backupFolderPath = "";
        public string BackupFolderPath
        {
            get => _backupFolderPath;
            set { _backupFolderPath = value; OnPropertyChanged(); }
        }

        private string _selectedPrinter = "";
        public string SelectedPrinter
        {
            get => _selectedPrinter;
            set { _selectedPrinter = value; OnPropertyChanged(); }
        }

        private int _copyCount;
        public int CopyCount
        {
            get => _copyCount;
            set
            {
                // FIX: Printer Safety Limits! Prevent 0, negatives, or massive paper waste.
                if (value < 1) value = 1;
                if (value > 3) value = 3;

                _copyCount = value;
                OnPropertyChanged();
            }
        }

        // --- COMMANDS ---
        public ICommand BrowseFolderCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand TestCloudUploadCommand { get; }
        public ICommand SavePrinterSettingsCommand { get; }

        public SettingsViewModel()
        {
            var db = DatabaseService.CreateDbContext();
            _backupService = new BackupService(db);

            // Init Commands
            BrowseFolderCommand = new RelayCommand(BrowseFolder);
            CreateBackupCommand = new RelayCommand(async () => await CreateBackup());
            RestoreCommand = new RelayCommand<BackupFile>(RestoreBackup);
            DeleteCommand = new RelayCommand<BackupFile>(DeleteBackup);
            TestCloudUploadCommand = new RelayCommand(async () => await TestCloudUpload());
            SavePrinterSettingsCommand = new RelayCommand(SavePrinterSettings);

            LoadBackupSettings();
            LoadPrinterSettings();
            RefreshList();
        }

        // --- PRINTER LOGIC ---
        private void LoadPrinterSettings()
        {
            try
            {
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    InstalledPrinters.Add(printer);
                }
            }
            catch { }

            SelectedPrinter = InventorySystem.UI.Properties.Settings.Default.PrinterName ?? "";

            int savedCopies = InventorySystem.UI.Properties.Settings.Default.ReceiptCopies;
            CopyCount = savedCopies < 1 ? 1 : savedCopies;
        }

        private void SavePrinterSettings()
        {
            InventorySystem.UI.Properties.Settings.Default.PrinterName = SelectedPrinter;
            InventorySystem.UI.Properties.Settings.Default.ReceiptCopies = CopyCount;
            InventorySystem.UI.Properties.Settings.Default.Save();
            MessageBox.Show("Printer configuration saved securely!", "Settings Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- BACKUP LOGIC ---
        private void LoadSettings() { LoadBackupSettings(); }

        private void LoadBackupSettings()
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
            SaveBackupConfig();
        }

        private void SaveBackupConfig()
        {
            try { File.WriteAllText(ConfigFile, BackupFolderPath); } catch { }
        }

        private void BrowseFolder()
        {
            var dialog = new OpenFolderDialog { Title = "Select Backup Location", InitialDirectory = BackupFolderPath };
            if (dialog.ShowDialog() == true)
            {
                if (Directory.Exists(dialog.FolderName))
                {
                    BackupFolderPath = dialog.FolderName;
                    SaveBackupConfig();
                    RefreshList();
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
                    var files = _backupService.GetBackups(BackupFolderPath).OrderByDescending(f => f.FileName);
                    foreach (var f in files) Backups.Add(f);
                }
            }
            catch { }
        }

        private async Task CreateBackup()
        {
            try
            {
                await _backupService.CreateBackupAsync(BackupFolderPath);
                PerformAutoCleanup();
                RefreshList();
                MessageBox.Show("Database Backup Created Successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PerformAutoCleanup()
        {
            try
            {
                var allFiles = _backupService.GetBackups(BackupFolderPath).OrderByDescending(f => f.FileName).ToList();
                if (allFiles.Count > MaxLocalBackups)
                {
                    foreach (var file in allFiles.Skip(MaxLocalBackups))
                    {
                        try { _backupService.DeleteBackup(file.FullPath); } catch { }
                    }
                }
            }
            catch { }
        }

        private void RestoreBackup(BackupFile file)
        {
            // --- 3-TIER ESCALATION WARNING SYSTEM ---

            // Warning 1
            var result1 = MessageBox.Show(
                $"Are you sure you want to restore the backup from '{file.CreatedDate:dd MMM yyyy hh:mm tt}'?\n\nALL current live data will be permanently overwritten.",
                "Critical Warning (1/3)",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result1 != MessageBoxResult.Yes) return;

            // Warning 2
            var result2 = MessageBox.Show(
                "⚠️ DANGER: Any sales, returns, or stock adjustments made AFTER this backup was created will be LOST FOREVER.\n\nDo you still want to proceed?",
                "Data Loss Warning (2/3)",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result2 != MessageBoxResult.Yes) return;

            // Warning 3
            var result3 = MessageBox.Show(
                "🚨 FINAL CONFIRMATION 🚨\n\nYou are about to replace the live database. This action CANNOT BE UNDONE.\n\nAre you absolutely certain?",
                "Final Warning (3/3)",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result3 != MessageBoxResult.Yes) return;

            // --- EXECUTE RESTORE ---
            try
            {
                _backupService.RestoreBackup(file.FullPath);
                MessageBox.Show("Restore successful! The application will now restart to apply changes.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);

                var exePath = Environment.ProcessPath;
                if (exePath != null) { Process.Start(exePath); Application.Current.Shutdown(); }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Restore Failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteBackup(BackupFile file)
        {
            if (MessageBox.Show($"Delete the backup file '{file.FileName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _backupService.DeleteBackup(file.FullPath);
                RefreshList();
            }
        }

        private async Task TestCloudUpload()
        {
            await Task.Delay(100);
            MessageBox.Show("Cloud Backup Sync requires an active Premium Cloud Add-on.\n\nPlease contact your software provider to enable cloud capabilities.", "Cloud Sync Unavailable", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public async Task CheckAndRunAutoBackup()
        {
            try
            {
                if (!Directory.Exists(BackupFolderPath)) return;
                await Task.Delay(100);
            }
            catch { }
        }
    }
}