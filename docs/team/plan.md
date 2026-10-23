# Team plan: architecture and backend

Manager: Claude (main session). Employees: five agent sessions (E1–E5). Frontends, GitHub remote and deployment come after this plan.

## Phases

| Phase | Who | Work | Done when |
|---|---|---|---|
| 0 | Manager | Repo, CLAUDE.md, architecture, database docs, this plan | Committed on `main` |
| 1 | E1 | Backend foundation: solution, SharedKernel, hosts, all 15 module skeletons with `.Public` contracts, test infrastructure, dev compose | `dotnet build` and `dotnet test` green; architecture tests green; committed |
| 1 | E2 | API specification `docs/api/README.md` | Every endpoint, permission, request/response, error code listed |
| 2 | E1–E5 in parallel worktrees | Module implementation (table below) | Module builds, unit + integration tests green, endpoints match `docs/api` |
| 3 | Manager | Merge branches, initial EF migration, full build and test, run API against dev Postgres | All green on `main` |
| 4 | E1 + E2 | Sample data loader + end-to-end API tests (checkout, concurrency, payments, weight, refunds); security review | Green; findings fixed |

## Phase 2 ownership

| Employee | Modules | Notes |
|---|---|---|
| E1 | Identity, Customers, Settings, Audit, Notifications | OTP login, JWT + refresh rotation, staff + permissions, addresses, number series, audit, notification queue (dev SMS/email senders) |
| E2 | Locations, Catalog, Pricing | Serviceability, city activation, catalog browse/search/filter, admin catalog, image upload, location prices with periods |
| E3 | Inventory, Delivery | Stock, batches (FEFO), movements, reservations, waste, cold chain; slots from templates, capacity, shipments, riders, attempts |
| E4 | Cart, Promotions, Ordering | Cart pricing on read, coupons, checkout transaction, order state machine, weigh and pack, substitutions, cancellations, complaints, invoices |
| E5 | Payments, Reporting | Payment adapters (COD, Fake), webhooks idempotency, refunds, cash settlements, reconciliation job; reporting views + admin report endpoints; reference data files |

## Rules for employees

- Work only inside your modules' folders and their test projects. Shared code changes (SharedKernel, hosts, another module's `.Public`) are requested from the manager, not made.
- Do not create EF migrations; the manager creates one after merge. Integration tests build the schema with `EnsureCreated` through the test fixture.
- Commit on your branch with Conventional Commits. Report: what was built, tests run and their result, open issues.
