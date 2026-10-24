# Identity module

Owner: E1. Schema `identity`. Uses no other module's `.Public`. Endpoints: `docs/api/README.md` §4.1, §4.21
(staff, roles, permissions). Error codes: `docs/api/README.md` §2 (Identity, Common).

## 1. Aggregates and boundaries

| Aggregate root | Tables | Children | Row version |
|---|---|---|---|
| User | `users` | — | `users.xmin` |
| OtpChallenge | `otp_challenges` | — | `otp_challenges.xmin` |
| RefreshToken | `refresh_tokens` | — | `refresh_tokens.xmin` (two concurrent rotations of one token: one wins, the other gets 409 `CONCURRENT_SAVE`) |
| StaffMember | `staff_members` | `staff_roles`, `staff_location_access` | `staff_members.xmin` |
| Role | `roles` | `role_permissions` | `roles.xmin` |
| Permission | `permissions` | — | reference data, read-only |

| Boundary rule |
|---|
| Identity never references Customers. The customer record is created through `ICustomerAccountDirectory` (interface in `Identity.Public`, implemented by Customers), tracked on the shared context and saved in the login transaction. The JWT `customer_id` comes from the same interface. |
| The OTP SMS is sent through `IOtpSender` (interface in `Identity.Public`, implemented by Notifications with the `OTP_LOGIN` template and its `ISmsSender`). The code is never stored in clear and never written to `notifications.messages`. |
| Guest cart merge is not done by Identity: OTP login raises `CustomerSignedIn` with the guest cart token; Cart merges in its handler (seconds of lag). The verify response therefore returns `cart: null`. |

### Public API (`Identity.Public`)

| Member | Returns | Used by |
|---|---|---|
| `IIdentityApi.GetUserAsync(userId)` / `GetUsersAsync(ids)` | `UserSummary` (phone, email, type, status); email falls back to the customer's profile email | Customers, Notifications, Payments |
| `IIdentityApi.FindUserIdByPhoneAsync(phone)` | user id or null | Customers (admin search) |
| `IIdentityApi.GetStaffMembersAsync(ids)` | `StaffSummary` (name, email, active) | Payments (names in lists) |
| `IIdentityApi.GetStaffUserIdsWithPermissionAsync(permission, locationId?)` | active staff user ids | Notifications (staff alerts) |
| `IIdentityApi.SetUserBlockedAsync(userId, blocked)` | tracked change; revokes refresh tokens when blocking | Customers (admin block/unblock) |
| `IIdentityApi.AnonymiseUserAsync(userId)` | tracked change: phone/email cleared, `DISABLED`, tokens revoked | Customers (account deletion) |
| `ICustomerAccountDirectory` (implemented by Customers) | ensure customer, customer id ↔ user id, customer email | Identity, Notifications |
| `IOtpSender` (implemented by Notifications) | sends the OTP SMS now | Identity |

## 2. States

### User (`users.status`)

| From | To | Trigger |
|---|---|---|
| — | ACTIVE | First OTP verify (customer) / staff created / `--create-admin` |
| ACTIVE | LOCKED | 10 consecutive wrong staff passwords (IDN-R12) |
| LOCKED | ACTIVE | Staff reset-password by `staff.manage` |
| ACTIVE, LOCKED | DISABLED | Customer blocked, account deleted, staff deactivated |
| DISABLED | ACTIVE | Customer unblocked, staff re-activated (never after account deletion) |

### OtpChallenge

`open` → `consumed` (right code) | `expired` (5 min) | `exhausted` (5 wrong codes). Only an open challenge verifies.

### RefreshToken

`active` → `rotated` (`revoked_at` + `replaced_by_id`) | `revoked` (logout, block, password change, family revoked) | `expired`.

## 3. Rules

| Key | Rule |
|---|---|
| IDN-R1 | Phone is E.164 (`+` and 8–15 digits). OTP is 6 digits from a CSPRNG, stored as HMAC-SHA256 (keyed by the JWT signing key, salted with the challenge id), valid 5 minutes. |
| IDN-R2 | `POST /auth/otp/request` answers 202 for every well-formed phone (no enumeration). A disabled user gets 202 but no SMS. |
| IDN-R3 | Resend window: a new request for the same phone within 30 s of the previous challenge → 429 `OTP_RESEND_TOO_SOON` with `retryAfterSeconds`. |
| IDN-R4 | Per-phone limit 3 challenges / 10 min and 10 / day; per-IP limit 20 / hour (counted from `otp_challenges`) → 429 `RATE_LIMITED` with `retryAfterSeconds`. The host IP limiters (`otp-request` and `otp-verify` policies) apply too. |
| IDN-R5 | Verify: unknown challenge, other phone or wrong code → 422 `OTP_INVALID`; consumed or expired → 422 `OTP_EXPIRED`; the 5th wrong code and every later try → 429 `OTP_TOO_MANY_ATTEMPTS`. The attempt counter is saved before the refusal is returned. |
| IDN-R6 | First verify creates the user (CUSTOMER, ACTIVE, `phone_verified_at`) and, through `ICustomerAccountDirectory`, the customer (`isNewCustomer = true`). A DISABLED user → 403 `ACCOUNT_DISABLED`. |
| IDN-R7 | Access token: JWT from `AccessTokenFactory` (15 min): `sub`, `actor_type`, `customer_id` or `staff_id`, `perm` per permission, `loc` per location or `all_loc`. |
| IDN-R8 | Refresh token: opaque `rt_` + 32 random bytes (base64url), stored as SHA-256; customer 30 days, staff 12 h; each refresh rotates (old token `revoked_at`, `replaced_by_id`), new token keeps the family. |
| IDN-R9 | Presenting a rotated token revokes every active token of the family → 401 `REFRESH_TOKEN_REUSED`. Unknown, expired or revoked token → 401 `REFRESH_TOKEN_INVALID`. A user no longer ACTIVE → 401 `REFRESH_TOKEN_INVALID`. |
| IDN-R10 | Logout revokes the presented token (only the caller's own), or every active token of the user with `allDevices = true`. |
| IDN-R11 | Staff login: unknown email or wrong password → 401 `INVALID_CREDENTIALS` (same answer). Wrong password increments `failed_login_count`; success resets it. |
| IDN-R12 | 10 consecutive failures → user LOCKED; a LOCKED user → 403 `ACCOUNT_LOCKED`; inactive staff or DISABLED user → 403 `ACCOUNT_DISABLED`. |
| IDN-R13 | Passwords: at least 12 characters and not in a small built-in list of common passwords → 422 `PASSWORD_TOO_WEAK`. Hashed with ASP.NET Core `PasswordHasher`. |
| IDN-R14 | Change password needs the current password (`INVALID_CREDENTIALS` 401 when wrong) and revokes every refresh token of the user. Reset by `staff.manage` sets a temporary password, unlocks and revokes sessions. |
| IDN-R15 | Staff email is unique (citext) → 409 `DUPLICATE` (`errors.email`). Role ids must exist (`ROLE_NOT_FOUND`, 422); `allLocations=false` needs at least one location id. |
| IDN-R16 | Last admin: an update that would leave no active staff member holding the ADMIN role (remove the role or deactivate) → 422 `LAST_ADMIN`. |
| IDN-R17 | Roles: code UPPER_SNAKE, unique (409 `DUPLICATE`); permission codes must exist (422 `PERMISSION_UNKNOWN`). System roles cannot be renamed or deleted, and ADMIN's permissions cannot be changed (it always holds every permission) → 422 `SYSTEM_ROLE_READ_ONLY`. A role assigned to staff cannot be deleted → 409 `ROLE_IN_USE`. |
| IDN-R18 | Reference data: `permissions.json` SYNC (a DB code missing from the file fails the load), `roles.json` INSERT_IF_MISSING; ADMIN always receives every permission. |
| IDN-R19 | First admin (D18): `IFirstAdminService.CreateFirstAdminAsync(email, fullName, password)` creates a staff user with the ADMIN role and all locations; refused with `ADMIN_ALREADY_EXISTS` when any active staff holds ADMIN; needs the ADMIN role (reference data) → else `CONFIGURATION_MISSING`. |

## 4. Events

| Event | Payload | Consumers |
|---|---|---|
| `CustomerSignedIn` | userId, customerId, isNewCustomer, guestCartToken?, deviceLabel?, signedInAt | Cart (merge guest cart, raw token in payload; Cart hashes it) |
| `StaffMemberCreated` | staffMemberId, userId, email, fullName | Notifications (`STAFF_ACCOUNT_CREATED`, after the event rule allows it) |
| `StaffPasswordReset` | staffMemberId, userId | Notifications (`STAFF_PASSWORD_RESET`) |
| `RefreshTokenReuseDetected` | userId, familyId | (security log / future alert) |

## 5. Commands

| Command | Endpoint | Rules | Writes |
|---|---|---|---|
| RequestOtp | `POST /auth/otp/request` | R1–R4 | OtpChallenge; SMS via `IOtpSender` after save |
| VerifyOtp | `POST /auth/otp/verify` | R5–R8 | challenge consumed, User (new or login), Customer (via directory), RefreshToken; `CustomerSignedIn` |
| Refresh | `POST /auth/refresh` | R8, R9 | old token rotated, new token |
| Logout | `POST /auth/logout`, `POST /admin/auth/logout` | R10 | tokens revoked |
| StaffLogin | `POST /admin/auth/login` | R11–R12 | User counters, RefreshToken |
| ChangePassword | `POST /admin/auth/password` | R13, R14 | User, tokens |
| CreateStaff / UpdateStaff / ResetStaffPassword | `/admin/staff…` | R13–R16 | User, StaffMember, tokens |
| CreateRole / UpdateRole / DeleteRole | `/admin/roles…` | R17 | Role |
