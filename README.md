# Threadlet

A native Windows messaging client for your own BlueBubbles server. Under construction: the app runs in demo mode, but live messaging still requires a configured Mac/macOS server and end-to-end validation.

## Open the app

Use the **Threadlet desktop shortcut** or **Open-Threadlet.vbs** to open the packaged app without a terminal. The package lives in artifacts/app. To rebuild from source, run **Start-Threadlet.bat** or:

```powershell
dotnet run --project Threadlet/Threadlet.csproj --configuration Release
```

The .NET 8 SDK is needed for source builds. Install-DotNet.bat installs it through winget.

## Features

- Messages-style WPF layout, service-colored bubbles, circular pins, contact names, conversation search and per-conversation drafts.
- New individual/group conversations with a searchable contact picker and multiple recipients.
- Text sending, photo/file upload, drag/drop attachments, and removable attachment previews.
- Inline photos, a click-to-open photo viewer with zoom, and saving original attachments. HEIC/HEIF viewing uses BlueBubbles conversion; sending and saving preserve the original format.
- Local unread dots/counts, All/Unread filtering and Mark as Read/Unread without sending receipts.
- Undo Send for eligible outgoing iMessages within two minutes; expired, carrier-service, unknown-capability and multipart actions stay hidden.
- Recipient Delivered/Read status with read timestamps when reported by the server. Threadlet does not send mark-read requests or enable your read receipts.
- Desktop notifications while running, with private previews by default. Right-click a chat for **Hide Alerts** or **Pin**. Settings are saved between launches; pins and mutes apply only to Threadlet.
- Conversation **Info** with Hide Alerts and shared attachments from the loaded messages.

## Connect

Open Settings and enter your BlueBubbles URL and password. Use HTTPS for remote servers; HTTP is allowed only on localhost. Credentials are kept in memory, so reconnect after restarting. The Mac needs Messages signed in and Contacts permission; sending and group creation use compatible Private API support.

## macOS VM and BlueBubbles setup

The planned backend is:

**Threadlet on Windows -> BlueBubbles server -> Messages inside a macOS VM**

VMware runs the virtual computer. **OC4VM (OpenCore for VMware)** supplies the boot image and VM templates that help macOS run without patching VMware's program files. OC4VM does not include macOS or guarantee iMessage activation.

### Current progress - November 10, 2026

- PC virtualization is enabled; the host has about 16 GB RAM and sufficient free disk space.
- QEMU's disk-image tools are installed.
- A macOS recovery image has been downloaded from Apple's servers and verified using the upstream recovery utility.
- OC4VM 3.0.1 has been downloaded, checksum-verified, and extracted.
- The official Intel macOS BlueBubbles 1.9.12 installer has been downloaded and verified.
- **VMware Workstation Pro 25H2u1 for Windows is currently being downloaded by the user.** Installation has not yet been confirmed.
- The macOS VM has not been created or booted. Messages sign-in, BlueBubbles configuration, and live Threadlet messaging are still pending.

### Install locations

1. Install **VMware Workstation Pro 25H2u1 for Windows** using the installer's default application folder.
2. Store the actual macOS VM files here:

   ~~~text
   C:\Users\danny\Virtual Machines\Threadlet
   ~~~

   This keeps the large, frequently changing virtual disks outside OneDrive and the source repository.

3. Downloaded setup assets are currently in the repository:

   | Asset | Location |
   | --- | --- |
   | BlueBubbles server installer | downloads/BlueBubbles-1.9.12.dmg |
   | Apple recovery image and verification data | backend/recovery/ |
   | Verified OC4VM package and templates | backend/tools/oc4vm-3.0.1/ |
   | Upstream macOS recovery utility | backend/tools/OpenCore-1.0.8/Utilities/macrecovery/ |

These download/tool directories are excluded from Git.

### Remaining setup

1. Finish downloading and installing VMware.
2. Prepare the Intel OC4VM template and attach the verified recovery image.
3. Boot the VM and install macOS.
4. Complete macOS setup and sign into Messages with your Apple Account. Sign-in and two-factor authentication require your interaction.
5. Install BlueBubbles inside macOS and grant its required permissions. Configure the compatible Private API helper for advanced messaging.
6. Verify the Windows-to-guest connection and server URL before connecting Threadlet. The current client allows HTTP only on localhost; direct HTTP to a VM's private IP is not yet supported. That networking step must be resolved during integration.
7. In Threadlet **Settings**, choose the VM's .vmx file and VMware's vmrun.exe, then enable automatic startup. Existing vm-config.cmd paths can also be imported.
8. Verify real text/photo delivery, recipient receipts, Undo Send, and background startup.

After configuration, Threadlet can start the existing VM with nogui from the desktop shortcut, direct executable, or source launcher. The initial installation and account setup remain interactive. Passwords are currently kept in memory, so server reconnection after restarting is still manual.

### Setup references

- [BlueBubbles: VMware on Windows guide](https://github.com/BlueBubblesApp/bluebubbles-docs/blob/master/server/advanced/macos-virtualization/running-a-macos-vm/deploying-macos-in-vmware-on-windows-full-guide.md)
- [OC4VM documentation and tested VMware versions](https://github.com/DrDonk/OC4VM/blob/main/readme.md)
- [Official VMware download instructions](https://knowledge.broadcom.com/external/article/368734)

The BlueBubbles guide includes older VMware 15/16 and unlocker instructions. Our prepared approach uses OC4VM; version-specific instructions must be checked during setup.

## Verification and limitations

```powershell
dotnet run --project tests/BridgeChecks/BridgeChecks.csproj
```

Mock-server checks validate client requests, photo conversion/original-download modes, recipient read-status presentation, and notification suppression. They do not prove Apple delivery, real HEIC conversion, or Windows notification display.

Polling runs every five seconds and loads at most 100 conversations and the latest 100 messages in each. Full-history search, reactions, inline replies, editing, audio recording, typing indicators, and Apple ecosystem features are not implemented. Undo Send is limited to supported simple iMessages and still needs live Apple validation. SMS/RCS compatibility is not promised. Windows notification settings can suppress desktop alerts. Read receipts can still be sent independently by your other Apple devices.

See [MESSAGES-REFERENCE.md](MESSAGES-REFERENCE.md) for the Apple documentation review and feature comparison. No BlueBubbles source is bundled; this app communicates with your server through its REST API.

Detailed research: [MESSAGES-COMPONENTS.md](MESSAGES-COMPONENTS.md). Current edge-case review and test limits: [FEATURE-AUDIT.md](FEATURE-AUDIT.md).
