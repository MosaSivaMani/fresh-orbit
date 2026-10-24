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
