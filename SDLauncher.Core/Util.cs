using Newtonsoft.Json;
using SDLauncher.Core.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SDLauncher.Core
{
    public static class JSONConverter
    {
        public static LabrinthResults.SearchResult ConvertToLabrinthSearchResult(string json)
        {
            return JsonConvert.DeserializeObject<LabrinthResults.SearchResult>(json);
        }
        public static LabrinthResults.ModrinthProject ConvertToLabrinthProject(string json)
        {
            return JsonConvert.DeserializeObject<LabrinthResults.ModrinthProject>(json);
        }
        public static List<LabrinthResults.DownloadManager.DownloadLink> ConvertDownloadLinksToCS(string json)
        {
            return JsonConvert.DeserializeObject<List<LabrinthResults.DownloadManager.DownloadLink>>(json);
        }
    }
    public static class Util
    {
        public static bool FolderExists(string path)
        {
            string p = NormalizePath(path);

            return Directory.Exists(p);
        }
        public static string NormalizePath(string path)
        {
            return Path.GetFullPath(path)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar);
        }
        public static string KiloFormat(this int num)
        {
            if (num >= 100000000)
                return (num / 1000000).ToString("#,0M");

            if (num >= 10000000)
                return (num / 1000000).ToString("0.#") + "M";

            if (num >= 100000)
                return (num / 1000).ToString("#,0K");

            if (num >= 10000)
                return (num / 1000).ToString("0.#") + "K";

            if (num >= 1000)
                return (num / 100).ToString("0.#") + "K";

            return num.ToString("#,0");
        }

        public static async Task<string> DownloadText(string url)
        {
            var c = new System.Net.WebClient();
            var s = await c.DownloadStringTaskAsync(new Uri(url));
            return s;
        }
    }
    public class FileDownloader : IDisposable
    {
        private readonly string _downloadUrl;
        private readonly string _destinationFilePath;

        private HttpClient _httpClient;

        public delegate void ProgressChangedHandler(long? totalFileSize, long totalBytesDownloaded, double? progressPercentage);

        public event ProgressChangedHandler ProgressChanged;

        public FileDownloader(string downloadUrl, string destinationFilePath)
        {
            _downloadUrl = downloadUrl;
            _destinationFilePath = destinationFilePath;
        }

        public async Task StartDownload()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromDays(1) };

            using (var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                await DownloadFileFromHttpResponseMessage(response);
        }

        private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response)
        {
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;

            using (var contentStream = await response.Content.ReadAsStreamAsync())
                await ProcessContentStream(totalBytes, contentStream);
        }

        private async Task ProcessContentStream(long? totalDownloadSize, Stream contentStream)
        {
            var totalBytesRead = 0L;
            var readCount = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            using (var fileStream = new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
            {
                do
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 100 == 0)
                        TriggerProgressChanged(totalDownloadSize, totalBytesRead);
                }
                while (isMoreToRead);
            }
        }

        private void TriggerProgressChanged(long? totalDownloadSize, long totalBytesRead)
        {
            if (ProgressChanged == null)
                return;

            double? progressPercentage = null;
            if (totalDownloadSize.HasValue)
                progressPercentage = Math.Round((double)totalBytesRead / totalDownloadSize.Value * 100, 2);

            ProgressChanged(totalDownloadSize, totalBytesRead, progressPercentage);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
