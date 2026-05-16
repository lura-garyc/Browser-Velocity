using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Data.Sqlite;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Browser_New
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<BrowserTab> Tabs = new ObservableCollection<BrowserTab>();
        private ObservableCollection<QuickItem> QuickItems = new ObservableCollection<QuickItem>();
        private string dbPath = "velocity_v2.db";

        private string customDownloadPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

        public MainWindow()
        {
            InitializeComponent();
            TabContainer.ItemsSource = Tabs;
            QuickList.ItemsSource = QuickItems;
            InitDatabase();
            LoadQuickItems();

            this.Loaded += (s, e) => {
                NewTab("about:blank");
                if (LangBox != null) LangBox.SelectedIndex = 0;
                if (PathDownloadTextBox != null) PathDownloadTextBox.Text = customDownloadPath;
            };
        }

        private void ApplyLanguage(int index)
        {
            if (TxtSettingsTitle == null || TxtExtTitle == null) return;
            if (index == 0)
            { // RU
                TxtSettingsTitle.Text = "Настройки"; BtnDevTools.Content = "Инструменты (F12)";
                BtnClearHist.Content = "Очистить историю"; BtnCloseSettings.Content = "Закрыть"; BtnAddSite.Content = "+ Добавить сайт";
                TxtExtTitle.Text = "Расширения (Для новых вкладок)"; TxtUblock.Text = "uBlock Origin (Антиреклама)";
                TxtSponsor.Text = "SponsorBlock (Пропуск интро)"; TxtJambo.Text = "Jambofy (Котики на YouTube)"; TxtLangTitle.Text = "Язык интерфейса";
                TxtDownloadTitle.Text = "Настройки загрузок"; CheckAskDownload.Content = "Всегда спрашивать путь";
                TxtCurrentFolderTitle.Text = "Папка сохранения:"; BtnChooseFolder.Content = "Изменить папку";
            }
            else if (index == 1)
            { // EN
                TxtSettingsTitle.Text = "Settings"; BtnDevTools.Content = "DevTools (F12)";
                BtnClearHist.Content = "Clear History"; BtnCloseSettings.Content = "Close"; BtnAddSite.Content = "+ Add Site";
                TxtExtTitle.Text = "Extensions (For new tabs)"; TxtUblock.Text = "uBlock Origin (AdBlock)";
                TxtSponsor.Text = "SponsorBlock (Skip Sponsor)"; TxtJambo.Text = "Jambofy (Cats on YouTube)"; TxtLangTitle.Text = "Interface Language";
                TxtDownloadTitle.Text = "Download Settings"; CheckAskDownload.Content = "Always ask for path";
                TxtCurrentFolderTitle.Text = "Save folder:"; BtnChooseFolder.Content = "Change folder";
            }
            else if (index == 2)
            { // UA
                TxtSettingsTitle.Text = "Налаштування"; BtnDevTools.Content = "Інструменти (F12)";
                BtnClearHist.Content = "Очистити історію"; BtnCloseSettings.Content = "Закрити"; BtnAddSite.Content = "+ Додати сайт";
                TxtExtTitle.Text = "Розширення (Для нових вкладок)"; TxtUblock.Text = "uBlock Origin (Антиреклама)";
                TxtSponsor.Text = "SponsorBlock (Пропуск интро)"; TxtJambo.Text = "Jambofy (Котики на YouTube)"; TxtLangTitle.Text = "Мова інтерфейсу";
                TxtDownloadTitle.Text = "Налаштування завантажень"; CheckAskDownload.Content = "Завжди питати шлях";
                TxtCurrentFolderTitle.Text = "Папка збереження:"; BtnChooseFolder.Content = "Змінити папку";
            }
        }

        private void InitDatabase()
        {
            using var c = new SqliteConnection($"Data Source={dbPath}");
            c.Open();
            new SqliteCommand("CREATE TABLE IF NOT EXISTS QuickAccess (Title TEXT, Url TEXT)", c).ExecuteNonQuery();
            new SqliteCommand("CREATE TABLE IF NOT EXISTS History (Title TEXT, Url TEXT, Time TEXT)", c).ExecuteNonQuery();
        }

        private void LoadQuickItems()
        {
            QuickItems.Clear();
            using var c = new SqliteConnection($"Data Source={dbPath}");
            c.Open();
            var r = new SqliteCommand("SELECT * FROM QuickAccess", c).ExecuteReader();
            while (r.Read())
                QuickItems.Add(new QuickItem { Title = r["Title"].ToString()!, Url = r["Url"].ToString()! });
        }

        // ЗАГРУЗКА РАСШИРЕНИЙ (SPONSORBLOCK И JAMBOFY)
        private async Task LoadExtensions(CoreWebView2Profile profile)
        {
            string extBase = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Extensions");

            if (!System.IO.Directory.Exists(extBase)) return;

            try
            {
                if (CheckSponsor.IsChecked == true)
                {
                    string path = System.IO.Path.Combine(extBase, "SponsorBlock");
                    if (System.IO.Directory.Exists(path))
                    {
                        await profile.AddBrowserExtensionAsync(path);
                    }
                }

                if (CheckJambo.IsChecked == true)
                {
                    string path = System.IO.Path.Combine(extBase, "Jambofy");
                    if (System.IO.Directory.Exists(path))
                    {
                        await profile.AddBrowserExtensionAsync(path);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки расширения: " + ex.Message);
            }
        }

        // ВСТРОЕННЫЙ СКРИПТ-АДБЛОК ДЛЯ ЗАМЕНЫ UBLOCK
        private async Task InjectAdBlocker(CoreWebView2 coreWebView2)
        {
            if (CheckAdBlock.IsChecked != true) return;

            string adBlockScript = @"
                (function() {
                    const adSelectors = [
                        '.video-ads', '.ytp-ad-module', '.ytp-ad-image-overlay', 
                        '#player-ads', '#masthead-ad', 'ytd-promoted-video-renderer',
                        '#ad-text', '.ad-container', '.ad-image', '[id^=""div-gpt-ad""]',
                        '.main-ad-wrapper', '.banner-ad', '.adsbygoogle', 'ytd-display-ad-renderer'
                    ];

                    function removeAds() {
                        adSelectors.forEach(selector => {
                            document.querySelectorAll(selector).forEach(el => el.remove());
                        });

                        const video = document.querySelector('video');
                        const skipButton = document.querySelector('.ytp-ad-skip-button, .ytp-ad-skip-button-modern');
                        if (skipButton) {
                            skipButton.click();
                        }
                        if (video && document.querySelector('.ad-showing')) {
                            video.currentTime = video.duration - 0.1;
                        }
                    }

                    removeAds();
                    setInterval(removeAds, 300);
                })();
            ";

            await coreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(adBlockScript);
        }

        private async void NewTab(string url)
        {
            var webView = new WebView2();
            var tab = new BrowserTab { Browser = webView, Title = "Загрузка..." };
            Tabs.Add(tab);
            TabContainer.SelectedItem = tab;

            webView.CoreWebView2InitializationCompleted += async (s, e) =>
            {
                if (e.IsSuccess)
                {
                    await InjectAdBlocker(webView.CoreWebView2);
                    await LoadExtensions(webView.CoreWebView2.Profile);

                    this.Dispatcher.Invoke(() => {
                        webView.Source = new Uri(url);
                    });
                }
                else
                {
                    MessageBox.Show("Ошибка инициализации WebView2: " + e.InitializationException?.Message);
                }
            };

            var envOptions = new CoreWebView2EnvironmentOptions();
            envOptions.AreBrowserExtensionsEnabled = true;
            envOptions.AdditionalBrowserArguments = "--enable-features=WebView2Extensions";

            string userDataFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "VelocityProfile");
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, envOptions);

            await webView.EnsureCoreWebView2Async(environment);

            // Обработка загрузок
            webView.CoreWebView2.DownloadStarting += (s, e) =>
            {
                e.Handled = true;
                if (CheckAskDownload.IsChecked == true)
                {
                    e.ResultFilePath = "";
                }
                else
                {
                    string fileName = System.IO.Path.GetFileName(e.ResultFilePath);
                    e.ResultFilePath = System.IO.Path.Combine(customDownloadPath, fileName);
                }
            };

            webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
            {
                this.Dispatcher.Invoke(() => {
                    string newTitle = webView.CoreWebView2.DocumentTitle;
                    tab.Title = string.IsNullOrWhiteSpace(newTitle) ? "Velocity" : newTitle;
                });
            };

            webView.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
            {
                this.Dispatcher.Invoke(() => {
                    if (webView.CoreWebView2.ContainsFullScreenElement)
                    {
                        if (RootGrid != null) { RootGrid.RowDefinitions[0].Height = new GridLength(0); RootGrid.RowDefinitions[1].Height = new GridLength(0); }
                        this.WindowStyle = WindowStyle.None; this.WindowState = WindowState.Maximized; this.ResizeMode = ResizeMode.NoResize; this.Topmost = true;
                    }
                    else
                    {
                        if (RootGrid != null) { RootGrid.RowDefinitions[0].Height = GridLength.Auto; RootGrid.RowDefinitions[1].Height = new GridLength(65); }
                        this.WindowStyle = WindowStyle.SingleBorderWindow; this.WindowState = WindowState.Normal; this.ResizeMode = ResizeMode.CanResize; this.Topmost = false;
                    }
                });
            };

            // ЛОГИКА ОТОБРАЖЕНИЯ ДОМЕНА И ОЧИСТКИ В КЛАДКАХ
            webView.NavigationCompleted += (s, e) => {
                string cur = webView.Source?.ToString() ?? "about:blank";
                bool isHome = cur == "about:blank" || cur.StartsWith("about:blank");

                QuickPanel.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;
                BrowserHost.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;

                if (isHome)
                {
                    UrlTextBox.Text = ""; // Если мы на главном меню — полностью убираем домен/текст
                }
                else
                {
                    try
                    {
                        // Если мы на сайте — вытаскиваем только чистый домен (например: google.com)
                        var uri = new Uri(cur);
                        UrlTextBox.Text = uri.Host;
                    }
                    catch
                    {
                        UrlTextBox.Text = cur; // На всякий случай, если ссылка будет нестандартной
                    }

                    // Запись в историю
                    using var c = new SqliteConnection($"Data Source={dbPath}"); c.Open();
                    var cmd = new SqliteCommand("INSERT INTO History VALUES (@t, @u, @time)", c);
                    cmd.Parameters.AddWithValue("@t", tab.Title); cmd.Parameters.AddWithValue("@u", cur);
                    cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("HH:mm")); cmd.ExecuteNonQuery();
                }
            };
        }

        private void ChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            dialog.Title = "Выберите папку для загрузок Velocity";
            if (dialog.ShowDialog() == true)
            {
                customDownloadPath = dialog.FolderName;
                PathDownloadTextBox.Text = customDownloadPath;
            }
        }

        private void CheckAskDownload_Changed(object sender, RoutedEventArgs e)
        {
            if (BtnChooseFolder == null || PathDownloadTextBox == null) return;
            bool ask = CheckAskDownload.IsChecked == true;
            BtnChooseFolder.IsEnabled = !ask;
            PathDownloadTextBox.IsEnabled = !ask;
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BrowserTab tab && Tabs.Count > 1)
            {
                tab.Browser.Source = new Uri("about:blank");
                tab.Browser.Dispose();
                Tabs.Remove(tab);
            }
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            using (var c = new SqliteConnection($"Data Source={dbPath}")) { c.Open(); new SqliteCommand("DELETE FROM History", c).ExecuteNonQuery(); }
            if (TabContainer.SelectedItem is BrowserTab t && t.Browser.CoreWebView2 != null) { await t.Browser.CoreWebView2.Profile.ClearBrowsingDataAsync(); t.Browser.Reload(); }
            MessageBox.Show("История и кэш очищены!");
        }

        private void Go_Click(object sender, RoutedEventArgs e)
        {
            if (TabContainer.SelectedItem is BrowserTab t)
            {
                string u = UrlTextBox.Text;
                if (string.IsNullOrWhiteSpace(u)) return;
                if (!u.Contains(".") || u.Contains(" ")) u = "https://google.com/search?q=" + u;
                else if (!u.StartsWith("http")) u = "https://" + u;
                t.Browser.Source = new Uri(u);
            }
        }

        // ОБНОВЛЕНИЕ ТЕКСТА ПРИ ПЕРЕКЛЮЧЕНИИ МЕЖДУ ВКЛАДКАМИ
        private void TabContainer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabContainer.SelectedItem is BrowserTab t && t.Browser != null)
            {
                BrowserHost.Children.Clear(); BrowserHost.Children.Add(t.Browser);
                string url = t.Browser.Source?.ToString() ?? "about:blank";
                bool isHome = url == "about:blank" || url.StartsWith("about:blank");

                QuickPanel.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;
                BrowserHost.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;

                if (isHome)
                {
                    UrlTextBox.Text = ""; // Убираем домен на главной
                }
                else
                {
                    try { UrlTextBox.Text = new Uri(url).Host; } // Показываем домен при переключении на сайт
                    catch { UrlTextBox.Text = url; }
                }
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) { if (TabContainer.SelectedItem is BrowserTab t && t.Browser.CanGoBack) t.Browser.GoBack(); }
        private void Forward_Click(object sender, RoutedEventArgs e) { if (TabContainer.SelectedItem is BrowserTab t && t.Browser.CanGoForward) t.Browser.GoForward(); }
        private void Reload_Click(object sender, RoutedEventArgs e) => (TabContainer.SelectedItem as BrowserTab)?.Browser.Reload();
        private void NewTab_Click(object sender, RoutedEventArgs e) => NewTab("about:blank");
        private void UrlTextBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Go_Click(null!, null!); }

        private void DevTools_Click(object sender, RoutedEventArgs e)
        {
            if (TabContainer.SelectedItem is BrowserTab t)
            {
                if (DevCol.Width.Value == 0) { DevCol.Width = new GridLength(450); t.Browser.CoreWebView2.OpenDevToolsWindow(); }
                else DevCol.Width = new GridLength(0);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.F12) DevTools_Click(null!, null!); }
        private void Settings_Click(object sender, RoutedEventArgs e) { SettingsPanel.Visibility = SettingsPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
        private void QuickList_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (QuickList.SelectedItem is QuickItem i && TabContainer.SelectedItem is BrowserTab t) t.Browser.Source = new Uri(i.Url); }

        private void DeleteQuickItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is QuickItem item)
            {
                using var c = new SqliteConnection($"Data Source={dbPath}"); c.Open();
                var cmd = new SqliteCommand("DELETE FROM QuickAccess WHERE Title = @t AND Url = @u", c);
                cmd.Parameters.AddWithValue("@t", item.Title); cmd.Parameters.AddWithValue("@u", item.Url);
                cmd.ExecuteNonQuery(); QuickItems.Remove(item);
            }
        }

        private void AddQuickBtn_Click(object sender, RoutedEventArgs e)
        {
            string t = Microsoft.VisualBasic.Interaction.InputBox("Название:", "Velocity", "YouTube");
            string u = Microsoft.VisualBasic.Interaction.InputBox("URL:", "Velocity", "https://");
            if (!string.IsNullOrEmpty(u))
            {
                if (!u.StartsWith("http")) u = "https://" + u;
                using var c = new SqliteConnection($"Data Source={dbPath}"); c.Open();
                var cmd = new SqliteCommand("INSERT INTO QuickAccess VALUES (@t, @u)", c);
                cmd.Parameters.AddWithValue("@t", t); cmd.Parameters.AddWithValue("@u", u);
                cmd.ExecuteNonQuery(); LoadQuickItems();
            }
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            Window histWin = new Window { Title = "History", Width = 400, Height = 500, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            ListBox lb = new ListBox { Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(10) };
            using var c = new SqliteConnection($"Data Source={dbPath}"); c.Open();
            var r = new SqliteCommand("SELECT * FROM History ORDER BY rowid DESC LIMIT 50", c).ExecuteReader();
            while (r.Read()) lb.Items.Add($"[{r["Time"]}] {r["Title"]}");
            histWin.Content = lb; histWin.ShowDialog();
        }

        private void LangBox_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (this.IsLoaded) ApplyLanguage(LangBox.SelectedIndex); }
    }

    public class BrowserTab : INotifyPropertyChanged
    {
        private string _title = "Velocity";
        public string Title { get => _title; set { _title = value; OnPropertyChanged(nameof(Title)); } }
        public WebView2 Browser { get; set; } = null!;
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class QuickItem
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Favicon => (Url != null && Url.Contains("://")) ? $"https://www.google.com/s2/favicons?sz=64&domain={new Uri(Url).Host}" : "https://www.google.com/s2/favicons?sz=64&domain=google.com";
    }
}