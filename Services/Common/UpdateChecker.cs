using System.IO;
using System.Net.Http;
using System.Text.Json;
using SteamRouteFixer.Models;

namespace SteamRouteFixer.Services.Common
{
    public class UpdateChecker
    {
        private readonly HttpClient _httpClient;
        public static readonly string CurrentVersion = "1.0.0";

        public UpdateChecker()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SteamRouteFixer-Updater");
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task<AppUpdateInfo> CheckForUpdateAsync(string updateUrl)
        {
            var result = new AppUpdateInfo
            {
                Version = CurrentVersion,
                ReleaseDate = DateTime.Now.ToString("yyyy-MM-dd"),
                HasUpdate = false
            };

            try
            {
                if (string.IsNullOrWhiteSpace(updateUrl))
                {
                    result.IsError = true;
                    result.ErrorMessage = "Chưa cấu hình URL kiểm tra cập nhật.";
                    return result;
                }

                var response = await _httpClient.GetAsync(updateUrl);
                if (!response.IsSuccessStatusCode)
                {
                    result.IsError = true;
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        result.ErrorMessage = "Chưa có bản Release nào được xuất bản trên repo GitHub TXAVL/SteamRouteFixer.";
                        result.Changelog = "Hiện tại chưa có bản phát hành chính thức nào trên GitHub Releases. Bạn đang chạy phiên bản thử nghiệm cục bộ v1.0.0.";
                    }
                    else
                    {
                        result.ErrorMessage = $"Máy chủ GitHub trả về mã {(int)response.StatusCode} ({response.ReasonPhrase}).";
                        result.Changelog = $"Không thể lấy thông tin release từ {updateUrl}";
                    }
                    return result;
                }

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latestVersion = CurrentVersion;
                string changelog = string.Empty;
                string downloadUrl = string.Empty;

                // Handle GitHub Releases format
                if (root.TryGetProperty("tag_name", out var tagProp))
                {
                    latestVersion = tagProp.GetString()?.TrimStart('v', 'V') ?? CurrentVersion;
                    if (root.TryGetProperty("body", out var bodyProp))
                    {
                        changelog = bodyProp.GetString() ?? string.Empty;
                    }
                    if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array && assetsProp.GetArrayLength() > 0)
                    {
                        var firstAsset = assetsProp[0];
                        if (firstAsset.TryGetProperty("browser_download_url", out var dlProp))
                        {
                            downloadUrl = dlProp.GetString() ?? string.Empty;
                        }
                    }
                }
                // Handle Custom JSON format
                else if (root.TryGetProperty("version", out var verProp))
                {
                    latestVersion = verProp.GetString() ?? CurrentVersion;
                    if (root.TryGetProperty("changelog", out var bodyProp))
                    {
                        changelog = bodyProp.GetString() ?? string.Empty;
                    }
                    if (root.TryGetProperty("download_url", out var dlProp))
                    {
                        downloadUrl = dlProp.GetString() ?? string.Empty;
                    }
                }

                result.Version = latestVersion;
                result.Changelog = string.IsNullOrWhiteSpace(changelog) ? "Có phiên bản cập nhật mới với nhiều cải tiến." : changelog;
                result.DownloadUrl = downloadUrl;

                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    result.HasUpdate = true;
                }
            }
            catch (Exception ex)
            {
                result.Changelog = $"Không thể kết nối đến máy chủ kiểm tra cập nhật: {ex.Message}";
            }

            return result;
        }

        public async Task<string> DownloadUpdateAsync(string url, IProgress<int> progress, CancellationToken ct = default)
        {
            StoragePathManager.EnsureDirectories();
            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "SteamRouteFixer_Update.exe";
            }

            string targetPath = Path.Combine(StoragePathManager.SetupDirectory, fileName);

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? -1;
            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    int percentage = (int)((totalRead * 100) / totalBytes);
                    progress.Report(percentage);
                }
            }

            progress.Report(100);
            return targetPath;
        }

        private static bool IsNewerVersion(string remote, string local)
        {
            if (Version.TryParse(remote, out var vRemote) && Version.TryParse(local, out var vLocal))
            {
                return vRemote > vLocal;
            }
            return string.Compare(remote, local, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
