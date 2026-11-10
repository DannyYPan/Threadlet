using System.Drawing;
using System.Windows.Forms;
namespace Threadlet;
public sealed class DesktopNotifications : IDisposable
{
    readonly NotifyIcon icon = new() { Icon = SystemIcons.Application, Text = "Threadlet", Visible = false };
    Action? open;
    public DesktopNotifications()
    {
        icon.BalloonTipClicked += (_, _) => { open?.Invoke(); icon.Visible = false; };
        icon.BalloonTipClosed += (_, _) => icon.Visible = false;
        icon.DoubleClick += (_, _) => open?.Invoke();
    }
    public void Show(string title, string body, Action onClick)
    {
        open = onClick;
        icon.Visible = true;
        icon.BalloonTipTitle = title.Length > 63 ? title[..60] + "..." : title;
        icon.BalloonTipText = body.Length > 240 ? body[..237] + "..." : body;
        icon.ShowBalloonTip(6000);
    }
    public void Dispose() => icon.Dispose();
}
