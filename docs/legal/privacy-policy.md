> **DRAFT for owner and lawyer review. Not published. Not legal advice.**
> Written from what the system collects and keeps today: `docs/database/README.md`, `docs/product/identity.md`
> (IDN-R1–R10), `customers.md` (CUS-R7, CUS-R9 and the retention table), `notifications.md` (NOT-R2, R6, R7, R11,
> R12), `payments.md` (PAY-R13, R24, Q10), `ordering.md` (ORD-R16, R56), `audit.md`, `docs/design/web.md` §4
> (cookies), `docs/security/review-2026-10.md` (SEC-8, SEC-13), `docs/runbooks/backup-restore.md`, decisions D25, D28,
> D34, D37. Aligned with the Digital Personal Data Protection Act 2023 ("DPDP Act") and the DPDP Rules 2025 as we
> understand them; see `docs/research/legal-policies-2026-10.md` for which points are certain.
> Replaces the earlier draft `docs/content/privacy-policy.md`.
> Fill every `{{OWNER: …}}`. Points marked **Check with lawyer:** must be cleared before publishing.

# Privacy policy

Last updated: {{OWNER: date of publication}}

This policy explains what personal data {{OWNER: legal name of the business}} ("Fresh Orbit", "we", "us") collects
when you use our website {{OWNER: website address}} and our mobile app, why we collect it, who we share it with, how
long we keep it, and what rights you have. Under the DPDP Act we are the **Data Fiduciary** for this data, and you are
the **Data Principal**.

## 1. Who we are and how to reach us

- {{OWNER: legal name of the business}}, {{OWNER: registered office address}}
- For anything about your personal data, write to our grievance officer:
  {{OWNER: grievance officer name}}, {{OWNER: grievance officer designation}}, {{OWNER: grievance officer email}},
  {{OWNER: grievance officer phone}}. See also the [Grievance redressal policy](grievance-redressal.md).

**Check with lawyer:** the DPDP Act s.8(9) asks every Data Fiduciary to publish the contact of a person who can
answer questions about personal data (a Data Protection Officer only if we are notified as a *Significant* Data
Fiduciary, which is unlikely for a business our size). Confirm that the grievance officer can be that person.

## 2. What we collect and why

We collect only what we need to sell and deliver your order, keep your account safe, and keep the records the law
requires.

| What | When we collect it | Why |
|---|---|---|
| **Mobile number** | When you sign in (we send a one-time password by SMS) | To create your account, sign you in securely, and send order updates |
| **Name and email** (email is optional) | When you fill in your profile | To address you, and to email receipts and order updates if you give an email |
| **Delivery addresses**: recipient name, phone, address, landmark, pincode, and an optional map pin | When you add an address | To check that we deliver there and to deliver your order |
| **Orders**: items, weights, prices, delivery slot, your notes, substitution choice, coupon used | When you order | To prepare and deliver your order and issue a GST invoice |
| **Payments**: payment method, amount, and the UPI reference the rider records | When you pay on delivery | To record your payment |
| **Refund details**: the UPI ID you give us for a refund | When we refund a pay-on-delivery order | To pay your refund. We do not store bank account details in our system |
| **Complaints**: what went wrong, your description and up to 4 photos | When you report a problem | To decide your complaint and refund you |
| **Wishlist and cart** | When you use them | To remember your choices |
| **Notification settings and messages** we send you | When you use the service | To send order updates, and offers only if you opt in |
| **App notification token** (Android, iOS or web) | When you allow app notifications | To send notifications to your device |
| **Sign-in records**: phone number, IP address and time of each one-time-password request; sign-in sessions | When you sign in | To keep accounts secure and stop abuse (for example, limits on how often a code can be requested) |

We do **not** collect card numbers or bank passwords. When online payment is added, it will be handled by a licensed
payment provider and we will update this policy before it starts.

### Cookies on our website

The website uses only cookies it needs to work: one remembers your pincode, one keeps your guest cart, one keeps you
signed in (it cannot be read by page scripts), and one tells the site that you have a session. We do not use
advertising or analytics cookies today. {{OWNER: if analytics or advertising tools are added later, list them here
and ask for consent first.}}

## 3. Our legal basis

- **Consent.** When you sign in for the first time we show you a short notice that lists the data above and its
  purposes, and ask you to agree. You can withdraw consent at any time (§6); withdrawing does not affect what we did
  before, but we cannot take new orders from an account without the data needed to deliver them.
- **Offers and marketing** are sent only if you switch them on, and you can switch them off at any time in your
  notification settings. Order updates by SMS cannot be switched off while you have an account, because they are part
  of the service (for example, the delivery code).
- **Legal duties.** We keep invoices and order records because tax law requires it (§5).

**Check with lawyer:**
- Which purposes rest on consent and which on a "legitimate use" under DPDP Act s.7 (for example s.7(a): data you give
  us voluntarily for a purpose you know about).
- The consent notice itself (DPDP Act s.5 and Rule 3 of the DPDP Rules 2025): its wording, that it is shown on its own
  and kept separate from these terms, and how we keep a record of consent. **The app and website do not show such a
  notice today**; this is a build item before launch.

## 4. Who we share your data with

We share only what each party needs, and we do not sell personal data.

| Who | What | Why |
|---|---|---|
| Our delivery riders and store staff | Recipient name, phone, address, map pin, order contents, amount due | To prepare and deliver your order. Staff see only orders of their own store, and their actions are logged |
| Our SMS provider ({{OWNER: SMS provider name, e.g. the provider chosen in `docs/research/sms-provider-2026-10.md`}}) and the telecom operators it uses | Mobile number, message text | To send one-time passwords and order updates |
| Our email provider ({{OWNER: email provider name}}) | Email address, message text | To send receipts and order updates |
| {{OWNER: app notification service, e.g. Google Firebase Cloud Messaging, once enabled}} | Notification token, message text | To send app notifications |
| Our hosting provider ({{OWNER: hosting provider and data centre location}}) | All data, stored on our servers | To run the service |
| Our backup storage ({{OWNER: backup storage provider and location}}) | Copies of the database and files, encrypted before they leave our server | To recover from failures |
| Payment provider (only once online payment starts) | Order amount, phone, payment reference | To take payments and pay refunds |
| Tax authorities, courts, police and regulators | What the law requires | To meet legal obligations |

**Check with lawyer:** whether any provider stores data outside India, and whether the Government has restricted
transfer to that country (DPDP Act s.16). Name the countries here if so.

## 5. How long we keep your data

| Data | How long |
|---|---|
| Profile (name, email, offer settings) and sign-in phone number | Until you delete your account. Then they are cleared |
| Saved addresses | Until you remove the address or delete your account. A removed address is blanked straight away; only its label, city, state and pincode stay |
| Wishlist and cart | Wishlist until you delete your account. A cart is kept 30 days after you last used it |
| **Orders and invoices**, including the delivery name, phone and address copied onto each order | **72 months**, because GST law (CGST Act s.36) requires us to keep accounts and records for that long. They are kept even if you delete your account, and are then deleted |
| Payment and refund records (including a UPI ID you gave for a refund) | Same 72 months, as part of our accounts |
| Complaint descriptions and photos | {{OWNER: retention for complaints and photos, e.g. 72 months with the order, or less}} |
| Messages we sent you | {{OWNER: retention for sent messages}} |
| One-time-password requests (phone, IP address, time) | {{OWNER: confirm 24 hours as planned (security item SEC-13)}} |
| Security logs | {{OWNER: confirm one year}} |
| Backups | Overwritten after {{OWNER: confirm 7 days on the server and 30 days off-site, the current backup settings}} |

**Check with lawyer:**
- The exact start of the 72-month GST period (it runs from the due date of the annual return for the year, not from
  the order date); our CA is to confirm.
- DPDP Rules 2025: we understand that logs of processing must be kept for at least one year for security. Confirm,
  and whether that one year overrides a shorter period above.
- **Today the system keeps one-time-password requests, sent messages and complaint photos with no time limit, and
  does not yet delete orders after 72 months.** These deletions must be built before this table is published
  (`docs/security/review-2026-10.md` SEC-13, `docs/team/next-steps.md`).

## 6. Your rights

Under the DPDP Act you can:

- **Access**: ask for a summary of the personal data we hold about you and how we use it, and who we shared it with
  (s.11).
- **Correct and update**: change your name, email and addresses in the app at any time, or ask us to correct anything
  else (s.12).
- **Erase**: delete your account yourself ("Delete my account" on your account page), or ask us to erase data we no longer need
  (s.12). See below for what is kept.
- **Withdraw consent**: at any time, as easily as you gave it (s.6(4)). For offers, use notification settings; for
  everything else, delete your account or write to us.
- **Nominate** someone to act for you if you die or cannot act (s.14).
- **Complain** to our grievance officer (s.13). If you are not satisfied with the answer, you can complain to the
  **Data Protection Board of India**. The law asks you to come to us first.

We answer requests within {{OWNER: number}} days, and never later than the time the DPDP Rules allow.

### Deleting your account

You cannot delete your account while an order is still in progress. When you delete it, we clear your name, email
and phone number, blank all your saved addresses, remove your wishlist, turn off offers and sign you out on every
device. Your past orders and invoices stay for the period in §5, because the law requires it, but they are no longer
linked to a phone number you can sign in with.

**Check with lawyer:**
- The DPDP Rules response time for rights requests and grievances (we understand it to be at most 90 days) and
  whether we should promise less.
- How we verify the identity of someone asking for access or erasure by email rather than in the app.
- Access and nomination requests are handled by hand today; there is no screen for them.
- An account can be deleted while a complaint or a pay-on-delivery refund is still open; we then lose the contact
  details needed to pay that refund. Decide whether to refuse deletion in that case.

## 7. Your duties

The DPDP Act (s.15) asks you not to impersonate anyone, not to give false details, and not to file false or
frivolous complaints.

## 8. How we protect your data

- All connections to the website, app and admin use encryption (HTTPS).
- One-time passwords and staff passwords are stored only as secure hashes.
- Staff accounts need a strong password, are locked after repeated wrong attempts, and give each person access only
  to their own store and duties.
- Every change made by staff is recorded in an audit log that cannot be edited.
- UPI IDs given for refunds are partly hidden in staff lists.

If a breach of personal data happens, we will tell the Data Protection Board of India and every affected person, as
the DPDP Act and Rules require.

**Check with lawyer:** the breach notice duties and timelines (DPDP Rules 2025, Rule 7: notice to the Board without
delay and a fuller report within 72 hours, as we understand it), and who in the business does this.

## 9. Children

Our service is for adults. You must be 18 or older to create an account. We do not knowingly collect data about
children.

**Check with lawyer:** the DPDP Act s.9 requires verifiable parental consent for anyone under 18. We do not check age
today; confirm whether a statement plus a check box at sign-in is enough.

## 10. Changes to this policy

When we change this policy we will update the date above and tell you in the app before the change applies. If a
change needs fresh consent, we will ask for it.
