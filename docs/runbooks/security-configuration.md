# Security configuration (Api and Worker)

These are the settings an operator must get right before real customers can reach the API. Every setting is
configuration. Environment variables use `__` in place of `:`, for example `ForwardedHeaders__KnownNetworks__0`.
A missing or invalid value stops the host at startup (rule 7); nothing falls back silently. For background, see
SEC-1, SEC-2 and SEC-3 in `docs/security/review-2026-10.md`, and decision D36.

## 1. Trusted proxy: `ForwardedHeaders:KnownNetworks` (Api)

The API sits behind Caddy (D25). Rate limits and the OTP per-IP cap are keyed on the client IP. The API takes that
IP from `X-Forwarded-For` **only** when the direct peer is in `ForwardedHeaders:KnownNetworks`.

| Value | Effect |
|---|---|
| `[]` (default) | Nothing is forwarded, not even from loopback. Behind Caddy, every customer then shares Caddy's IP, so one client can use up the OTP and login limits for everyone. Use the empty list only when clients reach the API directly. |
| The Caddy network, for example `["172.20.0.0/16"]` | `X-Forwarded-For` and `X-Forwarded-Proto` from Caddy are applied, one hop only (`ForwardLimit = 1`). |
| An invalid entry | The Api refuses to start. |

Each entry is a CIDR network or a single address (`10.0.0.5` means `/32`).

Steps:

1. In the compose file, give Caddy and the API a dedicated Docker network with a fixed subnet
   (`ipam.config.subnet`), so the CIDR stays the same between deploys.
2. On the Api container, set `ForwardedHeaders__KnownNetworks__0=<that subnet>`. List only that network. Never list
   `0.0.0.0/0`, a public range, or a network that other containers or tenants share.
3. Make sure Caddy sets `X-Forwarded-For` itself (its `reverse_proxy` does this by default). Do not publish the API
   port to the host or the internet: every client must go through Caddy to reach the API.
4. Check: request an OTP twice through Caddy, from two different client IPs. The `identity.otp_challenges.request_ip`
   values must be the two client IPs, not Caddy's.

## 2. SMS sender: `Notifications:Sms` (Api and Worker)

Outside Development and Testing, SMS goes through the `DLT` sender (E2E-1). It maps each of our template codes to its
DLT content template id and sends through one provider adapter (`Gateway`). Today the only adapter is `FAST2SMS`
(why: `docs/research/sms-provider-2026-10.md`).

| Key (environment variable in `.env.prod`) | Values |
|---|---|
| `Notifications:Sms:Provider` | `DEVELOPMENT` (Development and Testing only) or `DLT`. Fixed to `DLT` in `docker-compose.prod.yml` |
| `Notifications:Sms:Dlt:Gateway` (`SMS_DLT_GATEWAY`) | `FAST2SMS` |
| `Notifications:Sms:Dlt:BaseUrl` (`SMS_DLT_BASE_URL`) | Optional. Empty means the gateway's documented endpoint (`https://www.fast2sms.com/`). When set, it must be `https` |
| `Notifications:Sms:Dlt:ApiKey` (`SMS_DLT_API_KEY`) | The provider's API key. A secret: keep it in `.env.prod` on the server only, never in a committed file |
| `Notifications:Sms:Dlt:SenderId` (`SMS_DLT_SENDER_ID`) | The DLT-approved header, 3 to 6 letters or digits |
| `Notifications:Sms:Dlt:PrincipalEntityId` (`SMS_DLT_PRINCIPAL_ENTITY_ID`) | The DLT principal entity id (digits) |
| `Notifications:Sms:Dlt:TimeoutSeconds` (`SMS_DLT_TIMEOUT_SECONDS`) | 1 to 60, default 10 |
| `Notifications:Sms:Dlt:Templates:<CODE>` (`SMS_DLT_TEMPLATE_<CODE>`) | The DLT content template id (digits) of each SMS template in the table below. **Every one is required** |

What the hosts refuse:
- Outside Development and Testing, the Api and Worker refuse to start (one Critical log line, exit code 1, E2E-2)
  when the provider is `DEVELOPMENT`, empty or unknown, or when any `Dlt` value above is missing or malformed. That
  includes a missing template id for any of the 13 SMS templates.
- When a send fails (provider unreachable, timed out, key refused, message rejected), an OTP request answers
  **503 `SMS_PROVIDER_UNAVAILABLE`**, never 500. The Worker retries queued SMS only after an outage or a refused key
  (up to 5 attempts). A rejected message is marked FAILED at once, with `last_error` `SMS_REJECTED`.
- SMS text is never logged by the DLT sender: a log line has the template code and the last 2 digits of the phone
  (`**10`), plus the failure code and the provider's status code. The API key is sent only in the `authorization`
  header.
- In Development, read OTPs from `GET /api/v1/dev/sms?phone=+91…` (newest first). This route does not exist in other
  environments.

### 2.1 What the owner registers (once, before launch)

DLT registration is done by the business, not by us. Each step's id goes into `.env.prod`.

1. **Principal entity (PE).** Register Fresh Orbit as a principal entity on one operator's DLT portal (for example
   Jio, Airtel, Vi or BSNL; one is enough, as all operators share it). You will need the company PAN, GST and a
   letter of authorisation. After approval, copy the **PE id** → `SMS_DLT_PRINCIPAL_ENTITY_ID`.
2. **Header (sender id).** On the same portal, register a header for transactional and service messages, for
   example `FRSHOR` (up to 6 characters, related to the brand name) → `SMS_DLT_SENDER_ID`.
3. **Content templates.** Register one content template per row below, under the header from step 2:
   - **Type:** OTP_LOGIN and OTP_VERIFY_PHONE as **Transactional** (the category for OTPs). Every other row as
     **Service Implicit**.
   - **Text:** exactly the "DLT text" column, character for character. Each `{#var#}` is one variable. Our text
     must match the registered one exactly (spaces, full stops, dashes), or the operator drops the message.
   - After approval, copy each **content template id** → its `SMS_DLT_TEMPLATE_<CODE>`.
   - Variables are filled in this order: the "Variables" column. Keep each value within the portal's variable
     length limit (30 characters on the portals we know of, not verified). `businessName` is the Settings trade
     name (or `Notifications:BusinessName`).
4. **Provider account.** Open a Fast2SMS account (free ₹50 test credit), complete its DLT details with the same PE
   id, header and templates, and create an **API key** → `SMS_DLT_API_KEY`. Top up the prepaid balance before
   launch.
5. **Link the provider to the entity.** On the DLT portal, add Fast2SMS as your telemarketer (the "telemarketer
   chain" or "TM binding"; the menu name differs by portal). Without it, the operator refuses every message from
   the provider for your PE.
6. **Check before opening the store.** Deploy, request an OTP for your own phone, and confirm that the SMS arrives
   and that the Api log line shows `SMS OTP_LOGIN to **NN accepted by FAST2SMS`. A 503 `SMS_PROVIDER_UNAVAILABLE`
   means the log line gives the reason (`SMS_UNAUTHORIZED`: the key; `SMS_REJECTED` with a provider status: the PE,
   header, template or text; `SMS_UNREACHABLE`: network or timeout).

| Code | Environment variable | DLT text (register exactly) | Variables, in order |
|---|---|---|---|
| `OTP_LOGIN` | `SMS_DLT_TEMPLATE_OTP_LOGIN` | `{#var#} is your {#var#} login code. It expires in {#var#} minutes. Do not share it with anyone.` | code, businessName, expiresInMinutes |
| `OTP_VERIFY_PHONE` | `SMS_DLT_TEMPLATE_OTP_VERIFY_PHONE` | `{#var#} is your {#var#} code to verify this phone number. It expires in {#var#} minutes.` | code, businessName, expiresInMinutes |
| `ORDER_CONFIRMED` | `SMS_DLT_TEMPLATE_ORDER_CONFIRMED` | `Order {#var#} confirmed. Delivery {#var#}, {#var#}. Est. total {#var#} ({#var#}). - {#var#}` | orderNumber, slotDateText, slotWindowText, estimatedTotalText, paymentMethodText, businessName |
| `PAYMENT_RECEIVED` | `SMS_DLT_TEMPLATE_PAYMENT_RECEIVED` | `Payment of {#var#} received for order {#var#} ({#var#}). - {#var#}` | amountText, orderNumber, paymentMethodText, businessName |
| `PAYMENT_FAILED` | `SMS_DLT_TEMPLATE_PAYMENT_FAILED` | `Payment of {#var#} for order {#var#} failed. Retry: {#var#} - {#var#}` | amountText, orderNumber, retryUrl, businessName |
| `SUBSTITUTION_PROPOSED` | `SMS_DLT_TEMPLATE_SUBSTITUTION_PROPOSED` | `Order {#var#}: {#var#} is unavailable. Accept {#var#} ({#var#}) by {#var#}: {#var#}` | orderNumber, itemName, proposedItemName, priceDifferenceText, respondByText, orderUrl |
| `ORDER_OUT_FOR_DELIVERY` | `SMS_DLT_TEMPLATE_ORDER_OUT_FOR_DELIVERY` | `Order {#var#} is out for delivery with {#var#} ({#var#}). Code {#var#}. Due {#var#}. - {#var#}` | orderNumber, riderName, riderPhone, deliveryCode, amountDueText, businessName |
| `ORDER_DELIVERED` | `SMS_DLT_TEMPLATE_ORDER_DELIVERED` | `Order {#var#} delivered. Total {#var#}, invoice {#var#}. Thank you! - {#var#}` | orderNumber, finalTotalText, invoiceNumber, businessName |
| `DELIVERY_FAILED` | `SMS_DLT_TEMPLATE_DELIVERY_FAILED` | `We could not deliver order {#var#}: {#var#}. We will contact you. - {#var#} {#var#}` | orderNumber, reasonText, businessName, supportPhone |
| `ORDER_CANCELLED` | `SMS_DLT_TEMPLATE_ORDER_CANCELLED` | `Order {#var#} was cancelled: {#var#}. {#var#} - {#var#}` | orderNumber, reasonText, refundAmountText, businessName |
| `REFUND_INITIATED` | `SMS_DLT_TEMPLATE_REFUND_INITIATED` | `Refund of {#var#} for order {#var#} started to {#var#}. - {#var#}` | amountText, orderNumber, refundMethodText, businessName |
| `REFUND_SUCCEEDED` | `SMS_DLT_TEMPLATE_REFUND_SUCCEEDED` | `Refund of {#var#} for order {#var#} completed to {#var#}. Ref {#var#}. - {#var#}` | amountText, orderNumber, refundMethodText, referenceText, businessName |
| `COMPLAINT_RESOLVED` | `SMS_DLT_TEMPLATE_COMPLAINT_RESOLVED` | `Your complaint on order {#var#} is resolved: {#var#}. {#var#} - {#var#}` | orderNumber, resolutionText, refundAmountText, businessName |

**Editing an SMS text.** Staff can edit template texts in the admin dashboard (Notification templates). An SMS text
must stay equal to its registered DLT text: register the new text on the DLT portal first, wait for approval, put
the new id in `SMS_DLT_TEMPLATE_<CODE>`, redeploy, and only then save the edited text. The `dltTemplateId` shown on
a template in the admin API is not used for sending; the configuration is the source (E2E-1).

## 3. Rate limits: `RateLimiting:<Rule>` (Api)

Each rule has a `PermitLimit` and a `WindowSeconds`. Both must be above 0, or the Api refuses to start. The rules
are `OtpRequest`, `OtpVerify`, `Login`, `Refresh`, `Checkout`, `Search`, `Public` and `Coupon`; their defaults and
partition keys are in `docs/api/README.md` §1.9.

`RateLimiting:Otp` no longer exists. In any deployed configuration, rename it to `OtpRequest` and `OtpVerify`.

Limits are counted per instance, in memory. With more than one Api instance, each instance allows the full limit,
so the effective limit is multiplied by the number of instances.

### 3.1 Data Protection keys: `DataProtection:KeysPath` (Api)

An absolute directory for the ASP.NET Core Data Protection key ring, application name `FreshOrbit`. Required outside
Development and Testing; a missing or relative path stops the Api at start-up (exit 1). In `docker-compose.prod.yml` it
is `/keys` on the `dataprotection-keys` volume (mode 700, app user). Nothing protects data with it yet (JWT only), so
the keys are not encrypted or backed up; revisit both when a feature starts using it (for example the D40 staff
refresh cookie). Details: `research/deploy-gaps-ops-2026-10.md`.

## 4. Staff login lockout: `Identity:*` (Api)

D33, IDN-R11, IDN-R12 and IDN-R20 in `docs/product/identity.md`. The defaults below are the documented values, so
change them only for a good reason.

| Setting | Default | Meaning |
|---|---|---|
| `Identity:StaffMaxFailedLogins` | 10 | Consecutive wrong passwords that lock a staff account |
| `Identity:StaffLockoutMinutes` | 15 | Length of the first lock. Each further lock since the last successful sign-in doubles it |
| `Identity:StaffMaxLockoutHours` | 24 | The longest lock |
| `Identity:StaffLoginFailuresPerEmailIp` | 5 | Failed logins allowed per email + client IP in the window, then 429 `RATE_LIMITED` |
| `Identity:StaffLoginFailureWindowMinutes` | 15 | Window for the per email + IP count |

- Every refusal answers 401 `INVALID_CREDENTIALS`: a wrong password, an unknown email, and a locked, disabled or
  inactive account, whatever the password.
- A lock ends by itself at `identity.users.locked_until`. No administrator action is needed.
- When an account is locked, every active staff member holding `staff.manage` (ADMIN always) gets the
  `STAFF_ACCOUNT_LOCKED` e-mail, which gives the unlock time. The Worker must be running, and SMTP must be configured
  for the e-mail to go out.
- The per email + IP count is stored in `identity.staff_login_failures`, so it holds across Api instances and
  restarts (unlike §3). It relies on the real client IP, so §1 (`ForwardedHeaders:KnownNetworks`) must be right.

## 5. Break-glass: an administrator cannot sign in

Try these in order. Never paste a password into a ticket or chat, and never run commands with `set -x`.

1. **Locked by wrong passwords.** Wait for the lock to end. The `STAFF_ACCOUNT_LOCKED` e-mail gives the time, and
   no lock lasts longer than 24 hours. If another administrator can sign in, they can unlock at once: in the admin
   dashboard, **Staff → the account → Reset password**, which sets a temporary password, unlocks the account and
   resets the doubling. If the e-mail arrives and nobody caused the lock, someone may be guessing the password, so
   check the Api logs for the client IPs.
2. **No active ADMIN at all** (for example, every admin was deactivated in the database). Run the Migrations host
   once with `--create-admin` and a **new** email. It is refused with `ADMIN_ALREADY_EXISTS` while any active staff
   member holds ADMIN, even a locked one, and with `DUPLICATE` when the email already exists. The password must meet
   the policy: at least 12 characters and not a common password (`PASSWORD_TOO_WEAK`).

   ```bash
   cd /opt/freshorbit/infra/compose
   read -rs ADMIN_PASSWORD            # typed, not echoed, and not in the shell history
   IMAGE_TAG=$(cat ../../.deploy/current-tag) docker compose --env-file .env.prod -f docker-compose.prod.yml \
     run --rm migrations --create-admin --email rescue.admin@<domain> --name "Rescue Admin" --password "$ADMIN_PASSWORD"
   unset ADMIN_PASSWORD
   ```

   The command logs the email and the new staff member id, never the password. The password is passed as an
   argument, so it is visible in the container's process list while the command runs. Run it only on the server,
   and change the password after the first sign-in (`POST /admin/auth/password`).
3. **The only admin is locked and cannot wait, or has forgotten the password.** `--create-admin` does not help
   here: the admin is still active, so the command is refused. Until a dedicated command exists, use SQL in the
   Postgres container, and record what you did in the audit notes:

   ```bash
   IMAGE_TAG=$(cat ../../.deploy/current-tag) docker compose --env-file .env.prod -f docker-compose.prod.yml \
     exec postgres psql -U freshorbit -d freshorbit
   ```

   - **Locked, password known:** end the lock now.
     ```sql
     UPDATE identity.users SET status = 'ACTIVE', locked_until = NULL, failed_login_count = 0, lockout_count = 0
     WHERE email = 'admin@<domain>' AND user_type = 'STAFF' AND status = 'LOCKED';
     ```
   - **Password forgotten:** deactivate that staff member, then follow step 2 with a new email. Afterwards the new
     admin resets the old account's password in the dashboard and re-activates it.
     ```sql
     UPDATE identity.staff_members SET is_active = false
     WHERE user_id = (SELECT id FROM identity.users WHERE email = 'admin@<domain>' AND user_type = 'STAFF');
     ```
