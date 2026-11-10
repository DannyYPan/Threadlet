using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Threadlet;
var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start();
var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
using var listener = new HttpListener(); listener.Prefixes.Add($"http://localhost:{port}/"); listener.Start();
var routes = new List<string>();
var server = Task.Run(async () => {
    for (int i = 0; i < 7; i++) {
        var ctx = await listener.GetContextAsync();
        if (ctx.Request.QueryString["password"] != "test secret") throw new Exception("Authentication encoding failed");
        var path = ctx.Request.Url!.AbsolutePath; routes.Add(path);
        using var reader = new StreamReader(ctx.Request.InputStream);
        var body = await reader.ReadToEndAsync();
        if (path.EndsWith("/message/text")) {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.GetProperty("message").GetString() != "Hello" || json.RootElement.GetProperty("chatGuid").GetString() != "chat-guid") throw new Exception("Text payload incorrect");
        }
        if (path.EndsWith("/message/attachment") && (!body.Contains("photo.png") || !body.Contains("chat-guid"))) throw new Exception("Multipart attachment payload incorrect");
                if (path.Contains("/download") && ctx.Request.QueryString["original"] is not ("true" or "false")) throw new Exception("Image conversion/original mode must be explicit");
        var response = path.Contains("/download") ? new byte[] { 1, 2, 3 } : Encoding.UTF8.GetBytes(path.EndsWith("/contact") ? "{\"data\":[{\"displayName\":\"Jamie\",\"phoneNumbers\":[{\"address\":\"+15551234567\"}]}]}" : "{\"data\":[]}");
        await ctx.Response.OutputStream.WriteAsync(response); ctx.Response.Close();
    }
});
var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
try {
    using var bridge = new Bridge($"http://localhost:{port}", "test secret");
    await bridge.Ping();
    if ((await bridge.Contacts()).Rows().Single().Text("displayName") != "Jamie") throw new Exception("Contact parsing failed");
    await bridge.Messages("chat-guid"); await bridge.Send("chat-guid", "Hello");
    var photo = Path.Combine(temp, "photo.png"); await File.WriteAllBytesAsync(photo, new byte[] { 4, 5 });
    await bridge.Upload("chat-guid", photo);
    if ((await bridge.Preview("photo-guid")).Length != 3) throw new Exception("Preview failed");
    var output = Path.Combine(temp, "saved.png"); await bridge.Download("photo-guid", output);
    if (!File.ReadAllBytes(output).SequenceEqual(new byte[] { 1, 2, 3 })) throw new Exception("Download failed");
    await server.WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine("PASS: auth, contacts, message query/send, multipart photo upload, preview, attachment download, and original attachment download.");
} finally { Directory.Delete(temp, true); }

void Check(bool condition, string description) { if (!condition) throw new Exception(description); }
Check(MessageBehavior.Receipt(true, 0, 1700000000000, 1699999999999, true).StartsWith("Read "), "Recipient read timestamp must be shown");
Check(MessageBehavior.Receipt(true, 0, 0, 1700000000000, true) == "Delivered", "Delivery must not be called read");
Check(MessageBehavior.Receipt(true, 1, 0, 0, false) == "Not Delivered", "Failed delivery must be visible");
Check(MessageBehavior.Receipt(false, 0, 1700000000000, 0, true) == "", "Incoming messages must not display our read status");
Check(MessageBehavior.ShouldNotify(true, true, true, false, false, false), "Background incoming message should notify");
Check(!MessageBehavior.ShouldNotify(true, true, true, true, false, false), "Hide Alerts must suppress notifications");
Check(!MessageBehavior.ShouldNotify(false, true, true, false, false, false), "Initial history sync must stay quiet");
Check(!MessageBehavior.ShouldNotify(true, true, true, false, true, false), "Our own messages must not notify");
Check(!MessageBehavior.ShouldNotify(true, true, true, false, false, true), "Visible conversation must stay quiet");
Check(!MessageBehavior.ShouldNotify(true, false, true, false, false, false), "Repeated poll must stay quiet");
Check(!MessageBehavior.ShouldNotify(true, true, false, false, false, false), "Disabled notifications must stay quiet");
Console.WriteLine("PASS: incoming read receipts and notification suppression policies.");