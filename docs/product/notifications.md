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
| `IOtpSender` (Identity.Public) | Renders `OTP_LOGIN` / `OTP_VERIFY_PHONE` and sends the SMS immediately; nothing stored. | Identity |

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
| NOT-R6 | Worker job `notifications.send` (every minute) sends QUEUED SMS/EMAIL/PUSH in batches of 50 (`FOR UPDATE SKIP LOCKED`): SMS to the user's phone (`ISmsSender`), EMAIL to the user's email (`IEmailSender`, SMTP via MailKit; Mailpit in dev), PUSH to every device token (`IPushSender`; FCM adapter later, dev logging no-op). A provider error → attempts + 1 and `next_attempt_at` = now + attempts minutes; at 5 → FAILED. No address (no phone, no email, no device) → FAILED (`NO_RECIPIENT_ADDRESS`). Tokens the provider reports invalid are removed. |
| NOT-R7 | Device tokens (Ruling D28, Flutter app): `PUT /me/device-tokens` with `{ pushToken, provider: "FCM", platform: ANDROID/IOS/WEB }` registers the FCM registration token (or moves it to the caller), `last_seen_at` refreshed; another provider → 422; DELETE removes only the caller's token (unknown token → 204). |
| NOT-R8 | Preferences PUT replaces the full set; categories ORDER_UPDATES, OFFERS × channels SMS, EMAIL, PUSH, IN_APP; disabling ORDER_UPDATES SMS → 422 `PREFERENCE_REQUIRED`. |
| NOT-R9 | In-app list: the caller's IN_APP messages newest first, `unreadOnly` filter; mark read is idempotent; read-all marks every unread one. |
| NOT-R10 | Test send: renders the template with sample values (`[name]`) and sends to the calling staff member's own email/phone immediately; nothing stored. |
| NOT-R11 | Senders are adapters: `ISmsSender` chosen by `Notifications:Sms:Provider` — `DEVELOPMENT` (`DevelopmentSmsSender`: keeps messages in an in-memory outbox read via `GET /dev/sms`, logs only the phone masked to its last 4 digits and the length, never the text) or `DLT` (`DltSmsSender`, a stub: refuses when `Notifications:Sms:Dlt` (BaseUrl https, ApiKey, SenderId, PrincipalEntityId) is incomplete or the template has no `dlt_template_id`, and throws until the provider client is written). The Api and Worker refuse to start with the development sender outside Development/Testing, with an incomplete DLT configuration, or with an unknown provider (SEC-1, rule 7). `IEmailSender` (`SmtpEmailSender`; `LoggingEmailSender` when no SMTP host), `IPushSender` (`LoggingPushSender`, a no-op that logs; FCM adapter later). Hosts or tests register their own sender first (`TryAdd`). |

## 4. Event → template mapping (customer)

Not wired yet: the Ordering / Payments event classes do not exist, and `ProjectReferenceRuleTests` forbids
Notifications from referencing another module's Domain project (where events live). Until that rule changes, the
owning module calls `INotificationsApi.QueueForCustomerAsync(...)` from its own handler (or in its transaction) with
the event id as `DedupeKey`, using the template and data below.

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
