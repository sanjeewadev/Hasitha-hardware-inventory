using InventorySystem.Data.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InventorySystem.Infrastructure.Services
{
    public class BackupFile
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string Size { get; set; } = "";
    }

    public class BackupService
    {
        private readonly InventoryDbContext _context;
        private readonly string _dbPath;

        public BackupService(InventoryDbContext context)
        {
            _context = context;
            // Get the actual physical path of the running database
            _dbPath = _context.Database.GetDbConnection().DataSource;
        }

        // 1. GET BACKUPS
        public List<BackupFile> GetBackups(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return new List<BackupFile>();

            var files = Directory.GetFiles(folderPath, "*.db");
            var list = new List<BackupFile>();

            foreach (var f in files)
            {
                var info = new FileInfo(f);
                list.Add(new BackupFile
                {
                    FileName = info.Name,
                    FullPath = info.FullName,
                    CreatedDate = info.CreationTime,
                    Size = $"{info.Length / 1024.0:F1} KB"
                });
            }

            return list.OrderByDescending(x => x.CreatedDate).ToList();
        }

        // 2. CREATE BACKUP (Safe while running)
        public async Task CreateBackupAsync(string targetFolder)
        {
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            string fileName = $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
            string destPath = Path.Combine(targetFolder, fileName);

            // "VACUUM INTO" creates a safe snapshot even if the app is active
            string sql = $"VACUUM INTO '{destPath}'";
            await _context.Database.ExecuteSqlRawAsync(sql);
        }

        // 3. RESTORE BACKUP (File Operations Only)
        public void RestoreBackup(string backupPath)
        {
            if (!File.Exists(backupPath)) throw new FileNotFoundException("Backup file missing!");

            string currentDb = _dbPath;
            string tempName = currentDb + ".old";

            // Try to rename the current DB file to move it out of the way.
            // Note: On Windows, you can usually rename a locked file (Move), 
            // but you cannot delete it.

            if (File.Exists(tempName)) File.Delete(tempName);

            File.Move(currentDb, tempName); // Rename current active DB
            File.Copy(backupPath, currentDb); // Copy backup into place
        }

        public void DeleteBackup(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}