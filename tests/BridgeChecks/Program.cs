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
    for (int i = 0; i < 11; i++) {
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
        if (path.EndsWith("/unsend")) {
            using var json = JsonDocument.Parse(body);
            if (ctx.Request.HttpMethod != "POST" || json.RootElement.GetProperty("partIndex").GetInt32() != 0) throw new Exception("Undo Send must target a specific message part");
        }
        if (path.Contains("/fail/download")) ctx.Response.StatusCode = 503;
        if (path.EndsWith("/reject")) { var error = Encoding.UTF8.GetBytes("{\"status\":400,\"error\":{}}"); await ctx.Response.OutputStream.WriteAsync(error); ctx.Response.Close(); continue; }
        var response = path.Contains("/download") ? new byte[] { 1, 2, 3 } : Encoding.UTF8.GetBytes(path.EndsWith("/contact") ? "{\"data\":[{\"displayName\":\"Jamie\",\"phoneNumbers\":[{\"address\":\"+15551234567\"}]}]}" : "{\"data\":[]}");
        await ctx.Response.OutputStream.WriteAsync(response); ctx.Response.Close();
    }
});
var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()); Directory.CreateDirectory(temp);
try {
    using var bridge = new Bridge($"http://localhost:{port}", "test secret");
    await bridge.Ping();
    if ((await bridge.Contacts()).Rows().Single().Text("displayName") != "Jamie") throw new Exception("Contact parsing failed");
    await bridge.ServerInfo(); await bridge.Unsend("message-guid");
    await bridge.Messages("chat-guid"); await bridge.Send("chat-guid", "Hello");
    var photo = Path.Combine(temp, "photo.png"); await File.WriteAllBytesAsync(photo, new byte[] { 4, 5 });
    await bridge.Upload("chat-guid", photo);
    if ((await bridge.Preview("photo-guid")).Length != 3) throw new Exception("Preview failed");
    var output = Path.Combine(temp, "saved.png"); await bridge.Download("photo-guid", output);
    if (!File.ReadAllBytes(output).SequenceEqual(new byte[] { 1, 2, 3 })) throw new Exception("Download failed");
        var failed = false;
    try { await bridge.Post("reject", new { }); } catch (InvalidOperationException) { failed = true; }
    if (!failed) throw new Exception("An error envelope with HTTP 200 must still fail");
    failed = false;
    try { await bridge.Download("fail", output); } catch (InvalidOperationException) { failed = true; }
    if (!failed || !File.ReadAllBytes(output).SequenceEqual(new byte[] { 1, 2, 3 })) throw new Exception("Failed download must preserve the existing original");
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
const long now = 1700000000000;
bool Eligible(bool mine = true, string service = "iMessage", bool sent = true, long age = 1000, bool supported = true, bool retracted = false, bool simple = true) => MessageBehavior.CanUnsend(mine, service, sent, now - age, now, supported, retracted, simple);
Check(Eligible(), "Fresh outgoing iMessage must allow Undo Send");
Check(Eligible(age: 119999), "Undo Send should last until just before two minutes");
Check(!Eligible(age: 120000), "Undo Send must expire at two minutes exactly");
Check(!Eligible(age: 120001), "Expired messages must not allow Undo Send");
Check(!Eligible(age: -1), "Future timestamps must not allow Undo Send");
Check(!Eligible(mine: false), "Cannot unsend another person's message");
foreach (var service in new[] { "SMS", "MMS", "RCS", "" }) Check(!Eligible(service: service), "Non-iMessage or unknown service must hide Undo Send");
Check(!Eligible(sent: false), "Pending/failed messages must not allow Undo Send");
Check(!Eligible(supported: false), "Unknown or unavailable server capability must hide Undo Send");
Check(!Eligible(retracted: true), "An unsent message cannot be unsent again");
Check(!Eligible(simple: false), "Multipart messages require explicit part handling");
Check(MessageBehavior.IsUnread("new", false, false, null, false), "Unseen incoming message must be unread");
Check(!MessageBehavior.IsUnread("new", true, false, null, false), "Our outgoing message must not become unread");
Check(!MessageBehavior.IsUnread("same", false, false, "same", false), "Locally seen message must remain read despite stale server state");
Check(MessageBehavior.IsUnread("new", false, true, "old", false), "New message after local read must become unread");
Check(MessageBehavior.IsUnread("same", true, true, "same", true), "Manual mark unread must override normal state");
Check(!MessageBehavior.IsUnread("", false, false, null, false), "Missing message ID must not invent an unread message");
Check((await VmLauncher.Start(new Preferences())).Contains("not configured"), "Missing VM must be reported, not silently treated as started");
Check((await VmLauncher.Start(new Preferences { AutoStartVm = false })).Contains("off"), "Disabled VM startup must not start a process");
Check((await VmLauncher.Start(new Preferences { VmrunPath = @"Z:\missing\vmrun.exe", VmxPath = @"Z:\missing\mac.vmx" })).Contains("not found"), "Missing VM software must be reported");
Console.WriteLine("PASS: Undo Send service/ownership/time/capability/part boundaries, local unread state, and missing/disabled VM behavior.");
using (var fixture = JsonDocument.Parse("{\"guid\":\"iMessage;-;chat\",\"messages\":[{\"guid\":\"older\",\"dateCreated\":10},{\"guid\":\"newer\",\"dateCreated\":20}]}"))
    Check(fixture.RootElement.LastMessage().Text("guid") == "newer", "BlueBubbles messages[] schema must populate conversation previews and notifications");
using (var fixture = JsonDocument.Parse("{\"guid\":\"iMessage;-;chat\",\"lastMessage\":{\"guid\":\"legacy\"}}"))
    Check(fixture.RootElement.LastMessage().Text("guid") == "legacy", "Legacy lastMessage schema must remain supported");
Console.WriteLine("PASS: current and legacy BlueBubbles conversation response shapes.");