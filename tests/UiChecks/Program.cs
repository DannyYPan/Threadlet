using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Threadlet;
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new App { LaunchWindowOnStartup = false }; app.InitializeComponent(); app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var failed = false;
        app.Dispatcher.BeginInvoke(new Action(async () => {
            var temp = Path.Combine(Path.GetTempPath(), "Threadlet-Ui-" + Guid.NewGuid()); Directory.CreateDirectory(temp);
            var window = new MainWindow(new Preferences { StoragePath = Path.Combine(temp, "preferences.json"), AutoStartVm = false }) { ShowActivated = false, ShowInTaskbar = false, Left = -20000, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual };
            try
            {
                window.Show(); window.UpdateLayout();
                var path = Path.Combine(temp, "photo.png");
                var pixels = new byte[240 * 160 * 4];
                for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = 150; pixels[i + 1] = (byte)((i / 4 / 240) + 70); pixels[i + 2] = 70; pixels[i + 3] = 255; }
                var source = BitmapSource.Create(240, 160, 96, 96, PixelFormats.Bgra32, null, pixels, 240 * 4);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using (var output = File.Create(path)) encoder.Save(output);
                Invoke(window, "SetAttachment", path);
                if (((Image)window.FindName("PendingPreview")).Source == null) throw new Exception("Composer photo preview missing");
                ((TextBox)window.FindName("Composer")).Text = "Photo regression check";
                await (Task)Invoke(window, "SendMessage")!;
                await Task.Delay(300); window.UpdateLayout();
                var photos = Descendants(window).OfType<Image>().Where(i => i.DataContext is Attachment a && a.Guid == path).ToList();
                if (photos.Count == 0 || photos.All(i => i.Source == null)) throw new Exception("Sent photo is missing from conversation history");
                using (var fixture = System.Text.Json.JsonDocument.Parse("{\"guid\":\"live-message\",\"text\":\"Hello\",\"dateCreated\":1700000000000,\"dateDelivered\":1700000000001,\"isFromMe\":true,\"error\":0,\"itemType\":0,\"partCount\":1,\"chats\":[{\"guid\":\"iMessage;-;chat\"}]}"))
                {
                    var parsed = (Message)Invoke(window, "ParseMessage", fixture.RootElement)!;
                    if (parsed.Service != "iMessage" || !parsed.Sent || !parsed.SimplePart) throw new Exception("Live serializer shape does not identify a delivered iMessage correctly");
                }
                var messages = ((ItemsControl)window.FindName("Messages")).ItemsSource.Cast<Message>().ToList();
                var sent = messages.Last();
                if (!(bool)Invoke(window, "CanUnsend", sent)!) throw new Exception("Fresh demo iMessage should allow Undo Send");
                if ((bool)Invoke(window, "CanUnsend", sent with { Service = "SMS" })!) throw new Exception("SMS must hide Undo Send");
                if ((bool)Invoke(window, "CanUnsend", sent with { Created = DateTimeOffset.UtcNow.AddMinutes(-3).ToUnixTimeMilliseconds() })!) throw new Exception("Expired message must hide Undo Send");
                var list = (ListBox)window.FindName("Chats");
                var selected = (Conversation)list.SelectedItem;
                var menu = new MenuItem { DataContext = selected with { Unread = false } };
                Invoke(window, "ChatRead_Click", menu, new RoutedEventArgs());
                if (!list.Items.Cast<Conversation>().Any(c => c.Guid == selected.Guid && c.Unread)) throw new Exception("Mark unread did not add unread state");
                Invoke(window, "Filter_Click", window, new RoutedEventArgs());
                if (list.Items.Cast<Conversation>().Any(c => !c.Unread)) throw new Exception("Unread filter includes read chats");
                Invoke(window, "Filter_Click", window, new RoutedEventArgs());
                var restore = new MenuItem { DataContext = list.Items.Cast<Conversation>().First(c => c.Guid == selected.Guid) };
                Invoke(window, "ChatRead_Click", restore, new RoutedEventArgs());
                list.SelectedItem = list.Items.Cast<Conversation>().First(c => c.Guid == selected.Guid);
                await Task.Delay(100); window.UpdateLayout();
                if (args.Length > 0)
                {
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(window);
                    var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!); using var output = File.Create(args[0]); png.Save(output);
                }
                var expiredMenu = new ContextMenu { DataContext = sent with { Created = DateTimeOffset.UtcNow.AddMinutes(-3).ToUnixTimeMilliseconds() } };
                Invoke(window, "MessageMenu_Opened", expiredMenu, new RoutedEventArgs());
                if (expiredMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Undo Send")).Visibility != Visibility.Collapsed) throw new Exception("Expired Undo Send menu item is visible");
                var smsMenu = new ContextMenu { DataContext = sent with { Service = "SMS" } };
                Invoke(window, "MessageMenu_Opened", smsMenu, new RoutedEventArgs());
                if (smsMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Undo Send")).Visibility != Visibility.Collapsed) throw new Exception("SMS Undo Send menu item is visible");
                var undoMenu = new ContextMenu { DataContext = sent };
                Invoke(window, "MessageMenu_Opened", undoMenu, new RoutedEventArgs());
                var undo = undoMenu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Undo Send"));
                if (undo.Visibility != Visibility.Visible) throw new Exception("Fresh Undo Send menu item is hidden");
                undo.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); await Task.Delay(50);
                if (!((ItemsControl)window.FindName("Messages")).ItemsSource.Cast<Message>().Any(m => m.Id == sent.Id && m.Retracted && m.Attachments.Count == 0)) throw new Exception("Undo Send did not remove the demo photo");
                Console.WriteLine("PASS: running WPF UI renders sent photos, marks/filters unread chats, hides expired/SMS Undo Send, and unsends a demo message.");
            }
            catch (Exception ex) { failed = true; Console.Error.WriteLine(ex); }
            finally { window.Close(); Directory.Delete(temp, true); app.Shutdown(); }
        }));
        app.Run(); return failed ? 1 : 0;
    }
    static object? Invoke(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, args);
    static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
}
