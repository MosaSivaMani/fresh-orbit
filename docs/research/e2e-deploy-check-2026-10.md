# End-to-end deploy check, 2026-10-24

This is the result of `next-steps.md` §3.2: does a deploy run the migrations and the reference loader from end to
end? The answer is yes. It was proven on the Mac's Docker with the production compose file, against `main` at
`9501cec` (after `integration/sec-sample`). How to repeat the check: `docs/runbooks/e2e-check.md`.

## 1. Result

`infra/scripts/e2e-check.sh` exits 0. Both passes start from a fresh database.

| Check | Evidence |
|---|---|
| Images build | `fo-e2e/fresh-orbit-{migrations,api,worker,web,admin}:local` (linux/amd64 on the Mac) |
| Migrations on an empty database | Run 1 exits 0. `platform.__ef_migrations_history` = `20261023185953_InitialCreate (EF 10.0.13)`. There are 16 schemas (audit … settings) and 77 tables and views |
| Reference loader | Identity, Settings, Notifications, PreparationMethods, TaxCategories and StorageUnits all ran. Counts: permissions 25, roles 6, role_permissions 60, preparation_methods 16, storage_units 3, tax_categories 7, notification templates 54, number_series 4, order_policies 1. First admin: staff 1 |
| Idempotent | Run 2 exits 0. Every count is identical, and the loaders log `0 inserted … unchanged/skipped`. A second `--create-admin` is refused with exit 3. A third run, by `compose up`, exits 0 |
| Health | postgres, api, web, admin and caddy are `healthy`. The worker is `running` with 0 restarts after a 20 s soak |
| Through Caddy (TLS, local CA) | 14 checks pass: api, web, admin, `www` and HTTP redirects, CSP/HSTS/noindex headers, SPA fallback, `/media` privacy, and the web and admin same-origin API routes |
| Production guards | `/api/v1/dev/sms` returns 404. An OTP request returns 500 (the DLT stub refuses to send; since E2E-1 the real adapter answers 503 `SMS_PROVIDER_UNAVAILABLE` against the unreachable gateway). An api started with `Sms:Provider=DEVELOPMENT` refuses to start (`OptionsValidationException`) |
| Smoke, production pass | `smoke.sh` with `SMOKE_STOP_BEFORE_OTP=true`: **28 passed, 0 failed** |
| Smoke, testing pass | Full `smoke.sh`: **52 passed, 0 failed** |
| Teardown | `down -v` leaves no `fo-e2e` containers or volumes. The dev `freshorbit` stack was not touched |

## 2. Fixed (in `infra/`)

1. **`smoke.sh` built a wrong media URL behind Caddy.** The API returns absolute image URLs when
   `FileStorage__PublicBaseUrl` is set (`https://api.<domain>/media/...`). The script prefixed the API origin again.
   It now uses an absolute URL as it is.
2. **`smoke.sh` raced the Worker before dispatch.** The shipment exists after `OrderConfirmed` but is only marked
   packed when the Worker handles `OrderPacked`. "Shipment ready" now waits for `packedAt`. Without that wait, the
   testing pass failed once with `409 SHIPMENT_NOT_DISPATCHABLE`.
3. **`smoke.sh` could not run against Production mode** after SEC-1, because OTPs come only from `/dev/sms`. The public
   checks (serviceability, product list, media) now run before the sign-in, and `SMOKE_STOP_BEFORE_OTP=true` ends
   the run there.
4. **`deploy.sh` image pruning could delete other people's images.** It removed every `*/fresh-orbit-*` image not
   tagged current or previous. On this shared Docker that would have deleted `local/fresh-orbit-*` builds. It now
   prunes only `${IMAGE_REGISTRY}/fresh-orbit-*`, and `SKIP_IMAGE_PRUNE=true` leaves every image alone.
5. **`lib.sh` hard-coded the compose project.** `FO_PROJECT` and `FO_COMPOSE_OVERRIDE` can now be overridden (the
   production default is unchanged), so `deploy.sh` can drive a non-production project.

Nothing in the Migrations host needed a change.

## 3. Observations and open items (outside `infra/`, not changed here)

| # | Where | Finding |
|---|---|---|
| E2E-1 | Notifications (DLT sender) | **Done on `backend/deploy-gaps-sms`:** DLT sender + Fast2SMS adapter (`research/sms-provider-2026-10.md`); a failed send is 503 `SMS_PROVIDER_UNAVAILABLE`. Customers can sign in once the owner finishes the DLT registration (`runbooks/security-configuration.md` §2.1) |
| E2E-2 | Api host | When a start-up validator refuses the configuration, the container exits with **139**, after an `Unhandled exception` (`OptionsValidationException`). Catching it in `Program.cs`, logging it and returning exit code 1 would make crash loops easier to read. The refusal itself is correct |
| E2E-3 | Api host (Hosting) | Warning at start-up: Data Protection keys are stored in `/home/app/.aspnet/DataProtection-Keys` inside the container, unencrypted, and are lost on every deploy. If anything uses Data Protection (antiforgery, cookies), persist the keys (database or volume). If nothing uses it, leave it, but confirm |
| E2E-4 | Identity | EF warning on `POST /admin/auth/login`: one query loads several collection navigations with no `QuerySplittingBehavior`. Consider `AsSplitQuery()` |
| E2E-5 | Migrations / EF | On an empty database EF logs one **Error**-level `Failed executing DbCommand`, because it reads `platform.__ef_migrations_history` before creating it. This is harmless, and the run exits 0. Grafana's "Backend errors" alert watches only api and worker, so it does not fire. Leave it, or filter that one event in the Migrations host's logging |
| E2E-6 | web | `next build` logs `Footer: cities unavailable` while pre-rendering, because there is no API at build time. At runtime the footer cities are cached for 1 h (`publicHttp(3600)`), so a city activated just now is not listed straight away. This is by design, but worth knowing for demos |
| E2E-7 | infra / tooling | `deploy.sh` itself was not run on the Mac: it needs `flock` and bash 4 or later, and macOS has neither. A Linux helper container would need an image the project does not use (owner rule: no new images). Run `deploy.sh` against `fo-e2e` on a Linux machine or a staging server (`e2e-check.md` §5). `shellcheck` is not installed on the Mac either. One early run used the `koalaman/shellcheck` image (clean apart from two pre-existing SC2015 notes in `smoke.sh`); the image was removed after the owner rule arrived |
