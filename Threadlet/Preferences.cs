using System.IO;
using System.Text.Json;
namespace Threadlet;
public sealed class Preferences
{
    public HashSet<string> Muted { get; set; } = new();
    public HashSet<string> Pinned { get; set; } = new();
    public bool Notifications { get; set; } = true;
    public bool NotificationPreviews { get; set; }
    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Threadlet", "preferences.json");
    public static Preferences Load()
    {
        try { var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(FilePath)) ?? new(); value.Muted ??= new(); value.Pinned ??= new(); return value; }
        catch { return new(); }
    }
    public bool Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!); var temp = FilePath + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(this)); File.Move(temp, FilePath, true); return true; }
        catch { return false; }
    }
}
public record ContactChoice(string Name, string Address)
{
    public override string ToString() => Name + "  |  " + Address;
}
