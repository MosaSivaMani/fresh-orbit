# Notifications module

Owner: E1. Schema `notifications`. Uses `Identity.Public`. Endpoints: `docs/api/README.md` §4.10 (customer),
§4.21 (templates). Templates: `docs/product/reference-data.md` §5.

## 1. Aggregates

| Aggregate root | Table | Row version |
|---|---|---|
| Template | `templates` (unique `(code, channel)`) | `xmin` (If-Match on edit) |
| Message | `messages` | `xmin` |
| DeviceToken | `device_tokens` (unique `push_token`; `provider` FCM, `platform` ANDROID/IOS/WEB, `last_seen_at`) — Ruling D28 | `xmin` |
| Preference | `preferences` (PK `user_id, category, channel`) | none |

### Public API

| Member | Behaviour | Used by |
|---|---|---|
| `INotificationsApi.QueueAsync(request)` | Renders the template for each allowed channel and tracks one message per channel; saved by the caller. `DedupeKey` makes it idempotent per (key, channel). | any module, event handlers |
| `INotificationsApi.QueueForCustomerAsync(request)` | Same, recipient given as customer id (resolved through `ICustomerAccountDirectory`). | Ordering / Payments event adapters |
| `IOtpSender` (Identity.Public) | Renders `OTP_LOGIN` / `OTP_VERIFY_PHONE` and sends the SMS immediately; nothing stored. A failed send throws 503 `SMS_PROVIDER_UNAVAILABLE` (NOT-R12). | Identity |

## 2. States

`messages.status`: QUEUED → SENT (provider accepted) | FAILED (5 failed attempts or no recipient address). IN_APP
messages are stored SENT; SENT → READ when the customer reads them.

## 3. Rules

| Key | Rule |
|---|---|
| NOT-R1 | Placeholders are `{{name}}`. Each template code has a fixed list of variables (stored in `templates.variables` from `notification-templates.json`, plus `businessName`, `supportPhone`, `supportEmail`, `recipientName`; the first three come from configuration `Notifications:BusinessName|SupportPhone|SupportEmail` until Notifications may use `Settings.Public`). Reference-data reloads refresh variables and category but keep staff-edited texts. An edit using another name → 422 `TEMPLATE_PLACEHOLDER_UNKNOWN`. |
| NOT-R2 | Channels for a message = active template rows of the code ∩ requested channels (null = all) ∩ preferences. Category `ORDER_UPDATES` SMS cannot be disabled; OTP and staff templates ignore preferences. Missing preference rows mean enabled for `ORDER_UPDATES` and disabled for `OFFERS`. |
| NOT-R3 | No active template row for the code → 422 `CONFIGURATION_MISSING`. |
| NOT-R4 | Dedupe: with a `DedupeKey` (the source event id for event handlers), the unique index `(dedupe_key)` where not null on `dedupe_key = key:channel:user` keeps one message per channel even if the same event is handled twice. |
| NOT-R5 | Messages are rendered when queued (subject/body stored), so later template edits do not change queued messages. Variables missing in the data render as empty text. |
| NOT-R6 | Worker job `notifications.send` (every minute) sends QUEUED SMS/EMAIL/PUSH in batches of 50 (`FOR UPDATE SKIP LOCKED`): SMS to the user's phone (`ISmsSender`), EMAIL to the user's email (`IEmailSender`, SMTP via MailKit; Mailpit in dev), PUSH to every device token (`IPushSender`, FCM HTTP v1, NOT-R14). A provider error → attempts + 1 and `next_attempt_at` = now + attempts minutes; at 5 → FAILED. An SMS the provider rejected or that has no DLT template id → FAILED at once with `SMS_<KIND>` (NOT-R12). A push is SENT only when at least one device accepted it; `PUSH_UNREACHABLE` and `PUSH_UNAUTHORIZED` are retried, `PUSH_REJECTED` and `PUSH_NOT_CONFIGURED` are FAILED at once (D324). A channel switched off by configuration fails its messages at once with `EMAIL_DISABLED` / `PUSH_DISABLED`, never SENT (D322, D325). No address (no phone, no email, no device, or every device token unregistered) → FAILED (`NO_RECIPIENT_ADDRESS`). Tokens the provider reports unregistered are removed, also when the send failed. |
| NOT-R7 | Device tokens (Ruling D28, Flutter app): `PUT /me/device-tokens` with `{ pushToken, provider: "FCM", platform: ANDROID/IOS/WEB }` registers the FCM registration token (or moves it to the caller), `last_seen_at` refreshed; another provider → 422; DELETE removes only the caller's token (unknown token → 204). |
| NOT-R8 | Preferences PUT replaces the full set; categories ORDER_UPDATES, OFFERS × channels SMS, EMAIL, PUSH, IN_APP; disabling ORDER_UPDATES SMS → 422 `PREFERENCE_REQUIRED`. |
| NOT-R9 | In-app list: the caller's IN_APP messages newest first, `unreadOnly` filter; mark read is idempotent; read-all marks every unread one. |
| NOT-R10 | Test send: renders the template with sample values (`[name]`) and sends to the calling staff member's own email/phone immediately; nothing stored. |
| NOT-R11 | Senders are adapters: `ISmsSender` chosen by `Notifications:Sms:Provider` — `DEVELOPMENT` (`DevelopmentSmsSender`: keeps messages in an in-memory outbox read via `GET /dev/sms`, logs only the template code, the phone masked to its last 4 digits and the length, never the text) or `DLT` (`DltSmsSender`, NOT-R12). The Api and Worker refuse to start (exit 1) with the development sender outside Development/Testing, with an incomplete DLT configuration, or with an unknown provider (SEC-1, rule 7). `IEmailSender` chosen by `Notifications:Email:Provider` — `SMTP` (`SmtpEmailSender`, needs `Notifications:Smtp:Host` and `FromAddress`), `DISABLED` (`DisabledEmailSender`) or `DEVELOPMENT` (`LoggingEmailSender`: logs the address masked to `r***@domain` and the subject); empty means `SMTP` when an SMTP host is set, else `DEVELOPMENT` (D322). `IPushSender` chosen by `Notifications:Push:Provider` — `FCM` (`FcmPushSender`, NOT-R14), `DISABLED` (`DisabledPushSender`) or `DEVELOPMENT` (`LoggingPushSender`, logs the device count and title; also what empty means) (D325). The development senders store `provider_message_id` = `DEVELOPMENT_NOT_DELIVERED` (D323). Outside Development, Testing and Demo the Api and Worker refuse to start with a development e-mail or push sender, an incomplete SMTP or FCM configuration, or an unknown provider (rule 7); `DISABLED` is allowed and logged Critical at every start. Hosts or tests register their own sender first (`TryAdd`). Settings: `runbooks/security-configuration.md` §2.2–2.3. |
| NOT-R12 | DLT SMS (E2E-1). `DltSmsSender` is provider-neutral: it maps the template code to `Notifications:Sms:Dlt:Templates:<CODE>` (the DLT content template id; the `templates.dlt_template_id` column is not used for sending), accepts only Indian mobiles (`+91` + 10 digits starting 6–9), and hands `{ number, senderId, principalEntityId, dltTemplateId, text, variables }` to the `IDltSmsGateway` named by `Notifications:Sms:Dlt:Gateway` (`FAST2SMS`: `Fast2SmsGateway`, route `dlt_manual`, key in the `authorization` header, timeout `TimeoutSeconds`). `variables` are the placeholder values in order of appearance in the template body, which is the order of the `{#var#}` slots of the registered DLT text (`TemplateText.OrderedValues`, or `ExtractValues` from a queued message's body). Start-up requires `Gateway`, `ApiKey`, `SenderId` (3–6 letters/digits), `PrincipalEntityId` (digits), `TimeoutSeconds` 1–60, an https `BaseUrl` when set (empty = the gateway's endpoint) and a template id (digits) for **every** SMS template code (`DltTemplateCodes.All`, kept equal to the reference data by a test). Every send failure is `SmsDeliveryException` with a kind: `NOT_CONFIGURED`, `TEMPLATE_NOT_REGISTERED`, `RECIPIENT_UNSUPPORTED`, `UNAUTHORIZED`, `REJECTED`, `UNREACHABLE`. In a request (OTP, test send) it answers **503 `SMS_PROVIDER_UNAVAILABLE`** with a fixed detail; in the Worker `last_error` = `SMS_<KIND>`, and only `UNREACHABLE` and `UNAUTHORIZED` are retried (NOT-R6), the rest are FAILED at once. Logs carry the template code and the last 2 phone digits only, never the text or the key. Owner registration steps: `runbooks/security-configuration.md` §2.1; provider choice: `research/sms-provider-2026-10.md`. |
| NOT-R13 | `BACK_IN_STOCK` (category `ORDER_UPDATES`; channels EMAIL, PUSH, IN_APP, no SMS so no DLT template is needed) is queued once per `StockAlertTriggered`, with `productName`, `variantName`, `productSlug`. |
| NOT-R14 | FCM push (D324, BE-3 / L-20). `FcmPushSender` signs an RS256 JWT with the service account key (`Notifications:Push:Fcm:ServiceAccountJson`, scope `firebase.messaging`), trades it at the key's `token_uri` for an OAuth access token (cached until 5 minutes before expiry; forgotten after a refused send) and posts one `POST https://fcm.googleapis.com/v1/projects/{ProjectId}/messages:send` per device token with `notification { title, body }` and `data { templateCode, messageId }` plus `orderId` / `productSlug` when the message's payload has them (NOT-R15). FCM `UNREGISTERED` or `SENDER_ID_MISMATCH` (or a bare 404) marks the token invalid. When no device accepted, it throws `PushDeliveryException` with kind `UNREACHABLE` (network, timeout, 429, 5xx, `UNAVAILABLE`, `INTERNAL`, `QUOTA_EXCEEDED`), `UNAUTHORIZED` (401, 403, `THIRD_PARTY_AUTH_ERROR`, token exchange refused), `REJECTED` (other 4xx) or `NOT_CONFIGURED`; a retryable kind wins when devices failed differently. Start-up requires a project id (lower-case letters, digits, dashes), a key JSON with `client_email` and an RSA `private_key` in PEM, an https `token_uri` when set, and `TimeoutSeconds` 1–60. No Firebase SDK: plain `HttpClient` and `System.Security.Cryptography` (rule 13). Logs carry counts and codes only. The app registers real FCM tokens (D349, NOT-R15). |
| NOT-R15 | Push taps (D351). Every customer order message carries `orderId` (the order's UUID) in its payload, set by the event adapters (`CustomerNotifier.Id`); `BACK_IN_STOCK` carries `productSlug`. The dispatcher copies only these link keys (`MessageDispatcher.PushLinkKeys`) from the payload into the push `data`, next to `templateCode` and `messageId`; names, amounts and codes stay in the notification text. Templates do not use `orderId`. The app opens `/orders/{orderId}` for an order message, `/product/{slug}` for back in stock and the inbox for any other message (`docs/design/mobile.md` §10). The app registers its token while signed in and permitted (after sign-in, at launch, on token refresh, after the customer allows notifications) and removes it on sign-out (D349); it asks for the permission once after the first order, then only from Settings (D352). |

## 4. Event → template mapping (customer)

Wired (D30): the events live in the owners' `.Public/Events`; `Notifications.Application/Handlers/CustomerNotificationHandlers.cs`
handles each one and queues the template below for the order's customer, with the event id as `DedupeKey`.
Every order template's data also holds `orderId` for the app's tap (NOT-R15, D351); the table lists the template variables.

| Event (owner) | Template | Data |
|---|---|---|
| `OrderConfirmed` / `OrderPlaced` with status CONFIRMED (Ordering) | `ORDER_CONFIRMED` | orderNumber, slotDateText, slotWindowText, estimatedTotalText, paymentMethodText, orderUrl |
| `OrderStatusChanged` → PREPARING | `ORDER_PREPARING` | orderNumber |
| `OrderPacked` | `ORDER_PACKED` | orderNumber, finalTotalText, amountDueText, weightNoteText |
| `OrderDispatched` | `ORDER_OUT_FOR_DELIVERY` | orderNumber, riderName, riderPhone, deliveryCode, amountDueText, slotWindowText, trackUrl |
| `OrderDelivered` | `ORDER_DELIVERED` | orderNumber, invoiceNumber, finalTotalText, orderUrl |
| `OrderStatusChanged` → DELIVERY_FAILED | `DELIVERY_FAILED` | orderNumber, reasonText |
| `OrderCancelled`, `OrderPaymentFailed` (timeout) | `ORDER_CANCELLED` | orderNumber, reasonText, refundAmountText |
| `SubstitutionProposed` | `SUBSTITUTION_PROPOSED` | as reference data |
| `PaymentCaptured` (Payments) | `PAYMENT_RECEIVED` | orderNumber, amountText, paymentMethodText |
| `PaymentFailed` | `PAYMENT_FAILED` | orderNumber, amountText, retryUrl |
| `RefundApproved` / `RefundSucceeded` | `REFUND_INITIATED` / `REFUND_SUCCEEDED` | orderNumber, amountText, refundMethodText, referenceText |
| `ComplaintRaised` / `ComplaintDecided` | `COMPLAINT_RECEIVED` / `COMPLAINT_RESOLVED` | as reference data |

Every mapping passes the event id as `DedupeKey` (NOT-R4).

### Staff alerts

`Notifications.Application/Handlers/StaffNotificationHandlers.cs`, same rules (event id as `DedupeKey`, never saves).

| Event (owner) | Template | Recipients | Data |
|---|---|---|---|
| `StaffAccountLocked` (Identity, D33) | `STAFF_ACCOUNT_LOCKED` (EMAIL) | `IIdentityApi.GetStaffUserIdsWithPermissionAsync("staff.manage")`: active staff holding `staff.manage`, ADMIN always; a locked or disabled admin is skipped | staffEmail (read with `GetUserAsync`, not carried in the event), lockedUntilText (business time zone), lockoutCountText |

One `QueueAsync` per recipient: the recipients are the few administrators, so this loop is accepted in the handler.
