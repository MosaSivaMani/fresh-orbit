# Runbook: Google Play Console answers (data safety, content rating, target audience)

Prepared answers for the Play Console forms of the Android app `in.freshorbit.app` (gap audit L-24, L-81, F-13).
They are taken from what the code collects on `main` (checked 2026-11-08, evidence below), not only from the privacy
draft. The owner fills the forms; the lawyer's review of `docs/legal/privacy-policy.md` (L-9) may change a row.
**Re-check this page whenever the app adds an SDK, a permission or a new kind of data** (push with Firebase, an online
payment gateway, crash reporting, a map pin): each of those changes an answer below, marked "changes when".

Building and signing the bundle: `mobile-release.md`. Not legal advice.

## 1. Facts the answers rest on

| Fact | Evidence |
|---|---|
| Android permissions: `INTERNET` and `POST_NOTIFICATIONS` (declared for push, asked only once push works, MA-6). No location, camera, contacts, storage or phone permission. App backup is off (`allowBackup="false"`) | `apps/mobile/android/app/src/main/AndroidManifest.xml:3,6,11` |
| No analytics, advertising, crash-reporting or push SDK in the app (dependencies: dio, riverpod, flutter_secure_storage, go_router, image_picker, intl, shared_preferences, uuid, url_launcher) | `apps/mobile/pubspec.yaml` |
| Crash reports (D309): only when the store build sets `CRASH_REPORT_DSN`, the app posts a scrubbed error message and stack (no user, no device id, no phone numbers, codes, tokens or e-mails; tags: OS name, release, environment) to our own GlitchTip or Sentry server. Without it, errors stay in the device log | `apps/mobile/lib/core/crash_reporter.dart:102-117` |
| Complaint photos are picked from the gallery (system photo picker), never the camera, and uploaded to our server | `apps/mobile/lib/core/photo_picker.dart:15` |
| Addresses are typed (pincode, lines, landmark); the app sends no map pin (`latitude: null`), although the API accepts one | `apps/mobile/lib/api/models/account.dart:153`; `Customers.Domain/Addresses/Address.cs:42-62` |
| Customer record: full name, optional e-mail, offers opt-in; user record: phone | `Customers.Domain/Customers/Customer.cs:24-28`; `Identity.Domain/Users/User.cs:30-42` |
| Payments are cash or UPI on delivery: method, amount, the UPI reference the rider records. A refund may store the customer's UPI id. No card or bank data | `Payments.Domain/Payments/PaymentEnums.cs:4-7`; `Payment.cs:46-79`; `Refund.cs:73` |
| OTP requests keep phone, client IP and time (abuse limits); the session keeps a fixed device label "Fresh Orbitt app" (not a device id) | `Identity.Domain/Otp/OtpChallenge.cs:31,43`; `apps/mobile/lib/state/session.dart:45` |
| Device push tokens have a table and endpoint, but the app has no push SDK, so it sends none today | `Notifications.Domain/Devices/DeviceToken.cs:29-37`; `apps/mobile/lib/core/push_service.dart` |
| Wishlist, stock alerts, cart, orders, complaints (text + up to 4 photos), order notes are stored on the server | Customers and Ordering modules |
| Search text is sent to the server for suggestions and results, not stored as history | `apps/mobile/lib/api/repositories.dart:134` |
| Service providers that receive data on our behalf: the SMS provider (phone, message text), the SMTP provider (e-mail, message text), the hosting and the encrypted offsite backup storage | `Notifications.Infrastructure/Senders/Dlt/Fast2SmsGateway.cs`; `Senders.cs` (`SmtpEmailSender`); `backup-restore.md` |
| All traffic is HTTPS: release builds take `API_BASE_URL=https://…` (`mobile-release.yml` refuses anything else), the manifest allows no cleartext, and Caddy serves only TLS with HSTS | `.github/workflows/mobile-release.yml`; `infra/caddy/Caddyfile` |
| Account deletion is in the app (Settings › Delete my account) and on the website after sign-in (`/account/settings`, "Delete account"). It clears name, e-mail, phone, saved addresses (only label, city, state and pincode stay), wishlist and stock alerts, and disables the sign-in. Orders, invoices, payments and refunds (with the delivery details copied onto each order) are kept 72 months for GST law. Refused while a complaint or refund is open | `Customers.Application/Profiles/ProfileService.cs:68-117`; `docs/legal/privacy-policy.md` §5 |

## 2. Data safety form

### 2.1 Overview questions

| Question | Answer |
|---|---|
| Does your app collect or share any of the required user data types? | **Yes** |
| Is all of the user data collected by your app encrypted in transit? | **Yes** |
| Which of the following methods of account creation does your app support? | **Phone number with OTP** (no username/password for customers, no third-party sign-in) |
| Do you provide a way for users to request that their data is deleted? | **Yes**: in the app, and by the web link in §2.4 |
| Independent security review | **No** (not done) |

"Shared" in Play's sense excludes service providers that process data for us (SMS, e-mail, hosting, backups) and
legal requests, so nothing below is **shared**. Changes when: an advertising or analytics SDK is added, or data goes
to a partner for its own use.

### 2.2 Data types collected

Every row: **collected = yes, shared = no, processed ephemerally = no**. "Required" means the user cannot use the
feature without it.

| Play category › type | Collected because | Required or optional | Purposes to tick |
|---|---|---|---|
| Personal info › **Name** | Profile and the recipient name on each address | Required (delivery needs a recipient) | App functionality, Account management |
| Personal info › **Email address** | Optional profile field, for receipts and order e-mails | Optional | App functionality, Account management, Developer communications (only if offers are opted in) |
| Personal info › **Phone number** | Sign-in by OTP, order SMS, the recipient phone on addresses | Required | App functionality, Account management, Fraud prevention, security and compliance |
| Personal info › **Address** | Delivery addresses and pincode serviceability | Required to order | App functionality |
| Financial info › **Purchase history** | Orders, invoices, payments (cash or UPI on delivery) | Required to order | App functionality, Fraud prevention, security and compliance (GST records) |
| Financial info › **Other financial info** | The UPI id a customer gives for a pay-on-delivery refund | Optional (only when refunded) | App functionality |
| Photos and videos › **Photos** | Up to 4 photos on a complaint, chosen from the gallery | Optional | App functionality |
| App activity › **App interactions** | Wishlist, stock alerts, cart | Optional | App functionality, Personalization |
| App activity › **Other user-generated content** | Complaint description, order notes | Optional | App functionality |
| App info and performance › **Crash logs** | **Only if the store build sets `CRASH_REPORT_DSN`** (D309): scrubbed error and stack sent to our own error server. The DSN must be `https://`, or "encrypted in transit" is no longer true. Without the variable, leave it unticked | Not optional for the user (no switch in the app) | Analytics (app stability). Not linked to the user: the report carries no user or device id |
| Device or other IDs | **Not collected today.** Changes when: Firebase push is added (the FCM token is a device id; purpose App functionality) | | |

Not collected (leave unticked): Location (approximate and precise; the app reads no device location and sends no
map pin; a typed address is "Address" above; changes when a map pin or location permission is added), Messages
(e-mails, SMS, in-app chat: the app reads none; SMS **we send** are not user messages), Contacts, Calendar, Health
and fitness, Audio, Files and docs, Web browsing, Installed apps, In-app search history (search text is used for the
answer and not kept as history), Diagnostics and other app performance data (nothing beyond the crash logs above),
Credit score, User payment info (no card or bank details; changes when an online payment
gateway is added: the gateway's SDK and its own data-safety answers then apply).

**IP address.** The server keeps the IP address with each OTP request to limit abuse. Play does not list IP address
as its own type; Google counts it under location only when it is used to work out where the user is, which we do
not do. If the reviewer asks, describe it under Phone number's "Fraud prevention, security and compliance" purpose.
Check this once with the lawyer (L-9).

### 2.3 Security practices

| Question | Answer |
|---|---|
| Data encrypted in transit | Yes (HTTPS only, TLS at Caddy) |
| Users can request data deletion | Yes |
| Committed to Play Families policy | Not applicable (not for children, §4) |

### 2.4 Account deletion URL (required by Play)

Play asks for a web page that explains how to delete the account and data **without** the app.

- Use `https://<domain>/support/privacy-policy` (§6 "Erase") until a dedicated page exists. It must say: sign in on
  the website › `/account/settings` › Delete account, or e-mail `SUPPORT_EMAIL` from the registered number's owner;
  what is deleted; what is kept 72 months (orders and invoices) and why.
- The dedicated `/support/delete-account` page is still to be built (gap audit L-82, website). Switch the URL in Play
  Console when it ships.

## 3. Content rating questionnaire (IARC)

| Question | Answer |
|---|---|
| Category | **Reference, News, or Educational? No. Choose "All other app types"** (a shopping app) |
| Violence, fear, sexuality, crude humour, gambling, drugs, profanity | **No** to every question (product pictures are fresh fish and seafood, cut and raw) |
| Does the app sell or promote alcohol or tobacco? | No |
| Do users interact or exchange content with each other? | **No** (complaints and notes go only to the store) |
| Does the app share the user's current physical location with other users? | No |
| Does the app allow users to purchase digital goods? | **No** (physical goods only, paid on delivery) |
| Is the app a web browser or search engine? | No |

Expected rating: Everyone / 3+ (IARC). The store only serves adults anyway (§4).

## 4. Target audience and content

| Question | Answer |
|---|---|
| Target age groups | **18 and over** only (the terms say the service is for people aged 18 or older; `docs/legal/terms-of-use.md`) |
| Could the app unintentionally appeal to children? | No (no games; the illustrated characters are adults; the store sells raw food) |
| Ads | **No ads** |
| News app | No |
| COVID-19 contact tracing or status | No |
| Government app | No |
| Financial features | None (no loans, no investments, no crypto). Payment is cash or UPI on delivery |
| Health features | None |

## 5. Store listing contacts (for the "App content" and "Store settings" pages)

| Field | Value |
|---|---|
| App category | Shopping (or Food & Drink; owner chooses) |
| Contact e-mail | `SUPPORT_EMAIL` from `.env.prod` (the same as the business profile) |
| Contact phone | `SUPPORT_PHONE` (optional) |
| Website | `https://<domain>` |
| Privacy policy | `https://<domain>/support/privacy-policy` (also linked inside the app since D299) |

## 6. Before submitting

1. Privacy policy published on the website with no `{{OWNER: …}}` left and reviewed by the lawyer (L-8, L-9), and its
   §2 matches §2.2 here. Today the draft still lists the app notification token and a map pin, which the app does not
   send yet; that is fine for a policy (it describes the service), but the form above must stay with what the app
   actually collects.
2. Upload a bundle from `mobile-release.yml` to the internal testing track first.
3. Re-read §1 against `main` and update this page in the same pull request as any change that alters an answer.
