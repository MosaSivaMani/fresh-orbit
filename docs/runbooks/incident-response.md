# Runbook: security incident and personal-data breach (DPDP)

What to do when customer data may have been seen, changed, lost or taken by someone who should not have it (gap
audit L-27, F-19). Examples: a leaked `.env.prod` or database dump, a stolen staff laptop with a signed-in admin, a
staff account used by someone else, a wrong customer's details shown in an order, an exposed backup bucket key, or a
lost phone with a rider's session.

**Not legal advice.** The duties below are our reading of the DPDP Act 2023 s.8(6) and Rule 7 of the DPDP Rules 2025
(`docs/research/legal-policies-2026-10.md` §2) and of the CERT-In directions of 28 April 2022. Rows marked
**Check with lawyer** must be confirmed before launch (L-9). Most DPDP duties apply in full from about 13 May 2027;
we follow them from launch anyway.

## 1. Roles

Fill the names before launch and keep this table current. One person can hold several roles in a small team, but the
**decision maker** must always be reachable or have a named deputy.

| Role | Who | Does |
|---|---|---|
| Decision maker | {{OWNER: the owner}} (deputy {{OWNER: name}}) | Declares the incident, decides whether it is a personal-data breach, approves every notice, talks to the lawyer |
| Technical lead | {{OWNER: the engineer on call}} | Contains, collects evidence, finds what and who was affected, fixes the cause |
| Grievance officer | `FRESH_GRIEVANCE_OFFICER_NAME` in `.env.prod` | Sends the notices to customers and answers their questions (the business contact named in the notice) |
| Lawyer | {{OWNER: law firm and phone}} | Confirms the Board report and its wording; CERT-In report if needed |
| Scribe | Anyone not doing the above | Keeps the incident log (§6) from the first minute |

Contacts to keep in the team password manager next to the server secrets: the Data Protection Board's breach
reporting channel (**Check with lawyer**: the Board's portal or address once it publishes one), CERT-In
`incident@cert-in.org.in`, the hosting provider's support, Cloudflare, the SMS provider, the SMTP provider, the
backup storage provider.

## 2. Clocks

The clocks start when **we become aware** of the breach, not when it happened. Write that time (UTC and IST) as the
first line of the incident log.

| Within | What | Who |
|---|---|---|
| At once | Contain (§3 step 2) | Technical lead |
| **6 hours** | CERT-In report, for the incident types in its 2022 directions (for example unauthorised access to systems or data, data breach, data leak). **Check with lawyer** whether each incident needs it | Decision maker, lawyer |
| **Without delay** | First notice to the Data Protection Board: what happened, its nature, extent, timing and location, and its likely impact | Decision maker |
| **Without delay** | Notice to **every affected person** (§5): what happened, likely consequences, what we did, what they can do, and whom to contact | Grievance officer |
| **72 hours** | Detailed report to the Board: updated facts, the circumstances and reasons, measures taken or proposed, findings on who caused it, steps to stop a repeat, and a report on the notices sent to people. The Board may allow longer on a written request; ask before the 72 hours end, never after | Decision maker, lawyer |

A breach that touches **no personal data** (for example the website was down) needs no Board or customer notice;
write it in the incident log anyway.

## 3. Steps

1. **Declare.** Whoever notices it tells the decision maker and the technical lead by phone (not only chat). The
   decision maker opens the incident log (§6).
2. **Contain** (technical lead). Choose what fits; each line points to the existing procedure.
   - Stop new orders while you look: pause the city in the admin (Cities & Locations › the city › Pause; placed
     orders continue, LOC-R8), or stop the public services
     (`docker compose --env-file .env.prod -f docker-compose.prod.yml stop api worker web` in `infra/compose`, with
     `IMAGE_TAG=$(cat ../../.deploy/current-tag)` as in `deploy.md` §3).
   - A staff account misused: deactivate it in the admin (Settings › Staff & roles › edit the member › clear
     Active). If its sessions may still be live, also rotate `JWT_SIGNING_KEY` (next line). If no admin can sign
     in, `security-configuration.md` §5.
   - A leaked secret: rotate it (`security-configuration.md` §7). Rotating `JWT_SIGNING_KEY` signs **everyone**
     out at once, customers and staff, which is the quickest way to end stolen sessions.
   - A leaked deploy SSH key or server access: remove the key from `/home/deploy/.ssh/authorized_keys`, rotate the
     GitHub `DEPLOY_SSH_KEY`, and check `last` and `journalctl -u ssh` for logins.
   - A leaked backup key: revoke the bucket token at the provider, then make a new one (`backup-restore.md` §2).
     The backups themselves are encrypted with the rclone crypt passwords, which never leave the server and the
     password manager; if those leaked too, treat every offsite backup as read.
3. **Keep evidence before changing more** (technical lead). Copy, do not edit:
   - `infra/scripts/logs.sh --since 72h -n 100000 --no-follow > /var/backups/freshorbit/incident-<date>-logs.txt`
     (Docker keeps about 50 MB per container; Loki keeps 14 days when the monitoring profile is on).
   - A database backup labelled for the incident: `infra/scripts/backup.sh --label incident-<date>`.
   - The audit trail of staff actions in the window (append-only, `audit.entries`):
     ```sql
     SELECT occurred_at, actor_type, actor_name, action, entity_type, entity_id, ip
     FROM audit.entries
     WHERE occurred_at BETWEEN '<from>' AND '<to>'
     ORDER BY occurred_at;
     ```
   Open `psql` as in `security-configuration.md` §5 step 3. Keep the files on the server (`chmod 600`) and a copy in
   the password manager's secure notes or another encrypted place; they contain personal data.
4. **Decide** (decision maker, within hours): is personal data affected? Which kinds (§4)? How many people? If unsure,
   treat it as a breach: a notice that turns out unnecessary costs less than a late one.
5. **Notify** the Board and the affected people (§2 clocks, §5 templates). Send the customer notice by the channels
   we hold: SMS (only the registered DLT templates can be sent, so use e-mail where we have it, and a phone call or a
   notice on the website and app Help otherwise; **Check with lawyer** that a public notice is enough for people
   without e-mail), e-mail through the SMTP provider.
6. **Fix and recover.** Fix the cause, release through the normal pipeline (`deploy.md` §2), restore from backup
   only if data was changed or lost (`backup-restore.md` §4), un-pause the city.
7. **Close.** Send the Board the 72-hour detailed report; write the post-incident note (§6); add a decision to
   `docs/decisions/log.md` for every lasting change, and a row to `docs/team/progress.md`.

## 4. Find the affected people

Work on the server in `psql` (read-only queries). Export the result once, to the protected incident folder, never
to chat or e-mail.

Customers with an order in a time window (for example, orders a leaked rider session could see):

```sql
SELECT DISTINCT c.id AS customer_id, c.full_name, u.phone, c.email
FROM ordering.orders o
JOIN customers.customers c ON c.id = o.customer_id
JOIN identity.users u ON u.id = c.user_id
WHERE o.placed_at BETWEEN '<from>' AND '<to>'
  AND (<extra filter, e.g. o.fulfillment_location_id = '<id>'>);
```

Customers whose records a staff member opened or changed (from the audit trail):

```sql
SELECT DISTINCT c.id, c.full_name, u.phone, c.email
FROM audit.entries a
JOIN customers.customers c ON c.id::text = a.entity_id::text
JOIN identity.users u ON u.id = c.user_id
WHERE a.actor_id = '<staff user id>' AND a.entity_type = '<entity type>'
  AND a.occurred_at BETWEEN '<from>' AND '<to>';
```

Everyone (a full database or backup leak): every row of `customers.customers` that is not `DELETED`, plus the
delivery name and phone copied onto `ordering.orders` for deleted accounts (kept 72 months for GST). Count them
first: `SELECT count(*) FROM customers.customers WHERE status <> 'DELETED';`.

Check the column names against `docs/database/README.md` before running; adjust the joins to the entity the audit
rows name. What each kind of record holds (for the notice's "what data"): `play-store.md` §1 and
`docs/legal/privacy-policy.md` §2.

## 5. Notice templates

Plain language, no blame, no guessing. Fill every `<…>`; the decision maker approves the final text.

**To the Data Protection Board (first notice, without delay).**

> Fresh Orbitt (<legal name>, <registered address>, GSTIN <gstin>) reports a personal data breach.
> Became aware: <date and time IST>. Happened: <date and time, or "not yet known">.
> Nature: <what happened, e.g. unauthorised access to the order database through a staff account>.
> Extent: about <n> customers; data involved: <names, phone numbers, delivery addresses, order history, …>.
> Location: <our server at <hosting provider, data centre>, or the device or service involved>.
> Likely impact: <e.g. unwanted calls or messages; no payment card or bank data is held by us>.
> Contact: <grievance officer name, e-mail, phone>. A detailed report will follow within 72 hours.

**To the Data Protection Board (detailed report, within 72 hours).** The first notice, updated, plus: the
circumstances and cause; what we did to contain it and when; what we are changing so it does not repeat; what we know
about who caused it; and when and how we told the affected people (attach the notice text and the count sent).

**To each affected person (e-mail; the same facts on the website notice).**

> Subject: Important: your Fresh Orbitt account information
>
> On <date> we found that <plain description: e.g. someone used a staff login to view orders>. Your <name, phone
> number, delivery address, order details> may have been seen. Your <payment card or bank details were not involved:
> we do not hold them>.
>
> What this may mean for you: <e.g. calls or messages pretending to be us>. We will never ask for an OTP, a UPI PIN or
> a password by phone or message.
>
> What we have done: <stopped the access, signed everyone out, changed our keys, …>.
> What you can do: <ignore calls asking for OTPs; tell us about any order you did not place; …>.
>
> Questions: <grievance officer name>, <e-mail>, <phone>, <hours>. You can also complain to the Data Protection Board
> of India.

**To CERT-In (if needed, within 6 hours):** use CERT-In's incident reporting form (cert-in.org.in) or
`incident@cert-in.org.in` with the same facts as the Board notice and the technical details (affected systems, IPs,
time line). **Check with lawyer.**

## 6. Incident log and after-action note

Keep one log per incident in the team's shared drive (not in this repository, as it holds personal data):

| Time (IST) | Who | What was seen or done | Evidence (file, query, screenshot) |
|---|---|---|---|

After closing, write a short note **without personal data** in `docs/research/incident-<yyyy-mm-dd>.md`: what
happened, time line, cause, what we changed, and whether this runbook worked. Update this runbook in the same change.

## 7. Practise

Once before launch and then once a year, walk through §3 on a made-up case (for example "a rider's phone with a
signed-in session was stolen") with the people in §1, without touching production. Note the date and what was
unclear at the end of this page.

### Drill log

| Date | Case | People | What we changed |
|---|---|---|---|
