using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SteamRouteFixer.Services.Common;

namespace SteamRouteFixer.Views
{
    public class TranslationItemViewModel : INotifyPropertyChanged
    {
        private static readonly Regex FormatVarRegex = new(@"\{(\d+)(:[^}]+)?\}", RegexOptions.Compiled);
        private static readonly Regex EnvVarRegex = new(@"%[A-Za-z0-9_]+%", RegexOptions.Compiled);

        public string Key { get; set; } = string.Empty;

        private string _englishText = string.Empty;
        public string EnglishText
        {
            get => _englishText;
            set
            {
                _englishText = value;
                ExtractVariables();
                ValidateTranslation();
                OnPropertyChanged();
            }
        }

        private string _translatedText = string.Empty;
        public string TranslatedText
        {
            get => _translatedText;
            set
            {
                if (_translatedText != value)
                {
                    _translatedText = value;
                    ValidateTranslation();
                    OnPropertyChanged();
                }
            }
        }

        public List<string> RequiredVariables { get; } = new();
        public bool HasVariables => RequiredVariables.Count > 0;
        public Visibility VariableBadgeVisibility => HasVariables ? Visibility.Visible : Visibility.Collapsed;
        public string VariablesHint => HasVariables ? $"⚠️ Biến cần có: {string.Join(", ", RequiredVariables)}" : string.Empty;

        private bool _hasPlaceholderError = false;
        public bool HasPlaceholderError
        {
            get => _hasPlaceholderError;
            set
            {
                _hasPlaceholderError = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ValidationWarningVisibility));
                OnPropertyChanged(nameof(InputBorderBrushColor));
            }
        }

        private string _validationErrorMessage = string.Empty;
        public string ValidationErrorMessage
        {
            get => _validationErrorMessage;
            set
            {
                _validationErrorMessage = value;
                OnPropertyChanged();
            }
        }

        public Visibility ValidationWarningVisibility => (HasPlaceholderError && !string.IsNullOrWhiteSpace(TranslatedText)) ? Visibility.Visible : Visibility.Collapsed;

        public Brush InputBorderBrushColor
        {
            get
            {
                if (HasPlaceholderError && !string.IsNullOrWhiteSpace(_translatedText))
                {
                    return new SolidColorBrush(Color.FromRgb(255, 82, 82)); // #FF5252 Red
                }
                if (Application.Current?.Resources["CardBorderBrush"] is Brush b)
                {
                    return b;
                }
                return new SolidColorBrush(Color.FromRgb(56, 56, 56));
            }
        }

        private void ExtractVariables()
        {
            RequiredVariables.Clear();
            if (string.IsNullOrEmpty(_englishText)) return;

            foreach (Match m in FormatVarRegex.Matches(_englishText))
            {
                if (!RequiredVariables.Contains(m.Value))
                    RequiredVariables.Add(m.Value);
            }
            foreach (Match m in EnvVarRegex.Matches(_englishText))
            {
                if (!RequiredVariables.Contains(m.Value))
                    RequiredVariables.Add(m.Value);
            }
            OnPropertyChanged(nameof(HasVariables));
            OnPropertyChanged(nameof(VariableBadgeVisibility));
            OnPropertyChanged(nameof(VariablesHint));
        }

        public void ValidateTranslation()
        {
            if (string.IsNullOrWhiteSpace(_translatedText) || RequiredVariables.Count == 0)
            {
                HasPlaceholderError = false;
                ValidationErrorMessage = string.Empty;
                return;
            }

            var missing = new List<string>();
            foreach (var reqVar in RequiredVariables)
            {
                var formatMatch = FormatVarRegex.Match(reqVar);
                if (formatMatch.Success)
                {
                    string idx = formatMatch.Groups[1].Value;
                    var transMatch = Regex.Match(_translatedText, @"\{" + idx + @"(:[^}]+)?\}");
                    if (!transMatch.Success)
                    {
                        missing.Add(reqVar);
                    }
                }
                else
                {
                    if (!_translatedText.Contains(reqVar, StringComparison.OrdinalIgnoreCase))
                    {
                        missing.Add(reqVar);
                    }
                }
            }

            if (missing.Count > 0)
            {
                HasPlaceholderError = true;
                ValidationErrorMessage = $"⚠️ Chưa nhập hoặc thiếu biến: {string.Join(", ", missing)}";
            }
            else
            {
                HasPlaceholderError = false;
                ValidationErrorMessage = string.Empty;
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

            ApplyLanguageTranslations();
            TxaLanguageManager.OnLanguageChanged += ApplyLanguageTranslations;
            Closed += (s, e) => TxaLanguageManager.OnLanguageChanged -= ApplyLanguageTranslations;

            _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(700);
            _autoSaveTimer.Tick += (s, e) =>
            {
                _autoSaveTimer.Stop();
                SaveDraftToDisk();
            };

            _isLoadingData = true;
            try
            {
                TxtAuthor.Text = Environment.UserName;
                EnsureDraftsDirectory();
                LoadEnglishSourceTemplate();
                LoadAvailableCultures();
            }
            finally
            {
                _isLoadingData = false;
                UpdateProgress();
            }
        }

        private void ApplyLanguageTranslations()
        {
            Title = TxaLanguageManager.GetString("t_trans_title", "Biên dịch Ngôn ngữ - TXA Language Translator");
            if (TxtTranslatorMainTitle != null) TxtTranslatorMainTitle.Text = TxaLanguageManager.GetString("t_trans_header", "TRÌNH BIÊN DỊCH NGÔN NGỮ (TXA TRANSLATOR)");
            if (TxtTranslatorMainSub != null) TxtTranslatorMainSub.Text = TxaLanguageManager.GetString("t_trans_sub", "Biên dịch toàn bộ giao diện từ Tiếng Anh chuẩn sang ngôn ngữ mong muốn. Tiến độ nháp tự động lưu liên tục.");

            if (TxtTargetLangLabel != null) TxtTargetLangLabel.Text = TxaLanguageManager.GetString("t_trans_target_lbl", "🎯 CHỌN NGÔN NGỮ ĐÍCH BIÊN DỊCH:");
            if (TxtAuthorLabel != null) TxtAuthorLabel.Text = TxaLanguageManager.GetString("t_trans_author_lbl", "✍️ TÊN / NICKNAME TÁC GIẢ BẢN DỊCH:");

            if (TxtColSourceHeader != null) TxtColSourceHeader.Text = TxaLanguageManager.GetString("t_trans_col_source", "🔤 VĂN BẢN TIẾNG ANH GỐC (SOURCE EN-US)");
            if (TxtColTargetHeader != null) TxtColTargetHeader.Text = TxaLanguageManager.GetString("t_trans_col_target", "✏️ BẢN DỊCH NGÔN NGỮ ĐÍCH CỦA BẠN (TARGET TRANSLATION)");

            if (BtnSaveAndApply != null) BtnSaveAndApply.Content = TxaLanguageManager.GetString("t_trans_btn_save", "💾 Lưu & Áp Dụng (.txal)");
            if (BtnSubmitGithub != null) BtnSubmitGithub.Content = TxaLanguageManager.GetString("t_trans_btn_submit", "🚀 Gửi Lên GitHub (100%)");
            if (BtnCloseTrans != null) BtnCloseTrans.Content = TxaLanguageManager.GetString("t_btn_close", "Đóng");
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
                .Where(c => !string.IsNullOrEmpty(c.Name) && !existingCodes.Contains(c.Name))
                .GroupBy(c => c.Name)
                .Select(g => g.First())
                .OrderBy(c => c.EnglishName)
                .ToList();

            foreach (var ci in cultures)
            {
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
                LoadDraftForCulture(_allCultures[0].Code);
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
        }

        private void CmbTargetCulture_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingData) return;
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

                        TxtDraftStatus.Text = " • " + TxaLanguageManager.GetString("t_draft_loaded", "Đã nạp bản nháp tự động");
                    }
                }
                else
                {
                    // Clear translations for new language
                    foreach (var item in TranslationItems)
                    {
                        item.TranslatedText = string.Empty;
                    }
                    TxtDraftStatus.Text = " • " + TxaLanguageManager.GetString("t_draft_new", "Bản dịch mới");
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
            TxtDraftStatus.Text = " • " + TxaLanguageManager.GetString("t_draft_saving", "Đang lưu nháp...");
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
            int validTranslated = TranslationItems.Count(t => !string.IsNullOrWhiteSpace(t.TranslatedText) && !t.HasPlaceholderError);
            bool hasErrors = TranslationItems.Any(t => !string.IsNullOrWhiteSpace(t.TranslatedText) && t.HasPlaceholderError);

            double percent = (double)translated / total * 100.0;

            string progressFormat = TxaLanguageManager.GetString("t_trans_progress_fmt", "Tiến độ dịch: {0} / {1} chuỗi ({2:F1}%)");
            TxtProgressSummary.Text = string.Format(progressFormat, translated, total, percent);
            PbTranslationProgress.Value = percent;

            bool isComplete = (validTranslated == total && total > 0 && !hasErrors);
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

                TxtDraftStatus.Text = " • " + TxaLanguageManager.GetString("t_draft_saved", "Đã lưu nháp tự động");
            }
            catch { }
        }

        private void BtnSaveAndApply_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTargetCulture.SelectedItem is not CultureOption selected)
            {
                string warnMsg = TxaLanguageManager.GetString("t_trans_select_lang_warning", "Vui lòng chọn một ngôn ngữ đích để lưu.");
                string warnTitle = TxaLanguageManager.GetString("t_dialog_notice", "Thông báo");
                TxaMessageBox.Show(this, warnMsg, warnTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int translatedCount = TranslationItems.Count(t => !string.IsNullOrWhiteSpace(t.TranslatedText));
            if (translatedCount == 0)
            {
                string warnMsg = TxaLanguageManager.GetString("t_trans_need_translation_warning", "Vui lòng dịch ít nhất một vài câu trước khi lưu & áp dụng.");
                string warnTitle = TxaLanguageManager.GetString("t_dialog_notice", "Thông báo");
                TxaMessageBox.Show(this, warnMsg, warnTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate all required placeholder variables
            var errorItems = TranslationItems.Where(t => !string.IsNullOrWhiteSpace(t.TranslatedText) && t.HasPlaceholderError).ToList();
            if (errorItems.Count > 0)
            {
                string keysList = string.Join("\n", errorItems.Take(6).Select(t => $"• [{t.Key}]: {t.ValidationErrorMessage}"));
                if (errorItems.Count > 6) keysList += $"\n... và {errorItems.Count - 6} chuỗi khác.";

                string warnMsg = string.Format(
                    TxaLanguageManager.GetString("t_trans_var_error_msg", "Phát hiện {0} chuỗi dịch chưa nhập đúng hoặc còn thiếu các biến định dạng:\n\n{1}\n\nVui lòng kiểm tra và điền đầy đủ các biến trước khi lưu để tránh gây lỗi hiển thị trong ứng dụng!"),
                    errorItems.Count,
                    keysList
                );
                string warnTitle = TxaLanguageManager.GetString("t_trans_var_error_title", "Cảnh Báo Biến Định Dạng");
                TxaMessageBox.Show(this, warnMsg, warnTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
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

                string successFmt = TxaLanguageManager.GetString("t_trans_save_success_fmt", "Đã lưu thành công gói ngôn ngữ {0} ({1}) và áp dụng ngay lập tức vào Steam Route Fixer!");
                string successTitle = TxaLanguageManager.GetString("t_trans_save_success_title", "Hoàn tất biên dịch");
                TxaMessageBox.Show(
                    this,
                    string.Format(successFmt, pkg.lang_name, pkg.lang_code),
                    successTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                string errFmt = TxaLanguageManager.GetString("t_trans_save_error_fmt", "Lỗi khi lưu gói ngôn ngữ: {0}");
                TxaMessageBox.Show(this, string.Format(errFmt, ex.Message), "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSubmitGithub_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTargetCulture.SelectedItem is not CultureOption selected) return;

            var errorItems = TranslationItems.Where(t => !string.IsNullOrWhiteSpace(t.TranslatedText) && t.HasPlaceholderError).ToList();
            if (errorItems.Count > 0)
            {
                string keysList = string.Join("\n", errorItems.Take(6).Select(t => $"• [{t.Key}]: {t.ValidationErrorMessage}"));
                if (errorItems.Count > 6) keysList += $"\n... và {errorItems.Count - 6} chuỗi khác.";

                string warnMsg = string.Format(
                    TxaLanguageManager.GetString("t_trans_var_error_msg", "Phát hiện {0} chuỗi dịch chưa nhập đúng hoặc còn thiếu các biến định dạng:\n\n{1}\n\nVui lòng kiểm tra và điền đầy đủ các biến trước khi lưu để tránh gây lỗi hiển thị trong ứng dụng!"),
                    errorItems.Count,
                    keysList
                );
                string warnTitle = TxaLanguageManager.GetString("t_trans_var_error_title", "Cảnh Báo Biến Định Dạng");
                TxaMessageBox.Show(this, warnMsg, warnTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string jsonBody = JsonSerializer.Serialize(new
            {
                lang_code = selected.Code,
                lang_name = selected.NativeName,
                author = string.IsNullOrWhiteSpace(TxtAuthor.Text) ? Environment.UserName : TxtAuthor.Text.Trim(),
                keys = TranslationItems.ToDictionary(t => t.Key, t => t.TranslatedText)
            }, new JsonSerializerOptions { WriteIndented = true });

            string title = $"[New Language Submission] {selected.NativeName} ({selected.Code})";
            string body = $@"### 🌐 New TXA Language Package Contribution

**Language**: {selected.DisplayName}
**Code**: `{selected.Code}`
**Author**: {TxtAuthor.Text}

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
                TxaMessageBox.Show(this, $"Không thể mở trình duyệt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            SaveDraftToDisk();
            Close();
        }
    }
}
