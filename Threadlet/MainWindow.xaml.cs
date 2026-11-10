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
public record Conversation(string Guid, string Name, string Preview)
{
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(x => char.ToUpperInvariant(x[0])));
}
public record Attachment(string Guid, string Name, string Mime = "")
{
    public bool IsImage => Mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic" }.Contains(Path.GetExtension(Name).ToLowerInvariant());
}
public record Message(string Text, string Sender, string Time, bool Mine, List<Attachment> Attachments)
{
    public string Receipt { get; init; } = "";
    public HorizontalAlignment Alignment => Mine ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Brush Bubble => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Mine ? "#7160C3" : "#2B3044"));
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
        timer.Tick += async (_, _) => await Refresh();
        Closed += (_, _) => { timer.Stop(); bridge?.Dispose(); };
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
        Chats.ItemsSource = conversations.Where(c => c.Name.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || c.Preview.Contains(Search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
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
                string receipt = "";
        if (mine)
        {
            bool Positive(string key) => long.TryParse(row.Text(key), out var value) && value > 0;
            receipt = Positive("error") ? "Not delivered" : Positive("dateRead") ? "Read" : Positive("dateDelivered") ? "Delivered" : row.Text("isSent").Equals("true", StringComparison.OrdinalIgnoreCase) ? "Sent" : "";
        }
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
            Messages.ItemsSource = rows.Rows().Reverse().Select(ParseMessage).ToList();
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
                conversations.Add(new(row.Text("guid"), row.Text("displayName") is { Length: > 0 } name ? name : participants.Length > 0 ? participants : "Conversation", row.Part("lastMessage").Text("text", "Attachment or message")));
            }
            // Avoid firing SelectionChanged on every poll when the conversation list has not changed.
            var visible = conversations.Where(c => c.Name.Contains(Search.Text, StringComparison.OrdinalIgnoreCase) || c.Preview.Contains(Search.Text, StringComparison.OrdinalIgnoreCase)).ToList();
            if (Chats.ItemsSource is not List<Conversation> old || !old.SequenceEqual(visible)) Filter();
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
        if (picker.ShowDialog() == true) { file = picker.FileName; PendingFile.Text = "Attachment: " + Path.GetFileName(file) + "   |  click + to replace"; }
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
                Composer.Clear(); file = null; PendingFile.Text = "";
            }
            else
            {
                // Clear each successful part independently so retry cannot duplicate a successful text.
                if (!string.IsNullOrWhiteSpace(text)) { await bridge.Send(chat.Guid, text); Composer.Clear(); }
                if (attachment != null) { await bridge.Upload(chat.Guid, attachment); file = null; PendingFile.Text = ""; }
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
    async void MarkRead_Click(object sender, RoutedEventArgs e)
    {
        if (bridge == null || Chats.SelectedItem is not Conversation chat) { Status.Text = "Connect a server to mark messages as read."; return; }
        try { await bridge.MarkRead(chat.Guid); Status.Text = "Conversation marked as read. Receipt sharing follows your Messages settings."; }
        catch (Exception ex) { ShowError(ex); }
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
    static TextBox AddInput(Panel panel, string text) { var input = new TextBox { Text = text, Margin = new Thickness(0, 6, 0, 12) }; panel.Children.Add(input); return input; }
    static void AddLabel(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
    Window MakeDialog(string title, out StackPanel panel)
    {
        panel = new StackPanel { Margin = new Thickness(26) };
        return new Window { Title = title, Width = 520, SizeToContent = SizeToContent.Height, MaxHeight = 760, Background = Background, Foreground = Foreground, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner };
    }
    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = MakeDialog("Connect Threadlet", out var panel);
        AddLabel(panel, "Your messages. Your server.");
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
                previews.Clear(); drafts.Clear(); activeChat = null; Composer.Clear(); file = null; PendingFile.Text = ""; conversations.Clear(); Chats.ItemsSource = null; Messages.ItemsSource = null;
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
