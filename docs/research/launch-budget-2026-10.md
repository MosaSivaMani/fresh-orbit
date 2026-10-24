# Launch budget (very low cost plan), October 2026

The owner asked what each step costs and wants the lowest cost. Prices are approximate, include GST unless noted, and must be checked on the vendor's own page before paying. [V] = from the vendor or several sources, [U] = third-party only.

## 1. Building (now)

| Item | Cost | Note |
|---|---|---|
| Code, design, tests, docs | ₹0 | Free, open-source stack (D-rules 13) |
| Fonts (Baloo 2, Dancing Script, Inter) | ₹0 | OFL |
| Character images | ₹0 extra | Owner's existing ChatGPT |
| GitHub private repo | ₹0 | Free plan |

## 2. Soft launch: website + Android, Visakhapatnam

| Item | One-time | Monthly | Note |
|---|---|---|---|
| Server: Oracle Cloud Always Free (ARM, India) | ₹0 | ₹0 | D25; `runbooks/oracle-setup.md`; set a ₹100 budget alert |
| Domain `.in` | ~₹500–900 first year | — | Renewal ~₹800–1,000/yr [U] |
| SSL certificate | ₹0 | ₹0 | Caddy + Let's Encrypt |
| DLT registration (needed for any OTP SMS in India) | ~₹5,900 + GST once [U] | — | One operator portal; others free afterwards [U] |
| OTP SMS (Fast2SMS) | ₹100 minimum top-up | ~₹0.25 per SMS [V] | 2,000 logins/order SMS ≈ ₹500/month |
| Email (transactional) | ₹0 | ₹0 | Free tier of an SMTP provider |
| Push notifications (Firebase Cloud Messaging) | ₹0 | ₹0 | |
| Google Play developer account | ~₹2,100 once ($25) | — | |
| Backups | ₹0 | ₹0 | Free object-storage tier |
| FSSAI | ~₹100/yr basic registration, or ~₹2,000–5,000/yr state licence | — | Depends on turnover; confirm type with FSSAI |
| GST registration | ₹0 | — | |
| Payments | ₹0 | ₹0 | Cash / UPI on delivery at launch |

**Soft launch total ≈ ₹9,000–10,000 one-time, then ≈ ₹300–600 per month (mostly SMS).**

## 3. Later, only when needed

| Item | Cost | When |
|---|---|---|
| iPhone app: Apple Developer Program | ~₹8,700 per year [U] | When launching on iOS |
| Production server: Hostinger VPS KVM 2 | ~₹600–800/month on a long plan, renews ~₹1,000–1,200/month [U] | When Oracle Free is not enough (`runbooks/move-to-hostinger.md`) |
| Online payment gateway | ~2% per online payment, no setup fee typical | When prepaid UPI/cards are switched on |
| Lawyer review of `docs/legal/` | one-time, quote needed | Before public launch |
| Trademark "Fresh Orbit" | optional, ~₹4,500 per class (individual/startup) [U] | Any time |
| Product photography | owner's phone = ₹0 | Before launch |

## 4. Ways we keep it low

- Oracle Always Free until traffic needs more; the same Docker Compose moves to Hostinger in one evening.
- Android and website first; iOS only when there is demand (saves ₹8,700/yr).
- COD/UPI on delivery first; gateway fees only start with online payments.
- OTP SMS only for login; order updates by push and email where possible to save SMS.

Sources checked 2026-10-24: Fast2SMS pricing (see `research/sms-provider-2026-10.md`); Hostinger India VPS price articles (zoutons.com, bloggerspassion.com); DLT fee guides (developer.exotel.com, lucep.com); Apple developer forums for the India fee.
