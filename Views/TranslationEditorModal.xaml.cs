using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Views
{
    public class TranslationItemViewModel : INotifyPropertyChanged
    {
        public string Key { get; set; } = string.Empty;
        public string EnglishText { get; set; } = string.Empty;

        private string _translatedText = string.Empty;
        public string TranslatedText
        {
            get => _translatedText;
            set
            {
                if (_translatedText != value)
                {
                    _translatedText = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class CultureOption
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string NativeName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    public class TranslationDraftModel
    {
        public string LangCode { get; set; } = string.Empty;
        public string LangName { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public Dictionary<string, string> Translations { get; set; } = new();
    }

    public partial class TranslationEditorModal : Window
    {
        public ObservableCollection<TranslationItemViewModel> TranslationItems { get; } = new();
        private readonly List<CultureOption> _allCultures = new();
        private readonly DispatcherTimer _autoSaveTimer = new();
        private bool _isLoadingData = false;

        private string DraftsDirectory => Path.Combine(TxaLanguageManager.LanguagesDirectory, "drafts");

        public TranslationEditorModal()
        {
            InitializeComponent();
            ItemsTranslationList.ItemsSource = TranslationItems;

            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(700);
            _autoSaveTimer.Tick += (s, e) =>
            {
                _autoSaveTimer.Stop();
                SaveDraftToDisk();
            };

            TxtAuthor.Text = Environment.UserName;
            EnsureDraftsDirectory();
            LoadAvailableCultures();
            LoadEnglishSourceTemplate();
        }

        private void EnsureDraftsDirectory()
        {
            try
            {
                if (!Directory.Exists(DraftsDirectory))
                {
                    Directory.CreateDirectory(DraftsDirectory);
                }
            }
            catch { }
        }

        private void LoadAvailableCultures()
        {
            _allCultures.Clear();

            // Set of already installed language codes in app
            var existingCodes = new HashSet<string>(
                TxaLanguageManager.AvailableLanguages.Select(l => l.lang_code),
                StringComparer.OrdinalIgnoreCase
            );

            var cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .OrderBy(c => c.EnglishName)
                .ToList();

            foreach (var ci in cultures)
            {
                // Filter out existing installed languages (vi-VN, en-US, etc.) unless editing draft
                if (existingCodes.Contains(ci.Name)) continue;

                _allCultures.Add(new CultureOption
                {
                    Code = ci.Name,
                    DisplayName = $"{ci.EnglishName} - {ci.NativeName} [{ci.Name}]",
                    NativeName = ci.NativeName
                });
            }

            CmbTargetCulture.ItemsSource = _allCultures;
            if (_allCultures.Count > 0)
            {
                CmbTargetCulture.SelectedIndex = 0;
            }
        }

        private void LoadEnglishSourceTemplate()
        {
            var enDict = TxaLanguageManager.GetDefaultEnglishDictionary();
            TranslationItems.Clear();

            foreach (var kv in enDict)
            {
                TranslationItems.Add(new TranslationItemViewModel
                {
                    Key = kv.Key,
                    EnglishText = kv.Value,
                    TranslatedText = string.Empty
                });
            }

            UpdateProgress();
        }

        private void CmbTargetCulture_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbTargetCulture.SelectedItem is not CultureOption selected) return;

            LoadDraftForCulture(selected.Code);
        }

        private void LoadDraftForCulture(string cultureCode)
        {
            _isLoadingData = true;
            try
            {
                string draftFile = Path.Combine(DraftsDirectory, $"{cultureCode}.json");
                if (File.Exists(draftFile))
                {
                    string json = File.ReadAllText(draftFile);
                    var draft = JsonSerializer.Deserialize<TranslationDraftModel>(json);
                    if (draft != null)
                    {
                        if (!string.IsNullOrWhiteSpace(draft.Author))
                        {
                            TxtAuthor.Text = draft.Author;
                        }

                        foreach (var item in TranslationItems)
                        {
                            if (draft.Translations.TryGetValue(item.Key, out var transVal))
                            {
                                item.TranslatedText = transVal;
                            }
                            else
                            {
                                item.TranslatedText = string.Empty;
                            }
                        }

                        TxtDraftStatus.Text = " • Đã nạp bản nháp tự động";
                    }
                }
                else
                {
                    // Clear translations for new language
                    foreach (var item in TranslationItems)
                    {
                        item.TranslatedText = string.Empty;
                    }
                    TxtDraftStatus.Text = " • Bản dịch mới";
                }
            }
            catch { }
            finally
            {
                _isLoadingData = false;
                UpdateProgress();
            }
        }

        private void TranslationTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingData) return;

            UpdateProgress();
            TxtDraftStatus.Text = " • Đang lưu nháp...";
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void TxtAuthor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingData) return;
            _autoSaveTimer.Stop();
            _autoSaveTimer.Start();
        }

        private void UpdateProgress()
        {
            int total = TranslationItems.Count;
            if (total == 0) return;

            int translated = TranslationItems.Count(t => !string.IsNullOrWhiteSpace(t.TranslatedText));
            double percent = (double)translated / total * 100.0;

            TxtProgressSummary.Text = $"Tiến độ dịch: {translated} / {total} chuỗi ({percent:F1}%)";
            PbTranslationProgress.Value = percent;

            bool isComplete = (translated == total && total > 0);
            BtnSubmitGithub.IsEnabled = isComplete;
        }

        private void SaveDraftToDisk()
        {
            if (CmbTargetCulture.SelectedItem is not CultureOption selected) return;

            try
            {
                var draft = new TranslationDraftModel
                {
                    LangCode = selected.Code,
                    LangName = selected.NativeName,
                    Author = TxtAuthor.Text.Trim(),
                    Translations = TranslationItems.Where(t => !string.IsNullOrWhiteSpace(t.TranslatedText))
                                                  .ToDictionary(t => t.Key, t => t.TranslatedText)
                };

                string draftFile = Path.Combine(DraftsDirectory, $"{selected.Code}.json");
                string json = JsonSerializer.Serialize(draft, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(draftFile, json);

                TxtDraftStatus.Text = " • Đã lưu nháp tự động";
            }
            catch { }
        }

        private void BtnSaveAndApply_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTargetCulture.SelectedItem is not CultureOption selected)
            {
                MessageBox.Show("Vui lòng chọn một ngôn ngữ đích để lưu.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int translatedCount = TranslationItems.Count(t => !string.IsNullOrWhiteSpace(t.TranslatedText));
            if (translatedCount == 0)
            {
                MessageBox.Show("Vui lòng dịch ít nhất một vài câu trước khi lưu & áp dụng.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // 1. Save draft
                SaveDraftToDisk();

                // 2. Build TxaLanguagePackage
                string authorName = string.IsNullOrWhiteSpace(TxtAuthor.Text) ? Environment.UserName : TxtAuthor.Text.Trim();
                var pkg = new TxaLanguagePackage
                {
                    lang_code = selected.Code,
                    lang_name = selected.NativeName,
                    author = authorName,
                    txa_key = TranslationItems.ToDictionary(
                        t => t.Key,
                        t => !string.IsNullOrWhiteSpace(t.TranslatedText) ? t.TranslatedText : t.EnglishText
                    )
                };

                // 3. Encrypt and save .txal package
                string destPath = Path.Combine(TxaLanguageManager.LanguagesDirectory, $"{selected.Code}.txal");
                byte[] encryptedData = TxaLanguageManager.EncryptLanguagePackage(pkg);
                File.WriteAllBytes(destPath, encryptedData);

                // 4. Reload and apply language immediately
                TxaLanguageManager.ScanAvailableLanguages();
                TxaLanguageManager.ApplyLanguageByCode(selected.Code, saveToConfig: true);

                MessageBox.Show(
                    $"Đã lưu thành công gói ngôn ngữ {pkg.lang_name} ({pkg.lang_code}) và áp dụng ngay lập tức vào Steam Route Fixer!",
                    "Hoàn tất biên dịch",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lưu gói ngôn ngữ: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSubmitGithub_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTargetCulture.SelectedItem is not CultureOption selected) return;

            string authorName = string.IsNullOrWhiteSpace(TxtAuthor.Text) ? Environment.UserName : TxtAuthor.Text.Trim();
            
            var translationDict = TranslationItems.ToDictionary(t => t.Key, t => t.TranslatedText);
            string jsonBody = JsonSerializer.Serialize(translationDict, new JsonSerializerOptions { WriteIndented = true });

            string title = $"[Community Language] {selected.NativeName} ({selected.Code}) by {authorName}";
            string body = $@"### 🌐 Community Language Translation Submission

- **Language Name**: {selected.NativeName}
- **Language Code**: {selected.Code}
- **Author / Translator**: {authorName}
- **App Version**: Steam Route Fixer v1.0.0

```json
{jsonBody}
```
";
            string encodedTitle = WebUtility.UrlEncode(title);
            string encodedBody = WebUtility.UrlEncode(body);
            string url = $"https://github.com/TXAVL/SteamRouteFixer/issues/new?title={encodedTitle}&body={encodedBody}";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở trình duyệt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SaveDraftToDisk();
            Close();
        }
    }
}
