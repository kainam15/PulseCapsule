using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PulseCapsule;
using PulseCapsule.Core;
using PulseCapsule.Services;
using PulseCapsule.UI;

static class HoneSettingsChecks
{
    public static void Run(App app, Action<string, bool> check, string directory)
    {
        var original = app.Settings;
        var previousCredentials = app.Credentials;
        var credentials = new CredentialStore("hone-form-checks-" + Guid.NewGuid().ToString("N"));
        typeof(App).GetProperty("Credentials")!.SetValue(app, credentials);
        var api = ProviderConfig.Create(ProviderType.Hone) with { Id = "hone-form-api", Enabled = false };
        var wallet = ProviderConfig.Create(ProviderType.HoneWallet) with { Id = "hone-form-wallet", BaseUrl = "https://HONE.vvvv.ee:443/v1/" };
        var other = ProviderConfig.Create(ProviderType.Hone) with { Id = "hone-other-site", BaseUrl = "https://other.example.test" };
        app.SaveSettings(new AppSettings { Providers = [api, wallet, other, ProviderConfig.Create(ProviderType.Codex)], Capsules = [new() { Id = "clock" }] });
        var window = new SettingsWindow(app);
        var credentialIds = new HashSet<string> { wallet.Id, api.Id };
        T Find<T>(string name) where T : class => (T)window.FindName(name);
        HoneConnectionEditor[] Editors() => Find<StackPanel>("HoneConnections").Children.Cast<HoneConnectionEditor>().ToArray();
        HoneConnectionEditor Service(ProviderType type) => Editors().Single(e => e.Original.Type == type);
        T Field<T>(ProviderType type, string name) where T : class => (T)Service(type).FindName(name);
        void Click(string name) => Find<ButtonBase>(name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        string Snapshot() => JsonSerializer.Serialize(app.Store.Load());
        void Render(string name, int scale, double height)
        {
            var root = (Grid)window.Content; root.Background = (Brush)app.Resources["Canvas"];
            root.Measure(new Size(694, height)); root.Arrange(new Rect(0, 0, 694, height)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(694 * scale, (int)(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            bitmap.Render(root); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(Path.Combine(directory, name + ".png")); encoder.Save(output);
        }
        try
        {
            var before = Snapshot();
            check("Hone source list merges equivalent wallet/API sites and separates other sites", Find<ListBox>("ProviderList").Items.Count == 3
                && Find<TextBlock>("TypeTitle").Text == "Hone" && Editors().Length == 2);
            check("Hone shows both credentials on one page and one add-source type", Editors().Select(e => e.ServiceName).SequenceEqual(new[] { "账户钱包", "API key" })
                && Find<StackPanel>("StandardFields").Visibility == Visibility.Collapsed
                && Find<ComboBox>("AddType").Items.Cast<string>().Count(n => n.StartsWith("Hone")) == 1);
            Render("hone-account-minimum", 1, 502); Render("hone-account-standard", 1, 682); Render("hone-account-200-render", 2, 682);
            check("opening Hone is read-only and both old source IDs select the same page", Snapshot() == before && window.SelectProvider(api.Id)
                && Editors().Length == 2 && window.SelectProvider(wallet.Id) && Editors().Length == 2);
            Click("SaveButton");
            check("unchanged Hone save preserves provider IDs URLs enabled flags and options", Snapshot() == before);
            Field<TextBox>(ProviderType.HoneWallet, "RefreshMinutes").Text = "11";
            Field<TextBox>(ProviderType.Hone, "LowThreshold").Text = "9";
            Directory.CreateDirectory(app.Store.FilePath + ".tmp");
            Click("SaveButton");
            check("failed Hone save preserves every source and retains form edits", Snapshot() == before && Find<TextBlock>("UnsavedMarker").Visibility == Visibility.Visible
                && Field<TextBox>(ProviderType.HoneWallet, "RefreshMinutes").Text == "11");
            Directory.Delete(app.Store.FilePath + ".tmp");
            Click("SaveButton");
            check("one save persists both Hone forms without enabling a disabled member", app.Settings.Providers.Single(p => p.Id == wallet.Id).RefreshMinutes == 11
                && app.Settings.Providers.Single(p => p.Id == api.Id).LowThreshold == 9 && !app.Settings.Providers.Single(p => p.Id == api.Id).Enabled);
            before = Snapshot();
            Field<TextBox>(ProviderType.Hone, "RefreshMinutes").Text = "bad";
            Field<TextBox>(ProviderType.HoneWallet, "ConnectionName").Text = "未保存钱包名称";
            Click("SaveButton");
            check("invalid second Hone connection prevents saving the first and reveals its field", Snapshot() == before
                && Field<Expander>(ProviderType.Hone, "Advanced").IsExpanded && FieldFeedback.GetError(Field<TextBox>(ProviderType.Hone, "RefreshMinutes")).Contains("1–1440"));
            Click("DiscardButton");
            Click("AddHoneApi");
            check("adding a Hone connection is a draft on the same page", Editors().Length == 3 && Snapshot() == before);
            Click("DiscardButton");
            check("discard removes added connection and resets the entire Hone form", Editors().Length == 2 && Snapshot() == before
                && Field<TextBox>(ProviderType.HoneWallet, "ConnectionName").Text == wallet.Name);
            credentials.Write(wallet.Id, "fixture-wallet-token"); credentials.Write(api.Id, "fixture-api-key");
            window.SelectProvider(api.Id);
            check("stored wallet and API secrets are never loaded into password controls", Editors().All(e => ((PasswordBox)e.FindName("SecretInput")).Password == ""));
            Find<TextBox>("HoneBaseUrl").Text = "https://changed.example.test";
            Click("SaveButton");
            check("changing shared site requires matching credentials before any save", Snapshot() == before && Find<TextBlock>("StatusText").Text.Contains("接口地址已改变"));
            Click("DiscardButton");
            Field<PasswordBox>(ProviderType.HoneWallet, "SecretInput").Password = "fixture-new-wallet";
            Field<PasswordBox>(ProviderType.Hone, "SecretInput").Password = new string('x', 1400);
            Click("SaveButton");
            check("second credential failure leaves both original secrets and all config intact", Snapshot() == before
                && credentials.Read(wallet.Id) == "fixture-wallet-token" && credentials.Read(api.Id) == "fixture-api-key");
            Field<PasswordBox>(ProviderType.Hone, "SecretInput").Password = "fixture-new-api";
            Directory.CreateDirectory(app.Store.FilePath + ".tmp"); Click("SaveButton");
            check("configuration failure after credential staging keeps the original credential pair", Snapshot() == before
                && credentials.Read(wallet.Id) == "fixture-wallet-token" && credentials.Read(api.Id) == "fixture-api-key");
            Directory.Delete(app.Store.FilePath + ".tmp"); Click("SaveButton");
            foreach (var editor in Editors()) credentialIds.Add(editor.Original.Id);
            check("successful Hone credential update keeps token and API key in separate entries", credentials.Read(Service(ProviderType.HoneWallet).Original.Id) == "fixture-new-wallet"
                && credentials.Read(Service(ProviderType.Hone).Original.Id) == "fixture-new-api" && credentials.Read(wallet.Id) is null && credentials.Read(api.Id) is null);
            check("editing Hone leaves other suppliers unchanged", app.Settings.Providers.Single(p => p.Id == other.Id) == other);
            window.SelectProvider(other.Id); before = Snapshot();
            check("API-only Hone keeps wallet connection available without creating it", Editors().Length == 1 && Find<Button>("AddHoneWallet").Visibility == Visibility.Visible);
            Click("AddHoneWallet"); Click("SaveButton");
            check("adding a wallet persists one new member under the existing Hone entry", Editors().Length == 2 && Find<ListBox>("ProviderList").Items.Count == 3
                && app.Settings.Providers.Count == 5 && app.Settings.Providers.Single(p => p.Id == other.Id).BaseUrl == other.BaseUrl);
            Find<ComboBox>("AddType").SelectedIndex = 0; Click("AddButton");
            var countBeforeDraft = app.Settings.Providers.Count;
            check("new Hone is an isolated draft until saved", Editors().Length == 1 && Find<ListBox>("ProviderList").Items.Count == 4);
            Click("DiscardButton");
            check("discarding a new Hone leaves existing groups intact", app.Settings.Providers.Count == countBeforeDraft && Find<ListBox>("ProviderList").Items.Count == 3);
        }
        finally
        {
            typeof(SettingsWindow).GetField("formDirty", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, false);
            window.Close();
            foreach (var id in credentialIds) credentials.Delete(id);
            if (Directory.Exists(app.Store.FilePath + ".tmp")) Directory.Delete(app.Store.FilePath + ".tmp");
            app.SaveSettings(original);
            typeof(App).GetProperty("Credentials")!.SetValue(app, previousCredentials);
        }
    }
}
