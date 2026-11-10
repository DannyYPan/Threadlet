using System.Diagnostics;
using System.IO;
namespace Threadlet;
public static class VmLauncher
{
    public static void ImportLegacy(Preferences settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.VmxPath) && !string.IsNullOrWhiteSpace(settings.VmrunPath)) return;
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 7 && directory != null; i++, directory = directory.Parent)
        {
            var config = Path.Combine(directory.FullName, "vm-config.cmd");
            if (!File.Exists(config)) continue;
            foreach (var line in File.ReadLines(config))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, "^\\s*@?set\\s+\"?THREADLET_(VMRUN|VMX)=(.*?)\"?\\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success) continue;
                var value = match.Groups[2].Value.Trim().Trim('"');
                if (match.Groups[1].Value.Equals("VMRUN", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(settings.VmrunPath)) settings.VmrunPath = value;
                if (match.Groups[1].Value.Equals("VMX", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(settings.VmxPath)) settings.VmxPath = value;
            }
            break;
        }
        if (string.IsNullOrWhiteSpace(settings.VmrunPath))
            settings.VmrunPath = new[] { @"C:\Program Files (x86)\VMware\VMware Workstation\vmrun.exe", @"C:\Program Files\VMware\VMware Workstation\vmrun.exe" }.FirstOrDefault(File.Exists) ?? "";
    }
    public static async Task<string> Start(Preferences settings)
    {
        if (!settings.AutoStartVm) return "Background VM startup is off";
        if (string.IsNullOrWhiteSpace(settings.VmrunPath) || string.IsNullOrWhiteSpace(settings.VmxPath)) return "VM not configured - choose an existing VM in Settings";
        if (!File.Exists(settings.VmrunPath) || !Path.GetFileName(settings.VmrunPath).Equals("vmrun.exe", StringComparison.OrdinalIgnoreCase)) return "VMware vmrun.exe was not found";
        if (!File.Exists(settings.VmxPath) || !Path.GetExtension(settings.VmxPath).Equals(".vmx", StringComparison.OrdinalIgnoreCase)) return "The configured VM file was not found";
        try
        {
            var list = await Run(settings.VmrunPath, "list");
            if (list.Code != 0) return "VMware could not list VMs. Check its installation";
            if (list.Output.Split('\n').Any(line => line.Trim().Equals(Path.GetFullPath(settings.VmxPath), StringComparison.OrdinalIgnoreCase))) return "VM is already running in the background";
            var started = await Run(settings.VmrunPath, "start", settings.VmxPath, "nogui");
            return started.Code == 0 ? "VM started in the background - waiting for BlueBubbles" : "VM startup failed. Open VMware once to complete its setup";
        }
        catch (OperationCanceledException) { return "VMware startup timed out. Check VMware setup"; }
        catch { return "VMware could not start. Check the configured paths"; }
    }
    static async Task<(int Code, string Output)> Run(string program, params string[] args)
    {
        var info = new ProcessStartInfo(program) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException();
        var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(); throw; }
        await errors;
        return (process.ExitCode, await output);
    }
}
