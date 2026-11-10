using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
namespace Threadlet;
public sealed class Bridge : IDisposable
{
    readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(25) };
    readonly string root, password;
    public Bridge(string url, string secret)
    {
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback)))
            throw new InvalidOperationException("Use HTTPS, or HTTP for a local server only.");
        root = url.Trim().TrimEnd('/'); password = secret;
    }
    string Url(string path) => root + "/api/v1/" + path + (path.Contains('?') ? "&" : "?") + "password=" + Uri.EscapeDataString(password);
    async Task<JsonElement> Read(HttpResponseMessage response)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Server returned {(int)response.StatusCode}. Check your connection, password, and server configuration.");
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("data", out var data) ? data.Clone() : doc.RootElement.Clone();
        }
    }
    public async Task<JsonElement> Post(string path, object body) => await Read(await client.PostAsJsonAsync(Url(path), body));
    public async Task<JsonElement> Contacts() => await Read(await client.GetAsync(Url("contact")));
    public async Task Ping() => await Read(await client.GetAsync(Url("ping")));
    public async Task<JsonElement> Chats() => await Post("chat/query", new { limit = 100, offset = 0, with = new[] { "participants", "lastMessage" }, sort = "lastmessage" });
    public async Task<JsonElement> Messages(string guid, int offset = 0) => await Post("message/query", new { chatGuid = guid, limit = 100, offset, sort = "DESC", with = new[] { "handle", "attachment" } });
    public async Task Send(string guid, string text) => await Post("message/text", new { chatGuid = guid, message = text, tempGuid = Guid.NewGuid().ToString(), method = "private-api" });
    public async Task NewChat(string[] addresses, string text) => await Post("chat/new", new { addresses, message = text, method = "private-api" });
    public async Task Upload(string guid, string path)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(guid), "chatGuid");
        form.Add(new StringContent(Guid.NewGuid().ToString()), "tempGuid");
        form.Add(new StringContent("private-api"), "method");
        form.Add(new StreamContent(File.OpenRead(path)), "attachment", Path.GetFileName(path));
        await Read(await client.PostAsync(Url("message/attachment"), form));
    }
    public async Task Download(string guid, string path)
    {
        using var response = await client.GetAsync(Url("attachment/" + Uri.EscapeDataString(guid) + "/download?original=true"));
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("The attachment could not be downloaded.");
        await using var output = File.Create(path);
        await response.Content.CopyToAsync(output);
    }
        public async Task<byte[]> Preview(string guid, bool large = false)
    {
        using var response = await client.GetAsync(Url("attachment/" + Uri.EscapeDataString(guid) + (large ? "/download?original=false&width=2048&quality=best" : "/download?original=false&width=640&quality=good")), HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Preview unavailable");
        const int maximum = 12 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > maximum) throw new InvalidOperationException("Preview too large");
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var output = new MemoryStream();
        var buffer = new byte[8192]; int count;
        while ((count = await stream.ReadAsync(buffer)) > 0)
        {
            if (output.Length + count > maximum) throw new InvalidOperationException("Preview too large");
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
    public void Dispose() => client.Dispose();
}
public static class Json
{
    public static string Text(this JsonElement value, string key, string fallback = "") => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var item) && item.ValueKind != JsonValueKind.Null ? item.ToString() : fallback;
    public static JsonElement Part(this JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var item) ? item : default;
    public static IEnumerable<JsonElement> Rows(this JsonElement value) => value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : Array.Empty<JsonElement>();
}
