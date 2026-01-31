using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InventorySystem.Infrastructure.Services
{
    public class BackupFile
    {
        public string FileName { get; set; } = "";
        public string FullPath { get; set; } = "";
        public DateTime CreatedDate { get; set; }
        public string Size { get; set; } = "";
    }

    public static class BackupService
    {
        private static string DbPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "inventory.db");

        // 1. GET BACKUPS (From specific folder)
        public static List<BackupFile> GetBackups(string folderPath)
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

        // 2. CREATE BACKUP (To specific folder)
        public static void CreateBackup(string targetFolder)
        {
            if (!File.Exists(DbPath)) throw new FileNotFoundException("Database not found!");
            if (!Directory.Exists(targetFolder)) Directory.CreateDirectory(targetFolder);

            string fileName = $"Backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.db";
            string destPath = Path.Combine(targetFolder, fileName);

            File.Copy(DbPath, destPath);
        }

        public static void RestoreBackup(string backupPath)
        {
            if (!File.Exists(backupPath)) throw new FileNotFoundException("Backup file missing!");

            string tempName = DbPath + ".old";
            if (File.Exists(tempName)) File.Delete(tempName);

            try
            {
                File.Move(DbPath, tempName);
                File.Copy(backupPath, DbPath);
            }
            catch
            {
                File.Copy(backupPath, DbPath, true);
            }
        }

        public static void DeleteBackup(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}