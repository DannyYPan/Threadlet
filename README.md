# Threadlet

A native Windows messaging client for your own BlueBubbles server. Under construction: the app runs in demo mode, but live messaging still requires a configured Mac/macOS server and end-to-end validation.

## Open the app

Use the **Threadlet desktop shortcut** or **Open-Threadlet.vbs** to open the packaged app without a terminal. The package lives in artifacts/app. To rebuild from source, run **Start-Threadlet.bat** or:

```powershell
dotnet run --project Threadlet/Threadlet.csproj --configuration Release
```

The .NET 8 SDK is needed for source builds. Install-DotNet.bat installs it through winget.

## Features

- Native WPF interface, blue outgoing bubbles, contact names, conversation search and per-conversation drafts.
- New individual/group conversations with a searchable contact picker and multiple recipients.
- Text sending, photo/file upload, drag/drop attachments, and removable attachment previews.
- Inline photos, a click-to-open photo viewer with zoom, and saving original attachments. HEIC/HEIF viewing uses BlueBubbles conversion; sending and saving preserve the original format.
- Recipient Delivered/Read status with read timestamps when reported by the server. Threadlet does not send mark-read requests or enable your read receipts.
- Desktop notifications while running, with private previews by default. Right-click a chat for **Hide Alerts** or **Pin**. Settings are saved between launches; pins and mutes apply only to Threadlet.
- Conversation **Info** with Hide Alerts and shared attachments from the loaded messages.

## Connect

Open Connection & launch settings and enter your BlueBubbles URL and password. Use HTTPS for remote servers; HTTP is allowed only on localhost. Credentials are kept in memory, so reconnect after restarting. The Mac needs Messages signed in and Contacts permission; sending and group creation use compatible Private API support.

The official Intel macOS BlueBubbles installer is in downloads. No VM or macOS image has been installed. If you already have a working VMware VM, copy vm-config.example.cmd to vm-config.cmd and set its paths. The terminal-free launcher can start that VM with nogui. Initial macOS installation and Apple sign-in require interaction.

## Verification and limitations

```powershell
dotnet run --project tests/BridgeChecks/BridgeChecks.csproj
```

Mock-server checks validate client requests, photo conversion/original-download modes, recipient read-status presentation, and notification suppression. They do not prove Apple delivery, real HEIC conversion, or Windows notification display.

Polling runs every five seconds and loads at most 100 conversations and the latest 100 messages in each. Full-history search, reactions, inline replies, editing/unsending, audio recording, typing indicators, and Apple ecosystem features are not implemented. SMS/RCS compatibility is not promised. Windows notification settings can suppress desktop alerts. Read receipts can still be sent independently by your other Apple devices.

See [MESSAGES-REFERENCE.md](MESSAGES-REFERENCE.md) for the Apple documentation review and feature comparison. No BlueBubbles source is bundled; this app communicates with your server through its REST API.
