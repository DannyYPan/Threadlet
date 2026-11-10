# Messages behavior reference

Reviewed November 9, 2026. These are product references, not a claim of full iPhone feature parity.

| Area | Apple behavior | Threadlet implementation |
| --- | --- | --- |
| Conversations | Pin important threads; Hide Alerts stops one thread's notifications without stopping its messages | Right-click a thread to Pin/Unpin or Hide/Show Alerts. Up to nine pins; preferences saved locally. Pins sort first rather than using Apple's circular pinned grid. |
| Composition | Compose a conversation using recipients, then send text or media | New conversation offers searchable Mac contacts, multiple recipients, and a first message; existing chats support text, file picker, and file drag/drop. |
| Media | View photos in the conversation and find shared attachments in contact details | Click an image for a zoomable viewer. Info browses attachments from the loaded messages. Save original preserves the original bytes and extension. |
| HEIC/HEIF | Native Apple formats are displayable on Apple devices | The Mac server converts images for the Windows preview; originals remain unchanged. Requires working BlueBubbles conversion. Local unsent HEIC previews depend on Windows codecs. |
| Read status | A recipient who shares receipts exposes a Read status to the sender | Show the recipient's Read timestamp or Delivered status from server data. Per the user's preference, Threadlet never sends mark-read requests. Other Apple devices can independently share receipts. |
| Notifications | Per-conversation Hide Alerts | Windows notification-area balloons, with global settings, private previews by default, click-to-open, and persistent per-chat muting. Only while Threadlet is running; Windows settings can suppress alerts. |
| Search | Apple supports message history and combined filters | Current sidebar searches loaded conversation names and last-message previews only. Full history search is not implemented. |
| Reactions, inline replies, edit/unsend | Apple has message actions with service/version constraints | Not implemented yet. BlueBubbles documents Private API support, but these need a real server for capability checks and end-to-end testing. |
| Apple ecosystem | FaceTime, Check In, location sharing and other services | Not implemented. No claim that these can be reproduced by a Windows client. |

## Sources

- [Apple: Use Messages](https://support.apple.com/en-us/104982) — composition, nine pins, Hide Alerts and attachment details.
- [Apple: Read receipts](https://support.apple.com/guide/iphone/turn-read-receipts-on-or-off-iph5e713a045/ios) — distinction between receiving and sharing Read status.
- [Apple: Search messages](https://support.apple.com/en-gb/111116) — search and conversation filters.
- [Apple: Edit and unsend](https://support.apple.com/en-ie/guide/iphone/iphe67195653/27/ios/27) — iMessage requirements and timing limits.
- [BlueBubbles: Private API](https://docs.bluebubbles.app/private-api) — advanced functionality and dependencies.
- [BlueBubbles: Attachment router source](https://github.com/BlueBubblesApp/bluebubbles-server/blob/master/packages/server/src/server/api/http/api/v1/routers/attachmentRouter.ts) — converted previews versus original downloads.

## Current limits

Polling runs every five seconds. The client loads at most 100 conversations and the latest 100 messages per conversation. Notifications summarize the newest message returned for each chat rather than every intermediate message in a burst. Muting and pinning are local to Threadlet and do not change the iPhone's settings. Preferences are stored in LocalAppData/Threadlet/preferences.json; passwords, message bodies, and photos are not stored there. BlueBubbles is not yet configured on this PC, so live Apple behavior remains unverified.
