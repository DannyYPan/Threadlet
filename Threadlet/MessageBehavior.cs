namespace Threadlet;
public static class MessageBehavior
{
    public static bool ShouldNotify(bool ready, bool changed, bool enabled, bool muted, bool outgoing, bool viewing) => ready && changed && enabled && !muted && !outgoing && !viewing;
    public static bool IsUnread(string token, bool outgoing, bool serverRead, string? seenToken, bool forced) => forced || (!outgoing && token.Length > 0 && (seenToken != null ? seenToken != token : !serverRead));
    public static bool CanUnsend(bool mine, string service, bool sent, long created, long now, bool supported, bool retracted, bool simplePart) => supported && mine && sent && service.Equals("iMessage", StringComparison.OrdinalIgnoreCase) && !retracted && simplePart && created > 0 && now >= created && now - created < 120000;
    public static string Receipt(bool mine, long error, long read, long delivered, bool sent)
    {
        if (!mine) return "";
        if (error > 0) return "Not Delivered";
        if (read > 0) return "Read " + Timestamp(read);
        if (delivered > 0) return "Delivered";
        return sent ? "Sent" : "";
    }
    static string Timestamp(long value)
    {
        try { return DateTimeOffset.FromUnixTimeMilliseconds(value).LocalDateTime.ToString("h:mm tt"); }
        catch (ArgumentOutOfRangeException) { return ""; }
    }
}
