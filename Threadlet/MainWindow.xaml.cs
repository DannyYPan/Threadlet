using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
namespace Threadlet;
public record Conversation(string Guid, string Name, string Preview, bool Muted = false, bool Pinned = false)
{
    public string MuteLabel => Muted ? "Show Alerts" : "Hide Alerts";
    public string PinLabel => Pinned ? "Unpin" : "Pin";
    public string Indicators => (Pinned ? "Pinned  " : "") + (Muted ? "Alerts hidden" : "");
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
}
public record Attachment(string Guid, string Name, string Mime = "")
{
    public bool IsImage => Mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif" }.Contains(Path.GetExtension(Name).ToLowerInvariant());
}
public record Message(string Text, string Sender, string Time, bool Mine, List<Attachment> Attachments)
{
    public string Receipt { get; init; } = "";
    public HorizontalAlignment Alignment => Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Brush Bubble => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Mine ? "#007AFF" : "#2B3044"));
}
public partial class MainWindow : Window
{
        readonly Dictionary<string, (string Text, string? File)> drafts = new();
    string? activeChat;
    readonly Dictionary<string, string> contactNames = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, System.Windows.Media.Imaging.BitmapImage> previews = new();
    readonly SemaphoreSlim previewGate = new(3);
    string contactStatus = "Contacts not loaded";
    static string Normalize(string address) => address.Contains('@') ? address.Trim().ToLowerInvariant() : new string(address.Where(char.IsDigit).ToArray());
    string ContactName(string address) => contactNames.GetValueOrDefault(Normalize(address), address);
    readonly List<Conversation> conversations = new();
    readonly Dictionary<string, List<Message>> demo = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(5) };
    readonly DesktopNotifications notifications = new();
    readonly Dictionary<string, string> notificationBaseline = new();
    readonly Preferences preferences = Preferences.Load();
    bool notificationReady;
    bool notificationsEnabled { get => preferences.Notifications; set { preferences.Notifications = value; SavePreferences(); } }
    bool notificationPreviews { get => preferences.NotificationPreviews; set { preferences.NotificationPreviews = value; SavePreferences(); } }
    Bridge? bridge;
    string? file;
    bool refreshing, sending;
    int generation;


    string serverUrl = "http://localhost:1234";
    public MainWindow()
    {
        InitializeComponent();
        Search.ToolTip = "Search conversations";
        LoadDemo();


        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length > 0 && !sending) SetAttachment(paths[0]); };
        timer.Tick += async (_, _) => await Refresh();
        Closed += (_, _) => { timer.Stop(); notifications.Dispose(); bridge?.Dispose(); };
    }
    void LoadDemo()
    {
        conversations.Clear();
        conversations.AddRange(new[] { new Conversation("demo-friends", "The weekend crew", "Maya: Saturday sounds perfect."), new Conversation("demo-jamie", "Jamie Chen", "Did you get a chance to see it?"), new Conversation("demo-alex", "Alex Morgan", "Thanks! Catch you tomorrow.") });
        demo["demo-friends"] = new() { new("Anyone up for a little escape this weekend?", "Maya", "10:24 AM", false, new()), new("Absolutely. Coffee first, adventure second?", "You", "10:25 AM", true, new()), new("Saturday sounds perfect.", "Maya", "10:26 AM", false, new()) };
        demo["demo-jamie"] = new() { new("Did you get a chance to see it?", "Jamie", "Yesterday", false, new()) };
        demo["demo-alex"] = new() { new("Thanks! Catch you tomorrow.", "Alex", "Yesterday", false, new()) };
        Filter(); Chats.SelectedIndex = 0;
    }
    void Filter()
    {
        var selected = (Chats.SelectedItem as Conversation)?.Guid;
        Chats.ItemsSource = conversations.Where(c => c.Name.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || c.Preview.Contains(Search.Text, StringComparison.OrdinalIgnoreCase)).OrderByDescending(c => preferences.Pinned.Contains(c.Guid)).ToList();
        if (selected != null) Chats.SelectedItem = Chats.Items.Cast<Conversation>().FirstOrDefault(c => c.Guid == selected);
    }
    void Search_Changed(object sender, TextChangedEventArgs e) { if (Chats != null) Filter(); }
    async void Chat_Selected(object sender, SelectionChangedEventArgs e)
    {
        generation++;
        if (activeChat != null) drafts[activeChat] = (Composer.Text, file);
        activeChat = (Chats.SelectedItem as Conversation)?.Guid;
        var draft = activeChat != null ? drafts.GetValueOrDefault(activeChat) : default;
        Composer.Text = draft.Text ?? ""; file = draft.File;
        PendingFile.Text = file == null ? "" : "Attachment: " + Path.GetFileName(file);
        if (Chats.SelectedItem is not Conversation chat) { Messages.ItemsSource = null; return; }
        ChatTitle.Text = chat.Name;
        ChatSubtitle.Text = bridge == null ? "Demo conversation  |  no real messages are sent" : "BlueBubbles  |  synced with your Mac";
        if (bridge == null) Messages.ItemsSource = demo.GetValueOrDefault(chat.Guid);
        else { Messages.ItemsSource = null; await LoadMessages(chat, generation); }
        MessageScroll.ScrollToEnd();
    }
    Message ParseMessage(JsonElement row)
    {
        bool mine = row.Text("isFromMe") == "True" || row.Text("isFromMe") == "true";
        string time = "";
        if (long.TryParse(row.Text("dateCreated"), out var date))
            try { time = DateTimeOffset.FromUnixTimeMilliseconds(date).LocalDateTime.ToString("MMM d  |  h:mm tt"); } catch (ArgumentOutOfRangeException) { }
        var attachments = row.Part("attachments").Rows().Select(a => new Attachment(a.Text("guid"), a.Text("transferName", "Attachment"), a.Text("mimeType"))).ToList();
        long Number(string key) => long.TryParse(row.Text(key), out var value) ? value : 0;
        var receipt = MessageBehavior.Receipt(mine, Number("error"), Number("dateRead"), Number("dateDelivered"), row.Text("isSent").Equals("true", StringComparison.OrdinalIgnoreCase));
        return new(row.Text("text"), mine ? "You" : ContactName(row.Part("handle").Text("address", "Participant")), time, mine, attachments) { Receipt = receipt };
    }
    async Task LoadMessages(Conversation chat, int version)
    {
        try
        {
            var current = bridge;
            if (current == null) return;
            var rows = await current.Messages(chat.Guid);
            if (version != generation || current != bridge) return;
            var nearBottom = MessageScroll.ScrollableHeight - MessageScroll.VerticalOffset < 80;
            var ordered = rows.Rows().ToList();
            Messages.ItemsSource = ordered.AsEnumerable().Reverse().Select(ParseMessage).ToList();
            if (nearBottom) MessageScroll.ScrollToEnd();

        }
        catch (Exception ex) { ShowError(ex); }
    }
    async Task Refresh()
    {
        if (refreshing || sending || bridge == null) return;
        refreshing = true;
        try
        {
            var current = bridge;
            var rows = await current.Chats();
            if (current != bridge) return;
            conversations.Clear();
            foreach (var row in rows.Rows())
            {
                var participants = string.Join(", ", row.Part("participants").Rows().Select(p => ContactName(p.Text("address"))));
                NotifyIncoming(row);
                conversations.Add(new(row.Text("guid"), row.Text("displayName") is { Length: > 0 } name ? name : participants.Length > 0 ? participants : "Conversation", row.Part("lastMessage").Text("text", "Attachment or message"), preferences.Muted.Contains(row.Text("guid")), preferences.Pinned.Contains(row.Text("guid"))));
            }
            // Avoid firing SelectionChanged on every poll when the conversation list has not changed.
            var visible = conversations.Where(c => c.Name.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || c.Preview.Contains(Search.Text, StringComparison.OrdinalIgnoreCase)).OrderByDescending(c => preferences.Pinned.Contains(c.Guid)).ToList();
            if (Chats.ItemsSource is not List<Conversation> old || !old.SequenceEqual(visible)) Filter();
            notificationReady = true;
            if (Chats.SelectedItem is Conversation chat) await LoadMessages(chat, generation);
            Status.Text = "Connected  |  refreshed " + DateTime.Now.ToString("h:mm tt") + "  |  showing latest 100 messages";
        }
        catch (Exception ex) { ShowError(ex); }
        finally { refreshing = false; }
    }
    void ShowError(Exception ex) => Status.Text = ex is InvalidOperationException ? ex.Message : "Connection failed. Check that the server is running and reachable. Your draft has been kept.";
    void Attach_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog { Title = "Attach a photo or file" };
        if (picker.ShowDialog() == true) SetAttachment(picker.FileName);
    }
    async void Send_Click(object sender, RoutedEventArgs e) => await SendMessage();
    async void Composer_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { e.Handled = true; await SendMessage(); }
    }
    async Task SendMessage()
    {
        if (sending || Chats.SelectedItem is not Conversation chat || (string.IsNullOrWhiteSpace(Composer.Text) && file == null)) return;
        sending = true; Send.IsEnabled = false; Composer.IsReadOnly = true; Chats.IsEnabled = false;
        var text = Composer.Text; var attachment = file;
        try
        {
            if (bridge == null)
            {
                demo[chat.Guid].Add(new(text, "You", DateTime.Now.ToString("h:mm tt") + "  |  demo", true, attachment == null ? new() : new() { new(attachment, Path.GetFileName(attachment)) }));
                Messages.ItemsSource = null; Messages.ItemsSource = demo[chat.Guid];
                Composer.Clear(); file = null; PendingFile.Text = ""; PendingPreview.Source = null;
            }
            else
            {
                // Clear each successful part independently so retry cannot duplicate a successful text.
                if (!string.IsNullOrWhiteSpace(text)) { await bridge.Send(chat.Guid, text); Composer.Clear(); }
                if (attachment != null) { await bridge.Upload(chat.Guid, attachment); file = null; PendingFile.Text = ""; PendingPreview.Source = null; }
                await LoadMessages(chat, generation);
            }
            MessageScroll.ScrollToEnd();
        }
        catch (Exception ex) { ShowError(ex); }
        finally { sending = false; Send.IsEnabled = true; Composer.IsReadOnly = false; Chats.IsEnabled = true; }
    }
    async void Attachment_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not Attachment attachment) return;
        if (bridge == null) { Status.Text = "Demo attachment: " + attachment.Name; return; }
        var save = new SaveFileDialog { FileName = Path.GetFileName(attachment.Name), Title = "Save attachment" };
        if (save.ShowDialog() != true) return;
        try { await bridge.Download(attachment.Guid, save.FileName); Status.Text = "Attachment saved."; }
        catch (Exception ex) { ShowError(ex); }
    }
    async void NewChat_Click(object sender, RoutedEventArgs e)
    {
        if (bridge == null) { Status.Text = "Connect a server in settings to start a real conversation."; return; }
        var dialog = MakeDialog("New conversation", out var panel);
        AddLabel(panel, "Recipients  |  phone numbers or Apple emails, separated by commas"); var recipients = AddInput(panel, "");
        var contactSearch = AddInput(panel, ""); contactSearch.ToolTip = "Find a contact";
        var contacts = new ListBox { Height = 150, SelectionMode = SelectionMode.Extended, Background = Brushes.Transparent, BorderThickness = new Thickness(0) }; panel.Children.Add(contacts);
        void FindContacts() => contacts.ItemsSource = contactNames.Select(c => new ContactChoice(c.Value, c.Key)).Where(c => c.ToString().Contains(contactSearch.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(c => c.Name).ToList();
        contactSearch.TextChanged += (_, _) => FindContacts(); FindContacts();
        var add = new Button { Content = "Add selected recipients" }; panel.Children.Add(add);
        add.Click += (_, _) => recipients.Text = string.Join(", ", recipients.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Concat(contacts.SelectedItems.Cast<ContactChoice>().Select(c => c.Address)).Distinct(StringComparer.OrdinalIgnoreCase));
        AddLabel(panel, "First message"); var message = AddInput(panel, "");
        var send = new Button { Content = "Start conversation", Margin = new Thickness(0, 18, 0, 0) }; panel.Children.Add(send);
        send.Click += async (_, _) =>
        {
            var addresses = recipients.Text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (addresses.Length == 0 || string.IsNullOrWhiteSpace(message.Text)) return;
            send.IsEnabled = false;
            try { await bridge.NewChat(addresses, message.Text); dialog.Close(); await Refresh(); }
            catch (Exception ex) { ShowError(ex); MessageBox.Show(dialog, "Could not create the conversation. Check server connectivity and Private API support.", "Threadlet"); }
            finally { send.IsEnabled = true; }
        };
        dialog.ShowDialog();
    }
    async Task LoadContacts()
    {
        contactNames.Clear();
        try
        {
            if (bridge == null) return;
            foreach (var contact in (await bridge.Contacts()).Rows())
            {
                var name = contact.Text("displayName");
                if (string.IsNullOrWhiteSpace(name)) name = (contact.Text("firstName") + " " + contact.Text("lastName")).Trim();
                if (name.Length == 0) continue;
                foreach (var entry in contact.Part("phoneNumbers").Rows().Concat(contact.Part("emails").Rows()))
                {
                    var address = entry.ValueKind == JsonValueKind.String ? entry.GetString() ?? "" : entry.Text("address");
                    if (address.Length > 0) contactNames[Normalize(address)] = name;
                }
            }
            contactStatus = contactNames.Count == 0 ? "No contacts returned. Grant BlueBubbles Contacts permission on your Mac." : "Contacts loaded from your Mac.";
        }
        catch { contactStatus = "Contacts unavailable. Check BlueBubbles Contacts permission on your Mac."; }
    }
    async void Contacts_Click(object sender, RoutedEventArgs e)
    {
        if (bridge != null) await LoadContacts();
        var dialog = MakeDialog("Contacts", out var panel);
        AddLabel(panel, bridge == null ? "Connect BlueBubbles to see your Mac contacts." : contactStatus);
        var search = AddInput(panel, ""); search.ToolTip = "Search contacts";
        var list = new ListBox { Height = 320, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        panel.Children.Add(list);
        void Populate() => list.ItemsSource = contactNames.Select(c => c.Value + "  |  " + c.Key).Where(c => c.Contains(search.Text, StringComparison.OrdinalIgnoreCase)).OrderBy(c => c).ToList();
        search.TextChanged += (_, _) => Populate(); Populate();
        AddLabel(panel, "Use a contact's address in New conversation. Contacts are held in memory only.");
        dialog.ShowDialog();
    }
    async void Photo_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image || image.DataContext is not Attachment attachment || !attachment.IsImage) return;
        var current = bridge;
        if (current == null) return;
        await previewGate.WaitAsync();
        try
        {
            if (current != bridge) return;
            if (!previews.TryGetValue(attachment.Guid, out var bitmap))
            {
                var bytes = await current.Preview(attachment.Guid);
                using var memory = new MemoryStream(bytes);
                bitmap = new System.Windows.Media.Imaging.BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 640; bitmap.StreamSource = memory; bitmap.EndInit(); bitmap.Freeze();
                if (previews.Count >= 64) previews.Clear();
                previews[attachment.Guid] = bitmap;
            }
            if (current == bridge && Equals(image.DataContext, attachment)) image.Source = bitmap;
        }
        catch { image.ToolTip = "Preview unavailable. Use Save attachment to download the file."; }
        finally { previewGate.Release(); }
    }
    void SetAttachment(string path)
    {
        if (!File.Exists(path)) return;
        file = path; PendingFile.Text = "Attachment: " + Path.GetFileName(path);
        PendingPreview.Source = null;
        try { var bitmap = new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit(); bitmap.UriSource = new Uri(path); bitmap.DecodePixelWidth = 200; bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.EndInit(); PendingPreview.Source = bitmap; }
        catch { /* HEIC is sent unchanged; local preview depends on installed Windows codecs. */ }
    }
    void RemoveAttachment_Click(object sender, RoutedEventArgs e) { file = null; PendingFile.Text = ""; PendingPreview.Source = null; }
    async void Photo_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image { DataContext: Attachment attachment } || !attachment.IsImage) return;
        var current = bridge;
        var viewer = new Window { Title = attachment.Name, Owner = this, Width = 1000, Height = 760, MinWidth = 500, MinHeight = 400, Background = new SolidColorBrush(Color.FromRgb(16, 18, 26)), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var grid = new Grid(); grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); grid.RowDefinitions.Add(new RowDefinition());
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16) };
        var save = new Button { Content = "Save original", Tag = attachment }; save.Click += Attachment_Click; bar.Children.Add(save);
        var zoom = new Slider { Minimum = 0.25, Maximum = 3, Value = 1, Width = 160, Margin = new Thickness(18, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Zoom" }; bar.Children.Add(zoom);
        var close = new Button { Content = "Close" }; close.Click += (_, _) => viewer.Close(); bar.Children.Add(close); grid.Children.Add(bar);
        var photo = new Image { Stretch = Stretch.Uniform, Width = 800, Height = 600, Margin = new Thickness(20) };
        var scale = new ScaleTransform(1, 1); photo.LayoutTransform = scale; zoom.ValueChanged += (_, _) => { scale.ScaleX = zoom.Value; scale.ScaleY = zoom.Value; };
        var scroll = new ScrollViewer { Content = photo, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; scroll.SizeChanged += (_, _) => { photo.Width = Math.Max(100, scroll.ActualWidth - 50); photo.Height = Math.Max(100, scroll.ActualHeight - 50); }; Grid.SetRow(scroll, 1); grid.Children.Add(scroll);
        var feedback = new TextBlock { Text = "Loading photo...", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }; Grid.SetRow(feedback, 1); grid.Children.Add(feedback);
        viewer.Content = grid; viewer.KeyDown += (_, key) => { if (key.Key == Key.Escape) viewer.Close(); };
        bool open = true; viewer.Closed += (_, _) => open = false;
        viewer.Show();
        try
        {
            byte[] bytes;
            if (current == null) bytes = await File.ReadAllBytesAsync(attachment.Guid);
            else bytes = await current.Preview(attachment.Guid, large: true);
            if (!open) return;
            using var memory = new MemoryStream(bytes);
            var bitmap = new System.Windows.Media.Imaging.BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 2048; bitmap.StreamSource = memory; bitmap.EndInit(); bitmap.Freeze(); photo.Source = bitmap; feedback.Visibility = Visibility.Collapsed;
        }
        catch { if (open) feedback.Text = "Photo preview unavailable. HEIC/HEIF viewing requires server image conversion. You can still save the original."; }
    }
    static TextBox AddInput(Panel panel, string text) { var input = new TextBox { Text = text, Margin = new Thickness(0, 6, 0, 12) }; panel.Children.Add(input); return input; }
    static void AddLabel(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
    Window MakeDialog(string title, out StackPanel panel)
    {
        panel = new StackPanel { Margin = new Thickness(26) };
        return new Window { Title = title, Width = 520, SizeToContent = SizeToContent.Height, MaxHeight = 760, Background = Background, Foreground = Foreground, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
    }
    void NotifyIncoming(JsonElement row)
    {
        var chatGuid = row.Text("guid"); var last = row.Part("lastMessage"); var messageGuid = last.Text("guid");
        if (messageGuid.Length == 0 || chatGuid.Length == 0) return;
        var first = !notificationBaseline.TryGetValue(chatGuid, out var previous);
        notificationBaseline[chatGuid] = messageGuid;
        // Baseline the first sync so connecting never announces existing history.
        var viewing = IsActive && IsEnabled && WindowState != WindowState.Minimized && (Chats.SelectedItem as Conversation)?.Guid == chatGuid;
        if (!MessageBehavior.ShouldNotify(notificationReady, first || previous != messageGuid, notificationsEnabled, preferences.Muted.Contains(chatGuid), last.Text("isFromMe").Equals("true", StringComparison.OrdinalIgnoreCase), viewing)) return;
        var title = row.Text("displayName");
        if (string.IsNullOrWhiteSpace(title)) title = ContactName(last.Part("handle").Text("address", "New message"));
        var text = notificationPreviews ? last.Text("text", "Photo or attachment") : "New message";
        if (string.IsNullOrWhiteSpace(text)) text = "Photo or attachment";
        notifications.Show(title, text, () => Dispatcher.Invoke(() => {
            Show(); WindowState = WindowState.Normal; Activate();
            Search.Clear(); Filter(); Chats.SelectedItem = Chats.Items.Cast<Conversation>().FirstOrDefault(c => c.Guid == chatGuid);
        }));
    }
    void Info_Click(object sender, RoutedEventArgs e)
    {
        if (Chats.SelectedItem is not Conversation chat) return;
        var dialog = MakeDialog(chat.Name, out var panel);
        var hide = new CheckBox { Content = "Hide Alerts", IsChecked = preferences.Muted.Contains(chat.Guid), Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 10) };
        void SetMuted(bool muted) { if (muted) preferences.Muted.Add(chat.Guid); else preferences.Muted.Remove(chat.Guid); SavePreferences(); UpdateConversationFlags(); }
        hide.Checked += (_, _) => SetMuted(true); hide.Unchecked += (_, _) => SetMuted(false); panel.Children.Add(hide);
        AddLabel(panel, "Photos and attachments from the currently loaded messages");
        var photos = (Messages.ItemsSource as IEnumerable<Message> ?? Array.Empty<Message>()).SelectMany(m => m.Attachments).DistinctBy(a => a.Guid).ToList();
        if (photos.Count == 0) AddLabel(panel, "No attachments in the loaded messages.");
        var tiles = new WrapPanel(); panel.Children.Add(tiles);
        foreach (var attachment in photos)
        {
            var tile = new StackPanel { Width = 190, Margin = new Thickness(5) };
            if (attachment.IsImage)
            {
                var image = new Image { DataContext = attachment, MaxHeight = 140, Stretch = Stretch.Uniform, Cursor = Cursors.Hand, ToolTip = "Open photo" };
                image.Loaded += Photo_Loaded; image.MouseLeftButtonUp += Photo_Click; tile.Children.Add(image);
            }
            var save = new Button { Content = attachment.Name, Tag = attachment, ToolTip = "Save original attachment" }; save.Click += Attachment_Click; tile.Children.Add(save); tiles.Children.Add(tile);
        }
        dialog.ShowDialog();
    }
    void SavePreferences()
    {
        if (!preferences.Save() && Status != null) Status.Text = "Preferences changed for this session but could not be saved.";
    }
    void ChatMute_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is not Conversation chat) return;
        if (!preferences.Muted.Add(chat.Guid)) preferences.Muted.Remove(chat.Guid);
        SavePreferences(); UpdateConversationFlags();
    }
    void ChatPin_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is not Conversation chat) return;
        if (preferences.Pinned.Contains(chat.Guid)) preferences.Pinned.Remove(chat.Guid);
        else if (preferences.Pinned.Count < 9) preferences.Pinned.Add(chat.Guid);
        else { Status.Text = "You can pin up to nine conversations."; return; }
        SavePreferences(); UpdateConversationFlags();
    }
    void UpdateConversationFlags()
    {
        for (int i = 0; i < conversations.Count; i++) conversations[i] = conversations[i] with { Muted = preferences.Muted.Contains(conversations[i].Guid), Pinned = preferences.Pinned.Contains(conversations[i].Guid) };
        Filter();
    }
    void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as MenuItem)?.DataContext is Message message && !string.IsNullOrEmpty(message.Text))
            try { Clipboard.SetText(message.Text); } catch { Status.Text = "Clipboard is busy. Try again."; }
    }
    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = MakeDialog("Connect Threadlet", out var panel);
        AddLabel(panel, "Your messages. Your server.");
        var enabled = new CheckBox { Content = "Desktop notifications", IsChecked = notificationsEnabled, Foreground = Brushes.White, Margin = new Thickness(0, 10, 0, 10) };
        enabled.Checked += (_, _) => notificationsEnabled = true; enabled.Unchecked += (_, _) => notificationsEnabled = false; panel.Children.Add(enabled);
        var preview = new CheckBox { Content = "Show message text in notifications", IsChecked = notificationPreviews, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) };
        preview.Checked += (_, _) => notificationPreviews = true; preview.Unchecked += (_, _) => notificationPreviews = false; panel.Children.Add(preview);
        AddLabel(panel, "Read status from other people is shown when available. Threadlet does not send mark-read requests. Notification preferences and per-chat Hide Alerts are saved on this PC.");
        AddLabel(panel, "Enter your BlueBubbles server URL and password. Credentials are kept in memory only. Group creation and sending use the server's Private API.");
        AddLabel(panel, "Server URL (HTTPS, or localhost HTTP)"); var url = AddInput(panel, serverUrl);
        AddLabel(panel, "Server password"); var password = new PasswordBox { Margin = new Thickness(0, 6, 0, 12) }; panel.Children.Add(password);
        var feedback = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Salmon }; panel.Children.Add(feedback);
        var connect = new Button { Content = "Connect to BlueBubbles", Background = new SolidColorBrush(Color.FromRgb(116, 96, 200)) }; panel.Children.Add(connect);
        connect.Click += async (_, _) =>
        {
            Bridge? candidate = null; connect.IsEnabled = false;
            try
            {
                candidate = new Bridge(url.Text, password.Password); await candidate.Ping();
                timer.Stop(); generation++; bridge?.Dispose(); bridge = candidate; candidate = null; serverUrl = url.Text;
                previews.Clear(); notificationBaseline.Clear(); notificationReady = false; drafts.Clear(); activeChat = null; Composer.Clear(); file = null; PendingFile.Text = ""; PendingPreview.Source = null; conversations.Clear(); Chats.ItemsSource = null; Messages.ItemsSource = null;
                Connection.Text = "? Connected to your BlueBubbles server"; dialog.Close();
                await LoadContacts(); await Refresh(); if (Chats.Items.Count > 0) Chats.SelectedIndex = 0; timer.Start();
            }
            catch { feedback.Text = "Could not connect. Use HTTPS (or localhost HTTP) and check your URL, password, and server."; }
            finally { candidate?.Dispose(); connect.IsEnabled = true; }
        };
        AddLabel(panel, "VM startup is optional. The launcher reads vm-config.cmd; configure an existing VMware VM there. Threadlet does not install macOS or sign into Apple for you.");
        dialog.ShowDialog();
    }
}
