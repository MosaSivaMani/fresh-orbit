# SMS provider for DLT SMS, 2026-10-24 (E2E-1)

**Question.** Which Indian SMS provider does the first DLT adapter behind `ISmsSender` talk to? Production OTP sign-in
returned 500 because the `DLT` sender was a stub (E2E-1 in `e2e-deploy-check-2026-10.md`).

**Answer.** **Fast2SMS**, through its "DLT SMS (Manual)" route (`route=dlt_manual`). The code keeps the DLT rules
provider-neutral (`DltSmsSender`), so another provider is one more `IDltSmsGateway` class plus a
`Notifications:Sms:Dlt:Gateway` value. No payment or account is needed to build or test it: the tests use a fake
HTTP handler.

Legend: **[V]** verified on the vendor's own pages on 2026-10-24; **[U]** not verified (secondary source, memory,
or needs a live account).

## 1. What any provider has to do (TRAI DLT, the same for every provider)

- The business registers once as a **principal entity (PE)** on one operator's DLT portal. The PE id is used with
  every provider. [V, Fast2SMS docs call it the "DLT Principal Entity ID"]
- It registers a **header** (sender id) and one **content template** per message text. Variables in the template are
  written `{#var#}`. Each approved template gets a **content template id**. [V]
- Transactional and service messages must match a registered template, or the operator drops them ("scrubbing").
  The provider does not repair a mismatch. [V, Fast2SMS: "Passing incorrect or invalid details, or using a wrong
  message text" makes the SMS fail and is not refunded]
- The provider (telemarketer) must be linked to the PE on the DLT portal (the "telemarketer chain" binding). [U on
  the exact menu name; it varies by operator portal]
- The ids we have seen are 19 digits. The code only requires digits (10 to 25), so it does not reject an unusual
  but valid id. [U on the exact length rule]

## 2. Options compared

| | Fast2SMS | MSG91 | 2Factor.in |
|---|---|---|---|
| DLT with our own template ids | Yes: `dlt_manual` takes `template_id` + `entity_id` + the full text, passed to the operator as is [V] | Through a "Flow": the DLT template id is mapped to an MSG91 flow id on their panel, and the API sends the flow id with named variables [V on the mapping; the API shape is U] | Templates are registered on their panel and referred to by name [U] |
| Variables | Full text (no slot list) [V]; the `dlt` route takes `variables_values` separated by `\|` [U] | Named variables (`##OTP##` style) that must match the flow [U, third-party page] | `var1`, `var2`, ... [U] |
| Test access | ₹50 free credit when you register [V on the vendor's help page] | Free trial credit [U] | Free trial credit [U] |
| Pricing | Prepaid top-up from ₹100. ₹0.25 per SMS at ₹100 to ₹3,999 and down to ₹0.11 at the top band. The OTP API is ₹0.35 [V on the pricing page] | Per SMS, prepaid [U on the rates] | Per SMS, prepaid [U] |
| API key | HTTP header `authorization` [V] | HTTP header `authkey` [U] | Part of the URL path in the classic OTP API [U]. That would put the secret into access logs |
| Endpoint | `POST https://www.fast2sms.com/dev/bulkV2`, JSON body [V] | `POST https://control.msg91.com/api/v5/flow` [U] | `https://2factor.in/API/...` [U] |

**Why Fast2SMS.**
1. With `dlt_manual`, our configuration is the only source of the DLT ids. There is no second mapping on the
   provider's panel to keep in sync, and the request carries exactly the DLT fields our neutral layer already has.
2. The API key is sent in a header, never in the URL (SEC-1).
3. ₹50 of free credit is enough for a live check of the OTP template before going live.
4. Prepaid with no monthly fee, and cheap at our volume.

**Risks.**
- The provider does not check the text: a template mismatch fails silently at the operator. `docs/runbooks/security-configuration.md`
  §2 therefore makes the registered text exactly our template text, and staff must not edit an SMS body without
  registering the new text first.
- The error codes are not fully documented. Only `return`, `request_id`, `status_code` and `message` are known
  (§3). The adapter maps by HTTP status, `return` and `status_code` (§3); confirm the mapping against a live account
  before launch (open item).

## 3. Fast2SMS `dlt_manual` request and response (as implemented)

From `docs.fast2sms.com/reference/dlt-manual-single.md` [V], with anything not on that page marked:

```
POST https://www.fast2sms.com/dev/bulkV2
authorization: <API key>
Content-Type: application/json

{"route":"dlt_manual","sender_id":"<header>","message":"<full DLT-approved text, variables filled>",
 "template_id":"<DLT content template id>","entity_id":"<DLT PE id>","numbers":"9876543210"}
```

- `numbers` is the 10-digit number without `+91`. Several numbers can be sent, separated by commas; we send one.
  [V]
- `template_id` and `entity_id` may be omitted when the template was added in the provider's "DLT Manager". We
  always send them. [V]
- Success: HTTP 200 `{"return": true, "request_id": "…", "message": […]}`. [V on the schema; only HTTP 200 is
  documented]
- Failure: `{"return": false, "status_code": <n>, "message": "…"}`. [U] `status_code` 412 = invalid API key. [U]

The adapter maps the answers as follows (`Fast2SmsGateway`):

| Answer | Failure kind | Retried by the Worker |
|---|---|---|
| HTTP 401 or 403, or `status_code` 412 | `UNAUTHORIZED` | yes (up to 5 attempts) |
| HTTP 429 or 5xx, network error, timeout (`Notifications:Sms:Dlt:TimeoutSeconds`) | `UNREACHABLE` | yes |
| Any other `return: false`, an unreadable body, any other status | `REJECTED` | no (FAILED) |
| HTTP 2xx with `return: true` | sent; `request_id` is stored as the provider message id | |

## 4. Sources

- Fast2SMS API docs index: <https://docs.fast2sms.com/llms.txt>; DLT Manual: <https://docs.fast2sms.com/reference/dlt-manual-api.md>,
  <https://docs.fast2sms.com/reference/dlt-manual-single.md>; authorization: <https://docs.fast2sms.com/>.
- Fast2SMS pricing: <https://fast2sms.com/pricing>, <https://www.fast2sms.com/help/?p=16670>; free credit:
  <https://www.fast2sms.com/help/?p=16494>.
- MSG91 DLT template to flow mapping: <https://msg91.com/help/MSG91/map-approved-dlt-template-id-with-respective-flow-id-on-msg91-panel>.
