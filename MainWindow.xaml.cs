using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Data.Sqlite;

namespace Browser_New
{
    public partial class MainWindow : Window
    {
        private ObservableCollection<BrowserTab> Tabs = new ObservableCollection<BrowserTab>();
        private ObservableCollection<QuickItem> QuickItems = new ObservableCollection<QuickItem>();
        private string dbPath = "velocity_v2.db";

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
            };
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

        private void DeleteQuickItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is QuickItem item)
            {
                using (var c = new SqliteConnection($"Data Source={dbPath}"))
                {
                    c.Open();
                    var cmd = new SqliteCommand("DELETE FROM QuickAccess WHERE Title = @t AND Url = @u", c);
                    cmd.Parameters.AddWithValue("@t", item.Title);
                    cmd.Parameters.AddWithValue("@u", item.Url);
                    cmd.ExecuteNonQuery();
                }
                QuickItems.Remove(item);
            }
        }

        private void LangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this.IsLoaded) ApplyLanguage(LangBox.SelectedIndex);
        }

        private void ApplyLanguage(int index)
        {
            if (TxtSettingsTitle == null) return;
            if (index == 0)
            { // RU
                TxtSettingsTitle.Text = "Настройки"; BtnDevTools.Content = "Инструменты (F12)";
                BtnClearHist.Content = "Очистить историю"; BtnCloseSettings.Content = "Закрыть"; BtnAddSite.Content = "+ Добавить сайт";
            }
            else if (index == 1)
            { // EN
                TxtSettingsTitle.Text = "Settings"; BtnDevTools.Content = "DevTools (F12)";
                BtnClearHist.Content = "Clear History"; BtnCloseSettings.Content = "Close"; BtnAddSite.Content = "+ Add Site";
            }
            else if (index == 2)
            { // UA
                TxtSettingsTitle.Text = "Налаштування"; BtnDevTools.Content = "Інструменти (F12)";
                BtnClearHist.Content = "Очистити історію"; BtnCloseSettings.Content = "Закрити"; BtnAddSite.Content = "+ Додати сайт";
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                SettingsPanel.Visibility = Visibility.Collapsed;
                BrowserHost.IsHitTestVisible = true;
                DevCol.Width = new GridLength(0);
            }
            else
            {
                SettingsPanel.Visibility = Visibility.Visible;
                BrowserHost.IsHitTestVisible = false;
            }
        }

        private void History_Click(object sender, RoutedEventArgs e)
        {
            Window histWin = new Window { Title = "History", Width = 400, Height = 500, Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)), Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            ListBox lb = new ListBox { Background = Brushes.Transparent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Margin = new Thickness(10) };
            using var c = new SqliteConnection($"Data Source={dbPath}");
            c.Open();
            var r = new SqliteCommand("SELECT * FROM History ORDER BY rowid DESC LIMIT 50", c).ExecuteReader();
            while (r.Read()) lb.Items.Add($"[{r["Time"]}] {r["Title"]}");
            histWin.Content = lb;
            histWin.ShowDialog();
        }

        private async void NewTab(string url)
        {
            var webView = new WebView2();
            var tab = new BrowserTab { Browser = webView };
            Tabs.Add(tab);
            TabContainer.SelectedItem = tab;
            await webView.EnsureCoreWebView2Async();
            webView.NavigationCompleted += (s, e) => {
                string cur = webView.Source?.ToString() ?? "about:blank";
                bool isHome = cur == "about:blank" || cur.StartsWith("about:blank");
                QuickPanel.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;
                BrowserHost.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;
                tab.Title = isHome ? "Velocity" : (webView.CoreWebView2?.DocumentTitle ?? "Velocity");
                if (!isHome)
                {
                    using var c = new SqliteConnection($"Data Source={dbPath}"); c.Open();
                    var cmd = new SqliteCommand("INSERT INTO History VALUES (@t, @u, @time)", c);
                    cmd.Parameters.AddWithValue("@t", tab.Title); cmd.Parameters.AddWithValue("@u", cur);
                    cmd.Parameters.AddWithValue("@time", DateTime.Now.ToString("HH:mm")); cmd.ExecuteNonQuery();
                }
            };
            webView.Source = new Uri(url);
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

        private void QuickList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (QuickList.SelectedItem is QuickItem i && TabContainer.SelectedItem is BrowserTab t)
                t.Browser.Source = new Uri(i.Url);
        }

        private void TabContainer_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabContainer.SelectedItem is BrowserTab t && t.Browser != null)
            {
                BrowserHost.Children.Clear(); BrowserHost.Children.Add(t.Browser);
                string url = t.Browser.Source?.ToString() ?? "about:blank";
                bool isHome = url == "about:blank" || url.StartsWith("about:blank");
                QuickPanel.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;
                BrowserHost.Visibility = isHome ? Visibility.Collapsed : Visibility.Visible;
                UrlTextBox.Text = isHome ? "" : url;
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e) { if (TabContainer.SelectedItem is BrowserTab t && t.Browser.CanGoBack) t.Browser.GoBack(); }
        private void Forward_Click(object sender, RoutedEventArgs e) { if (TabContainer.SelectedItem is BrowserTab t && t.Browser.CanGoForward) t.Browser.GoForward(); }
        private void Reload_Click(object sender, RoutedEventArgs e) => (TabContainer.SelectedItem as BrowserTab)?.Browser.Reload();
        private void Home_Click(object sender, RoutedEventArgs e) { if (TabContainer.SelectedItem is BrowserTab t) t.Browser.Source = new Uri("about:blank"); }
        private void NewTab_Click(object sender, RoutedEventArgs e) => NewTab("about:blank");

        // ИСПРАВЛЕННЫЙ МЕТОД ЗАКРЫТИЯ ВКЛАДКИ
        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is BrowserTab tab && Tabs.Count > 1)
                Tabs.Remove(tab);
        }

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
        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            using var c = new SqliteConnection($"Data Source={dbPath}"); c.Open();
            new SqliteCommand("DELETE FROM History", c).ExecuteNonQuery(); MessageBox.Show("История очищена!");
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
    }

    public class BrowserTab { public string Title { get; set; } = "Velocity"; public WebView2 Browser { get; set; } = null!; }
    public class QuickItem { public string Title { get; set; } = ""; public string Url { get; set; } = ""; public string Favicon => (Url != null && Url.Contains("://")) ? $"https://www.google.com/s2/favicons?sz=64&domain={new Uri(Url).Host}" : "https://www.google.com/s2/favicons?sz=64&domain=google.com"; }
}