namespace Threadlet;
public static class MessageBehavior
{
    public static bool ShouldNotify(bool ready, bool changed, bool enabled, bool muted, bool outgoing, bool viewing) => ready && changed && enabled && !muted && !outgoing && !viewing;
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
