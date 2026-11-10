using System.IO;
using System.Text.Json;
namespace Threadlet;
public sealed class Preferences
{
    public HashSet<string> Muted { get; set; } = new();
    public HashSet<string> Pinned { get; set; } = new();
    public Dictionary<string, string> SeenMessages { get; set; } = new();
    public HashSet<string> ForcedUnread { get; set; } = new();
    public string VmrunPath { get; set; } = "";
    public string VmxPath { get; set; } = "";
    public bool AutoStartVm { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public bool NotificationPreviews { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string? StoragePath { get; init; }
    static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Threadlet", "preferences.json");
    public static Preferences Load()
    {
        try { var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(DefaultPath)) ?? new(); value.Muted ??= new(); value.Pinned ??= new(); value.SeenMessages ??= new(); value.ForcedUnread ??= new(); return value; }
        catch { return new(); }
    }
    public bool Save()
    {
        try { var FilePath = StoragePath ?? DefaultPath; Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); var temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this)); File.Move(temp, FilePath, true); return true; }
        catch { return false; }
    }
}
public record ContactChoice(string Name, string Address)
{
    public override string ToString() => Name + "  |  " + Address;
}
