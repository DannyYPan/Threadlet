# Feature edge-case audit

Audited November 9, 2026. Checks below distinguish deterministic local verification from live Apple verification. No macOS VM/server is configured on this computer yet.

| Area | Issue/check | Result |
| --- | --- | --- |
| Sent photos | Demo history loader returned immediately without a server | Fixed. A running WPF test sends a generated PNG and verifies its history Image.Source. |
| Photo originals | Save must not silently convert HEIC to another format | original=true; download streams through a temporary file before replacing the destination. |
| Failed downloads | Preserve an existing destination on server failure | Mock server rejection test passes. |
| HEIC/HEIF | Server conversion versus local codec availability | Remote previews request converted images; local decoding can still fail without a Windows codec. Live HEIC conversion remains untested. |
| Response schema | Chat serializer uses messages[], not a guaranteed lastMessage field | Current and legacy shapes handled and fixture-tested. |
| Service metadata | Message serializer can omit service/isSent | Resolve service from chat identity; Undo Send requires delivery/read evidence or a confirmed send response. Unknown state stays ineligible. |
| Undo Send age | Exact two-minute limit and future timestamps | Boundary tests cover 119999, 120000 and 120001 ms, plus future dates. |
| Undo Send type | Incoming, SMS/MMS/RCS, unknown service, pending, retracted and multipart messages | Ineligible. System/reaction records are also excluded in parsing. |
| Undo Send backend | macOS 13+, Private API enabled and helper connected | Capabilities checked; unknown/stale state hides the action. |
| Undo Send menu | Menu remains open as the message expires | Visibility refreshes while open; click rechecks eligibility. |
| Undo Send failure | Do not remove locally before server success | Local tombstone appears only after acceptance. Older recipient software may retain originals. |
| Undo Send UI | Actual context menu and demo removal | WPF tests verify fresh visibility, SMS/expired hiding, and removal of demo photo content. |
| Read status | Do not infer Read from Delivered | Separate timestamp rules; failure takes precedence. |
| Your receipts | Local unread must not call server mark-read | No mark-read endpoint/method in the client. Other Apple devices are independent. |
| Unread | Local view, manual unread, filtering, missing IDs, own outgoing messages | Policy and running UI checks pass. State is saved locally. |
| Notifications | Muted, disabled, own message, visible chat, initial history and duplicate polls | Suppression rules tested. Windows notification display still requires manual/live verification. |
| Contacts | Keep original +country prefixes for composition | Lookup normalization is separate from the original recipient address. |
| Recipients | Repeated formatted/case-variant addresses | Normalized duplicates removed before creation. Recipient availability remains server-validated. |
| Send overlap | Selection, attachment replacement, connection changes during send | Core controls guarded; successful text cleared independently before a following attachment upload. |
| Send uncertainty | Network timeout can happen after delivery | No automatic resend. A retained draft is not proof that delivery failed. |
| Live carrier services | UI must not imply tested SMS/RCS sending | Non-iMessage live sends disabled; service coloring/status retained for reading. |
| VM entry points | Direct exe previously bypassed script startup | Startup now handled in the app for shortcut, direct exe and source launch. |
| Missing VM | No VM should be silently considered running | Explicit status and setup controls; missing/disabled path checks tested. |
| Existing VM | Avoid unnecessary start | vmrun list checked first; helper uses nogui, no terminal, timeout and argument lists. Real VM startup remains untested here. |
| Visuals | Prior purple theme differed from requested Messages style | Neutral surfaces, blue bubbles, circular pins, compact header/composer; rendered screenshot inspected. |

## Verification commands

    dotnet run --project tests/BridgeChecks/BridgeChecks.csproj
    dotnet run --project tests/UiChecks/UiChecks.csproj -- artifacts/screenshots/messages.png

The UI test uses temporary preferences and generated image data, starts no VM, and sends no real message. Its fixture files are removed afterward.

## Unresolved live checks

- Real iMessage activation, server connectivity and Private API availability.
- Text and photo delivery in direct/group conversations, including actual HEIC originals.
- Actual recipient receipt updates and Apple-side Undo Send acceptance.
- macOS/server version differences, server restart and reconnect behavior.
- Windows alert display, Focus/Do Not Disturb effects and rapid multiple notifications.
- VM software installation, macOS installation, sign-in and first-run permissions.

See MESSAGES-COMPONENTS.md for the wider feature inventory and source links.
