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

        private string _selectedPrinter = ""; // Fixed Non-nullable warning
        public string SelectedPrinter
        {
            get => _selectedPrinter;
            set { _selectedPrinter = value; OnPropertyChanged(); }
        }

        private int _copyCount;
        public int CopyCount
        {
            get => _copyCount;
            set { _copyCount = value; OnPropertyChanged(); }
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
            CopyCount = InventorySystem.UI.Properties.Settings.Default.ReceiptCopies;
            if (CopyCount < 1) CopyCount = 1;
        }

        private void SavePrinterSettings()
        {
            InventorySystem.UI.Properties.Settings.Default.PrinterName = SelectedPrinter;
            InventorySystem.UI.Properties.Settings.Default.ReceiptCopies = CopyCount;
            InventorySystem.UI.Properties.Settings.Default.Save();
            MessageBox.Show("Printer configuration saved!", "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // --- BACKUP LOGIC ---
        private void LoadSettings() { LoadBackupSettings(); } // Alias for old calls

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
                MessageBox.Show("Backup Success!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
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
            if (MessageBox.Show("Restore this backup? Current data will be lost.", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    _backupService.RestoreBackup(file.FullPath);
                    MessageBox.Show("Restore successful! Restarting...", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    var exePath = Environment.ProcessPath;
                    if (exePath != null) { Process.Start(exePath); Application.Current.Shutdown(); }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message); }
            }
        }

        private void DeleteBackup(BackupFile file)
        {
            if (MessageBox.Show($"Delete '{file.FileName}'?", "Confirm", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _backupService.DeleteBackup(file.FullPath);
                RefreshList();
            }
        }

        private async Task TestCloudUpload()
        {
            // Placeholder to fix async warning
            await Task.Delay(100);
            MessageBox.Show("Cloud Upload Logic Placeholder");
        }

        // --- PUBLIC METHOD FOR APP.XAML.CS ---
        public async Task CheckAndRunAutoBackup()
        {
            try
            {
                // Simple logic: If no backups exist, create one.
                if (!Directory.Exists(BackupFolderPath)) return;

                // You can add time-based logic here later
                await Task.Delay(100); // Placeholder
            }
            catch { }
        }
    }
}