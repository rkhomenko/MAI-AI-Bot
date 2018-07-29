using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.File;

namespace MAIAIBot.Core
{
    public class AzureStorageProvider : IStorageProvider
    {
        private string ShareName;
        private string Prefix;
        private string SasToken;
        private CloudFileShare FileShare;
        
        public AzureStorageProvider(string shareName,
                                    string prefix,
                                    string sasToken,
                                    string connectionStr)
        {
            ShareName = shareName;
            Prefix = prefix;
            SasToken = sasToken;

            var storageAccount = CloudStorageAccount.Parse(connectionStr);
            var fileClient = storageAccount.CreateCloudFileClient();
            FileShare = fileClient.GetShareReference(ShareName);
        }

        public Uri GetCorrectUri(Uri uri)
        {
            return new Uri(uri + SasToken);
        }
        public async Task<Uri> Load(Stream stream, string name)
        {
            var rootDir = FileShare.GetRootDirectoryReference();
            var cloudFile = rootDir.GetFileReference(
                Prefix +
                Guid.NewGuid() +
                "-" +
                name
            );
            
            await cloudFile.UploadFromStreamAsync(stream);
            
            return await Task.FromResult(cloudFile.Uri);
        }

        public async Task Remove(Uri uri)
            => await new CloudFile(uri).DeleteAsync();
    }
}