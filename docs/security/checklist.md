# Pre-release security checklist

Run this before every release that reaches real users: staging soft launch, production, and every later deploy.
Copy the list into the release notes and record **Pass / Fail / N/A** with evidence (command output, file:line or a
screenshot). A failed **[block]** item stops the release. Other failed items need an owner and a date in
`docs/team/progress.md`.

Background for each item is in `docs/security/review-2026-10.md` (SEC-n references).

## 1. Configuration and secrets

- [ ] **[block]** `ASPNETCORE_ENVIRONMENT=Production` (or `Staging`) on the server, never `Development`.
- [ ] **[block]** `Jwt:SigningKey` comes from the environment or a secret file, is at least 64 random bytes in base64, and is not the committed development value (SEC-11).
- [ ] **[block]** The database password, SMTP credentials, payment webhook secrets and the SMS provider key come from the environment or secret files. None of them is in `appsettings*.json` or `docker-compose*.yml` in git.
- [ ] **[block]** Secret scan of the release diff is clean: `git log -p <last-release>..HEAD | grep -iE "signingkey|webhooksecret|password=|secret|BEGIN .*PRIVATE|AKIA|rzp_live_"` shows only known development values.
- [ ] **[block]** `Payments:Providers:FAKE:Enabled` is false or absent (startup also refuses it in Production). The Fake gateway runs only in Development and Testing (SEC-17).
- [ ] **[block]** A real `ISmsSender` (DLT-registered) is configured. Logs from a test OTP request contain no OTP code and no full phone number (SEC-1).
- [ ] SMTP host is configured, so the logging email sender is not in use (SEC-17).
- [ ] `Cors:AllowedOrigins` lists exactly the production website and admin origins over `https://`. No wildcard, no `localhost`.
- [ ] `FileStorage:RootPath` points at the persistent volume, and backups include it.
- [ ] Separate secrets for the JWT key and the OTP hash key once SEC-11 is done. Rotation procedure written in `docs/runbooks/`.

## 2. Reverse proxy (Caddy) and transport

- [ ] **[block]** HTTPS only; HTTP redirects to HTTPS; certificates are valid.
- [ ] **[block]** The API reads the client IP from `X-Forwarded-For` **only** from the Caddy network (`KnownProxies`/`KnownNetworks` set, `ForwardLimit = 1`). Test: a request with a spoofed `X-Forwarded-For` sent from outside does not change the rate-limit partition (SEC-2).
- [ ] Response headers on `https://api…`, the website and admin include `Strict-Transport-Security: max-age=31536000; includeSubDomains`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` (API) or `strict-origin-when-cross-origin` (web), `X-Frame-Options: DENY` or CSP `frame-ancestors 'none'`, and no `Server` version (SEC-16).
- [ ] Authenticated API responses (`/me*`, `/auth/*`, `/admin/*`) carry `Cache-Control: no-store`.
- [ ] Caddy `/media` serves only `*.webp` renditions; `…/original` returns 404 (SEC-20).
- [ ] Request body limits are set in Caddy and Kestrel: about 1 MB by default, larger only for image upload and bulk price import (SEC-18).
- [ ] Postgres, MinIO and Mailpit ports are not published on the public interface (`ss -tlnp` on the host shows them bound to localhost or the Docker network only).

## 3. Exposure

- [ ] **[block]** `GET /openapi/v1.json` and `GET /docs` (Scalar) return 404 in Production.
- [ ] **[block]** `POST /api/v1/dev/payments/fake/{id}/simulate` returns 404 in Production.
- [ ] `/health` returns only a status, with no connection strings or exception text.
- [ ] Error responses: send malformed JSON and an unknown route, and force a 500 in staging. Each answer is Problem Details with `code` and no stack trace, type names or SQL (SEC-15).

## 4. Authentication

- [ ] Access token lifetime is 15 min; refresh tokens are 30 days (customer) and 12 h (staff), or the absolute limits once SEC-12 is done.
- [ ] A token signed with `alg: none` or HS512, or with a wrong issuer or audience, gets 401 (integration test `Identity.Tests` or a manual curl).
- [ ] Refresh reuse: presenting a rotated refresh token returns 401 `REFRESH_TOKEN_REUSED` and revokes the family.
- [ ] OTP: the 6th wrong code returns 429 `OTP_TOO_MANY_ATTEMPTS`; the per-phone limit of 3 per 10 min holds; an unknown phone and a disabled phone get the same response.
- [ ] Staff login: unknown email, wrong password and locked account all return the same error (SEC-4). The lockout policy matches the decision log.
- [ ] At least two active ADMIN accounts exist, and the break-glass admin procedure is in `docs/runbooks/`.
- [ ] The staff password policy (at least 12 characters, common-password list) is enforced on create, reset and change.

## 5. Authorization

- [ ] **[block]** The architecture or integration test proving that every `/api/v1/admin/**` endpoint requires a permission passes. New endpoints since the last release have been reviewed by hand.
- [ ] **[block]** BOLA spot-check with two customers, A and B. B gets 404 on A's orders, payments, refunds, addresses, cart lines, complaints and invoice.
- [ ] Location spot-check: a STORE_MANAGER for location X gets 403 `LOCATION_ACCESS_DENIED` on location Y's prices, stock, shipments, refunds, settlements and reports, and sees no rows from Y in lists.
- [ ] A customer token on any `/admin` route gets 403. A staff token on customer-only routes gets 401 or 403.
- [ ] Global settings (tax rules, number series, business profile, roles) can be changed only by roles meant to have them (SEC-9, SEC-10).
- [ ] Request records for new or changed endpoints contain no owner ids, status, prices, totals or version fields that the server should set.

## 6. Business logic

- [ ] **[block]** Checkout and order totals are computed on the server only. A client-sent price or total changes nothing (`PRICE_CHANGED` when `expectedTotalPaise` differs).
- [ ] Every route in `docs/api/README.md` §1.8 enforces `Idempotency-Key` (400 without it). A replay with another user's key creates a new request and returns nothing of the first user's.
- [ ] Refunds: the total cannot exceed the amount paid under parallel requests; the requester cannot approve their own refund.
- [ ] Coupons and stock: parallel orders cannot overspend a coupon's limit or oversell stock (integration test with parallel requests).
- [ ] Webhooks: an invalid signature returns 401; a replayed event id returns `duplicate: true`; an amount mismatch opens an issue and does not capture the payment; the route has a body size limit (SEC-6).

## 7. Input handling

- [ ] Image upload refuses: a non-image renamed `.jpg`, more than 5 MB, a shortest side under 800 px, and a decompression bomb (for example a 20,000 × 20,000 PNG). The API stays up (SEC-5).
- [ ] CSV exports open in a spreadsheet with no formula evaluated for cells starting with `= + - @ \t \r` (SEC-14).
- [ ] No new string-built SQL: `grep -rn "FromSqlRaw\|ExecuteSqlRaw\|SqlQueryRaw\|CommandText" backend/src` shows only reviewed uses, with identifiers from allow-lists and values as parameters.
- [ ] Notification templates still render as plain text, with placeholders replaced once and no evaluation.

## 8. Rate limiting

- [ ] **[block]** The `otp-request`, `otp-verify`, `staff-login`, `refresh`, `checkout`, `search` and `public` policies exist with the §1.9 limits and keys (SEC-3).
- [ ] 429 responses carry `Retry-After` and code `RATE_LIMITED`.
- [ ] A load test from two client IPs through Caddy shows separate buckets, so one client's limit does not block the other (SEC-2).

## 9. Data protection (DPDP Act)

- [ ] **[block]** No OTP codes, refresh or access tokens, guest-cart tokens, passwords or full card data in the logs. Grep a day of staging logs for `rt_`, `eyJ`, 6-digit codes next to "SMS", and `+91`.
- [ ] Account deletion anonymises the profile, **all** addresses (active and removed), the user's phone and email, and revokes sessions (SEC-8).
- [ ] Retention jobs run: OTP challenges (24 h), processed outbox rows, webhook payloads (SEC-13).
- [ ] The audit trail records every admin write (staff and roles, refunds, prices and tax, customer block, settings), and `GET /admin/audit` shows the release-test actions (SEC-7).
- [ ] The privacy notice and consent text on the website and app match what is collected (phone, name, email, addresses, device tokens).
- [ ] No card or PAN data is stored. Webhook payloads from real gateways are checked for `card` objects and redacted before storage.

## 10. Dependencies and build

- [ ] **[block]** `dotnet list backend/FreshOrbit.slnx package --vulnerable --include-transitive` reports no High or Critical. Record the output.
- [ ] **[block]** `pnpm audit --prod` reports no High or Critical. Record the output.
- [ ] Container base images are current (`docker scout cves` or `trivy image`, both free) with no Critical CVEs in the runtime image.
- [ ] CI is green: `dotnet build` with 0 warnings, `dotnet test`, `pnpm lint/typecheck/test`.

## 11. Operations

- [ ] Database backups run daily, a restore was tested within the last 30 days, and backups are encrypted at rest.
- [ ] Logs are retained for a set time and access is limited to named people.
- [ ] Server: SSH keys only (no password login), the firewall allows only 80 and 443 (plus SSH from known IPs), and automatic security updates are on.
- [ ] Incident contacts and the DPDP breach-notification steps are in `docs/runbooks/`.
