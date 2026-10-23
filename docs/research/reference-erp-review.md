# Reference ERP and AgentOps review (2026-10-23)

The owner asked to look at two existing projects for reference. Ideas only; **no code is copied** (CLAUDE.md rule 14).
Decision: D27. AgentOps is not used in this project (CLAUDE.md rule 15).

## Reference ERP (a large .NET modular monolith)

| Pattern | Adopted? | How in Fresh Orbit |
|---|---|---|
| Separate Api / Worker / Migrations hosts from one codebase | Yes | `backend/src/Hosts` |
| Four projects per module: Domain, Application, Infrastructure, Public | Yes | Every module |
| One DbContext, each module adds its model | Yes | `FreshOrbitDbContext` + `IModuleModel` |
| Movements are the record (balances are folds over movements) | Yes | `inventory.stock_movements` → `stock_items` |
| Row version + If-Match; 412 stale, 409 race | Yes | `xmin` row version |
| Outbox + inbox + dead letters | Yes | `platform.*` tables, Worker dispatcher |
| Command shape Read → Decide → Commit → Follow-on | Yes | CLAUDE.md rule 5 |
| Missing configuration is refused, never guessed | Yes | No price = not for sale |
| UPPER_SNAKE stored codes, field-keyed errors | Yes | `EnumCodes`, Problem Details |
| "Capabilities, never countries" | Yes, as "no city names in code" | City rules are data |
| Multi-tenancy with row-level security | No | Single business |
| SQL Server, RabbitMQ, Redis, Azure, Kubernetes | No | PostgreSQL, one server, Docker Compose |
| One git repository per app | No | One monorepo |

## AgentOps
A tool for tracking AI-agent development work (work items, crews, reviews, product map). Not part of the product; not used here. Two ideas kept: generated shared API types for all clients, and a per-module rules catalogue (`docs/product/`).
