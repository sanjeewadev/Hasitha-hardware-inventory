using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace InventorySystem.Infrastructure.Services
{
    public static class GoogleDriveService
    {
        private static string[] Scopes = { DriveService.Scope.DriveFile };
        private static string ApplicationName = "Inventory Backup System";
        private static string CredentialsPath = "credentials.json";
        private static string TokenPath = "token.json"; // Google will create this auto-magically

        // The exact name of the file in Google Drive (We overwrite this single file)
        private const string CloudFileName = "Inventory_AutoBackup.db";

        // 1. AUTHENTICATE
        private static async Task<DriveService> GetDriveService()
        {
            if (!File.Exists(CredentialsPath))
                throw new FileNotFoundException("credentials.json not found! Did you set 'Copy to Output'?");

            using (var stream = new FileStream(CredentialsPath, FileMode.Open, FileAccess.Read))
            {
                // This opens the browser for the user to login (only the first time)
                UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    Scopes,
                    "user",
                    CancellationToken.None,
                    new FileDataStore(TokenPath, true));

                return new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = ApplicationName,
                });
            }
        }

        // 2. UPLOAD (AND OVERWRITE)
        public static async Task UploadBackupAsync(string localFilePath)
        {
            var service = await GetDriveService();

            // Step A: Check if the file already exists in Cloud
            var listRequest = service.Files.List();
            listRequest.Q = $"name = '{CloudFileName}' and trashed = false";
            listRequest.Fields = "files(id, name)";
            var files = await listRequest.ExecuteAsync();

            var existingFile = files.Files.FirstOrDefault();

            using (var uploadStream = new FileStream(localFilePath, FileMode.Open))
            {
                if (existingFile != null)
                {
                    // UPDATE existing file (Overwrite)
                    var updateRequest = service.Files.Update(new Google.Apis.Drive.v3.Data.File(), existingFile.Id, uploadStream, "application/octet-stream");
                    await updateRequest.UploadAsync();
                }
                else
                {
                    // CREATE new file
                    var fileMetadata = new Google.Apis.Drive.v3.Data.File()
                    {
                        Name = CloudFileName
                    };
                    var createRequest = service.Files.Create(fileMetadata, uploadStream, "application/octet-stream");
                    await createRequest.UploadAsync();
                }
            }
        }
    }
}