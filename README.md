# Threadlet
A Windows messaging app powered by BlueBubbles, bringing iMessage conversations, group chats, and photo sharing to your desktop through a macOS bridge running in the background.

this is currently under construction; it doesn't work yet!
## Run on Windows

Use the **Threadlet desktop shortcut** or double-click **Open-Threadlet.vbs** for the packaged app without a terminal. For rebuilding from source, double-click **Start-Threadlet.bat** in this folder. It builds the Release version and opens the app. The first build needs the .NET 8 SDK; Install-DotNet.bat installs it through Microsoft's winget package.

Alternatively:

```powershell
dotnet run --project Threadlet/Threadlet.csproj --configuration Release
```

## Current implementation

- Native C# / WPF interface with a dark theme, searchable conversation sidebar, message bubbles, and a multiline composer.
- Clearly labeled local demo conversations; demo messages never reach Apple or a server.
- BlueBubbles server connection with in-memory credentials, conversation polling every five seconds, and the latest 100 messages in each chat.
- Text sending, group-conversation creation, attachment upload, and attachment saving through the BlueBubbles REST API. Sending and group creation require compatible Private API support.
- HTTPS server connections, with plain HTTP allowed only for localhost. Use a trusted HTTPS endpoint for remote servers.
- Optional headless VMware startup: copy vm-config.example.cmd to vm-config.cmd and configure paths to an existing VM. The launcher never shuts down an existing VM.

## Connect

Open **Connection & launch settings**, enter your BlueBubbles URL and password, and connect. The Mac or macOS VM must already run BlueBubbles with Messages signed in. Credentials are intentionally not saved to disk; reconnect when opening the app.

## Limits and validation

This is an initial client, not a complete replacement for Apple Messages. Live API operations still need testing against your BlueBubbles server. There is no VM image, automatic Apple sign-in, reaction support, typing indicator, older-message pagination, persistent message cache, or Windows toast integration yet. Images show inline previews when the server can convert them; attachment buttons save files. Delivery/read labels reflect actual server timestamps, and Mark current conversation read explicitly marks the selected chat. Contacts can be browsed and are used for conversation names when the Mac grants access. SMS/RCS support is not promised. Only the latest 100 conversations and messages are loaded.

The UI can be explored without any server or VM. No BlueBubbles source is bundled; the client communicates with your own server over HTTP.

API reference: https://docs.bluebubbles.app/server/developer-guides/rest-api-and-webhooks

## Verification

Local mock-server checks cover authentication, contacts, querying and sending messages, multipart photo upload, preview downloads, attachment saving, and mark-read requests. Run: dotnet run --project tests/BridgeChecks/BridgeChecks.csproj. These checks validate client requests, not Apple delivery or server compatibility.

The packaged app lives in artifacts/app. The desktop shortcut uses Open-Threadlet.vbs so no terminal window appears. If vm-config.cmd exists, Start-VM.cmd starts the configured existing VMware VM with nogui. macOS setup and Apple sign-in still need to be completed interactively. No VM software or macOS image is included.
