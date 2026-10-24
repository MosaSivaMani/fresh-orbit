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

| Key | Values |
|---|---|
| `Notifications:Sms:Provider` | `DEVELOPMENT` (Development and Testing only) or `DLT` |
| `Notifications:Sms:Dlt:BaseUrl` | The provider's API base URL; must be `https` |
| `Notifications:Sms:Dlt:ApiKey` | The provider secret. Keep it in the secret store or the environment, never in a committed file |
| `Notifications:Sms:Dlt:SenderId` | The DLT-registered sender header |
| `Notifications:Sms:Dlt:PrincipalEntityId` | The TRAI DLT principal entity id |

- Outside Development and Testing, the hosts refuse to start when the provider is `DEVELOPMENT`, empty or unknown,
  or when any `Dlt` setting is missing.
- Until a provider is chosen, the `DLT` sender is a stub. It starts when configured, but every send throws, so OTP
  requests fail with 500 rather than pretending a code was sent. Do not open the store to customers until the
  provider client is written.
- SMS text is never logged. Phone numbers appear in the logs as `***` followed by the last 4 digits.
- In Development, read OTPs from `GET /api/v1/dev/sms?phone=+91…` (newest first). This route does not exist in other
  environments.

## 3. Rate limits: `RateLimiting:<Rule>` (Api)

Each rule has a `PermitLimit` and a `WindowSeconds`. Both must be above 0, or the Api refuses to start. The rules
are `OtpRequest`, `OtpVerify`, `Login`, `Refresh`, `Checkout`, `Search`, `Public` and `Coupon`; their defaults and
partition keys are in `docs/api/README.md` §1.9.

`RateLimiting:Otp` no longer exists. In any deployed configuration, rename it to `OtpRequest` and `OtpVerify`.

Limits are counted per instance, in memory. With more than one Api instance, each instance allows the full limit,
so the effective limit is multiplied by the number of instances.

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
