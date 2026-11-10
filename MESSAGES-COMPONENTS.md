# Messages component inventory

Research reviewed November 9, 2026. This is an implementation inventory, not a claim that Threadlet duplicates every iPhone feature. Apple behavior is cited; proposed Windows behavior and priorities are engineering judgments. Availability varies with OS version, service, region and server configuration.

Status: **Implemented** means client code exists; **Partial** means a narrower version exists; **Missing** means no working control. Live Apple operations remain unverified until a BlueBubbles server is configured.

## Conversation navigation

| Component | Threadlet status | Required behavior / next work |
| --- | --- | --- |
| Conversation list | Implemented | Name, avatar initials, last-message preview, selection and unread dot. No contact photos yet. |
| Pins | Implemented | Up to nine, with circular shortcuts and Pin/Unpin actions. Local settings rather than Apple-synced pins. |
| All / Unread | Implemented | Toggle filter; opening the latest messages clears local unread state. |
| Manual read/unread | Implemented | Local metadata only; no outgoing read receipt. Mark Unread dismisses the active selection. |
| Known / unknown / spam | Missing | Separate filtering from blocking or deleting. Avoid inventing Apple's spam classifications. |
| History search | Partial | Sidebar searches loaded conversation names/previews. Full message/attachment results and pagination are missing. |
| Recently Deleted | Missing | Do not present ordinary deletion as Undo Send or imply recoverability without backend support. |
| Empty / loading / offline | Partial | Status messages exist; richer empty and retry states are needed. |

Apple references: [Pins and Hide Alerts](https://support.apple.com/en-us/104982), [unknown sender screening](https://support.apple.com/en-nz/guide/iphone/iph203ab0be4/27/ios/27), [search](https://support.apple.com/en-gb/111116), [recovery](https://support.apple.com/guide/iphone/recover-deleted-messages-iph16ecebf48/ios).

## Compose and send

| Component | Threadlet status | Required behavior / next work |
| --- | --- | --- |
| New conversation | Implemented | Search contacts or enter addresses; demo mode also permits composition. |
| Recipient selection | Implemented | Multiple recipients, original contact addresses preserved, normalized duplicate removal. Recipient chips and availability badges remain missing. |
| Group creation | Implemented | Uses Private API; require compatible macOS/helper before live creation. |
| Text composer | Implemented | Multiline text, Enter to send, Shift+Enter newline. Whitespace-only messages stay unsent unless an attachment exists. |
| Drafts | Partial | Per-chat in-memory text/file drafts; restart persistence is missing. |
| Attachment controls | Partial | File picker, drag/drop, remove attachment and preview. One attachment per send; multi-photo selection is missing. |
| Sending/failure | Partial | Block overlapping sends and retain unsuccessful parts. A timeout can leave delivery uncertain; do not retry automatically. |
| Service indication | Partial | Blue iMessage and green recognized carrier-service bubbles. Live carrier sends are disabled pending validation. |
| Send Later | Missing | A bridge scheduler is not equivalent to Apple's cloud scheduling. |

Apple supports composition and inline replies; Send Later can schedule an iMessage up to 14 days ahead and deliver while the sender's devices are offline. Editing/canceling requires connectivity. Sources: [composition](https://support.apple.com/guide/iphone/send-and-reply-to-messages-iph82fb73ba3/26/ios/26), [Send Later](https://support.apple.com/guide/iphone/schedule-text-messages-to-send-later-iph5ae9a7be6/ios), [services](https://support.apple.com/en-gb/104972).

## Transcript and message actions

| Component | Threadlet status | Required behavior / next work |
| --- | --- | --- |
| Bubbles / sender labels | Implemented | Direction, sender identity, service color. Bubble grouping and timeline date separators need refinement. |
| Timestamps | Partial | Message times are shown. iPhone-style gesture disclosure is not implemented. |
| Delivered / Read | Implemented | Actual recipient timestamps only; normal status appears under the latest outgoing message. Failures stay visible. |
| Copy | Implemented | Context-menu copy; selectable substrings are missing. |
| Undo Send | Implemented | Eligible outgoing iMessage only; two-minute limit, server capability and message-part checks. |
| Edit | Missing | Must enforce iMessage eligibility, Apple's edit window and count, and show edit history. |
| Inline replies | Missing | Must preserve the referenced message/part; a copied quotation is not protocol-level reply support. |
| Mentions | Missing | Structured recipient targeting and mention-alert behavior need implementation. |
| Tapbacks | Missing | Store sender/target association, replacement/removal, and avoid rendering a reaction as ordinary chat text. |
| Forward / multi-select | Missing | Explicit selection, attribution and attachment handling required. |
| Delete | Missing | Separate local deletion, synced deletion and unsending; scope must be explicit. |

Apple limits Undo Send to two minutes and editing to five changes within 15 minutes. Older recipient software can retain an unsent original or display edits differently. These are iMessage features rather than SMS/MMS/RCS actions. Sources: [edit/unsend](https://support.apple.com/en-ie/guide/iphone/iphe67195653/27/ios/27), [replies/mentions](https://support.apple.com/en-gb/104974), [Tapbacks](https://support.apple.com/en-mide/guide/iphone/iph018d3c336/ios), [deletion](https://support.apple.com/en-ie/guide/iphone/iph2c9c4bfcb/ios).

## Media

| Component | Threadlet status | Required behavior / next work |
| --- | --- | --- |
| Inline photos | Implemented | Same loader supports sent demo files and server attachments; failures have a visible fallback. |
| HEIC / HEIF | Partial | Connected Mac converts previews. Original downloads retain format. Offline/local decoding depends on Windows codecs. |
| Photo viewer | Implemented | Fit, zoom, close/Escape and Save original. Gallery next/previous and copy image remain missing. |
| Photo collections | Missing | Multiple images should form a collection rather than unrelated placeholders. |
| Shared media | Partial | Info browses loaded attachments. Complete history and category filters are missing. |
| Video | Missing | Requires playback controls, codec fallback, duration and safe download behavior. |
| Audio messages | Missing | Recording/review/cancel/playback, transcripts and expiry semantics need their own model. |
| Documents | Partial | Save-original support; file-type cards and previews are missing. |
| Rich links | Missing | URL recognition, preview loading and explicit open-link behavior needed. |
| Live Photos / stickers / Memoji | Missing | Rendering a static attachment does not reproduce animation, placement or editable sticker behavior. |

Apple audio supports recording review and Keep/expiry behavior; expiration is local and must not be confused with retracting another person's copy. Stickers can be sent independently or placed onto bubbles. Sources: [audio](https://support.apple.com/en-euro/guide/iphone/iph2e42d3117/ios), [stickers](https://support.apple.com/en-gb/guide/iphone/iph37b0bfe7b/ios), [photo sharing](https://support.apple.com/guide/iphone/share-photos-and-videos-iphf28f17237/ios).

## Conversation details and groups

| Component | Threadlet status | Required behavior / next work |
| --- | --- | --- |
| Details panel | Partial | Hide Alerts and loaded attachments; full participant profiles are missing. |
| Group title/photo | Missing | Server exposes related actions; need capability checks and UI. |
| Add/remove members | Missing | Validate group service, size and supported recipients before showing actions. |
| Leave group | Missing | Do not equate muting or deleting with leaving. |
| Contact actions | Partial | Read-only contact lookup exists. Creating/updating contacts and name/photo sharing need validation. |
| Polls / backgrounds | Missing | Native shared state requires protocol support; local decoration alone does not establish interoperability. |

Apple's group rules vary by service: adding to an existing iMessage group requires an existing group rather than a direct conversation; removal has minimum membership requirements. Sources: [membership rules](https://support.apple.com/en-ph/108303), [group conversations](https://support.apple.com/en-gb/guide/iphone/iphb10c80fc5/ios), [polls](https://support.apple.com/en-il/guide/iphone/iphde1787df4/ios).

## Notifications and privacy

| Component | Threadlet status | Required behavior / next work |
| --- | --- | --- |
| Desktop alerts | Partial | Notification-area balloons while running; Windows toast history and inline notification replies are missing. |
| Hide Alerts | Implemented | Persistent per-chat setting; messages and unread state continue updating. |
| Private previews | Implemented | Message bodies hidden by default; names can still appear. |
| Click to open | Implemented | Open target conversation; overlapping notification activation requires further live testing. |
| Focus / mentions | Missing | Mention overrides and Focus sharing must be independent from ordinary mute. |
| Your read receipts | Intentionally off | No mark-read endpoint in the client. Other Apple devices may independently send receipts. |
| Their read receipts | Implemented | Cannot force someone to share them or infer reading from delivery. |
| Blocking / reporting | Missing | Local Hide Alerts does not block a sender or report anything to Apple. |
| Sensitive-content warnings | Missing | Apple's on-device detection is not automatically inherited through the bridge. |
| Contact verification / encryption UI | Missing | Apple encryption does not automatically cover the extra Windows-to-bridge connection. Avoid claiming Apple verification for this client. |

Sources: [notification behavior](https://support.apple.com/en-gb/108127), [notification settings](https://support.apple.com/en-gb/guide/iphone/iph62faab6a4/ios), [read receipts](https://support.apple.com/guide/iphone/turn-read-receipts-on-or-off-iph5e713a045/ios), [sensitive content](https://support.apple.com/en-us/105071), [Contact Key Verification](https://support.apple.com/en-sa/guide/iphone/-iph654dd8c53/ios).

## Effects, integrations and accessibility

Text formatting, text animations, bubble/screen effects, iMessage app extensions, Apple Cash, Check In, location sharing, collaboration, translation and FaceTime require separate work and service checks. They are not implemented. BlueBubbles exposes some FaceTime-related routes; that is a feasibility lead rather than proof of a complete Windows calling experience.

A desktop adaptation should also support keyboard navigation, accessible labels, scalable text, high contrast and reduced motion. Named primary controls exist; comprehensive accessibility validation remains outstanding.

Sources: [text and effects](https://support.apple.com/en-euro/guide/iphone/iphe5c5af4d4/ios), [iMessage apps](https://support.apple.com/en-gb/104969), [Check In](https://support.apple.com/en-gb/guide/personal-safety/ips56b5bc469/web), [text accessibility](https://support.apple.com/guide/iphone/make-text-easier-to-read-iph3c076905a/ios), [reduced motion](https://support.apple.com/guide/iphone/customize-onscreen-motion-iph0b691d3ed/ios).

## Backend assessment

The official [Private API documentation](https://docs.bluebubbles.app/private-api) lists reactions, typing, replies, group controls, editing and unsending with version constraints. Its [HTTP routes](https://github.com/BlueBubblesApp/bluebubbles-server/blob/master/packages/server/src/server/api/http/api/v1/httpRoutes.ts) expose scheduling and additional integrations. A route's existence does not establish eligibility for every message or deployment.

Current Threadlet uses REST polling. A production next step is event-driven message/update processing with reconnect recovery, history pagination and robust local storage. Those are engineering recommendations; Apple documentation does not prescribe this client architecture.

## Recommended order

1. Complete live end-to-end verification of server setup, text, photos, receipts and eligibility.
2. Event updates, older history, restart-safe drafts and notification handling.
3. Replies, Tapbacks and group administration with capability-gated controls.
4. Editing/history and scheduling with explicit service/version behavior.
5. Rich media, richer search, accessibility and optional ecosystem integrations.

The user's preference remains: show recipients' read status without sending the user's read receipts.
