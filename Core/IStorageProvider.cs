using System;
using System.IO;
using System.Threading.Tasks;

namespace MAIAIBot.Core
{
    public interface IStorageProvider
    {
        Uri GetCorrectUri(Uri uriFromDatabase);

        Task<Uri> Load(Stream stream, string name);

        Task Remove(Uri uri);
    }
}
