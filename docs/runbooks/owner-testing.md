# Owner testing: try Fresh Orbit without the work laptop

There are two ways to try the product. Neither needs the company laptop.

| What | What you see | Needs |
|---|---|---|
| **A. Demo app on your Android phone** | The customer app with made-up sample data. Nothing is saved; no server. | An Android phone |
| **B. The whole store in a GitHub Codespace** | The real website, the admin dashboard and the API with the sample store, in your browser | A computer or tablet with a browser, your GitHub account |

---

## A. Demo app on your Android phone

### Get a new copy of the app

Someone (you or the team) makes the file once:

1. On GitHub, open the repository, then **Actions** → **Mobile demo APK** (on the left).
2. Click **Run workflow**, keep **Branch: main**, click the green **Run workflow** button.
3. Wait about 10 to 15 minutes until the run has a green tick.

The app is now on the **Releases** page as a "Pre-release" named **Demo app** with a short code, for example
`demo-apk-1a2b3c4`. (Each run on a pull request also keeps the file for 30 days under that run's **Artifacts**,
but artifacts download as a .zip, so the Releases page is easier on a phone.)

### Install it on the phone

1. On the phone, open the browser, sign in to github.com and open the repository.
2. Tap **Releases** (on a phone it is under the repository description; or go to `…/fresh-orbit/releases`).
3. Open the newest **Demo app** pre-release and tap the file ending in **.apk** under **Assets**.
4. The phone may say the file can be harmful. Tap **Download anyway**. It is our own app.
5. Open the downloaded file. The phone asks to **allow installs from this source** (your browser): tap
   **Settings**, switch **Allow from this source** on, go back and tap **Install**.
6. Open **Fresh Orbit** from the app list.

After installing you can switch "Allow from this source" off again (Settings → Apps → your browser → Install
unknown apps).

### Using the demo

- Any phone number works; any 6-digit code signs you in.
- Pincode `999999` shows the "we don't deliver here yet" screen; any other pincode is served.
- **Account** tab → **Demo** panel: switch on empty lists or offline mode, and jump to each screen state.
- To update the app, install a newer APK the same way; it replaces the old one.

This is a test build (Flutter "profile" mode, signed with a test key). It is not the Play Store app, and the
store build will refuse demo mode.

---

## B. The whole store in a GitHub Codespace

A Codespace is a computer in GitHub's cloud that runs the store for you. You use it from the browser.

### Open it

1. On github.com open the repository. Click the green **Code** button → **Codespaces** tab → **Create
   codespace on main**. (To test a branch, first pick that branch on the repository page.)
   If GitHub asks for a machine type, choose **4-core**.
2. Wait until the editor opens and the terminal at the bottom says
   `Ready. Start the store with: bash infra/scripts/codespace-up.sh`. The first time takes about 5 to 10 minutes.
3. Click in the terminal, type this and press Enter:

   ```bash
   bash infra/scripts/codespace-up.sh
   ```

   The first start takes about 5 minutes. At the end it prints a list of links and the **staff password**.
   **Write the staff password down now**: it is shown only this once.
4. Open the **PORTS** tab (next to TERMINAL). Hover over a row and click the globe icon to open it:

   | Port | Label | What it is |
   |---|---|---|
   | 3000 | Website (customer) | The shop as customers see it |
   | 5173 | Admin (staff) | The staff dashboard |
   | 5080 | API | The server; the sign-in codes are read from here |
   | 8025 | E-mails (Mailpit) | Every e-mail the store sends, caught here instead |

   The links work only for you, while you are signed in to GitHub in the same browser.

### Test data

| What | Value |
|---|---|
| Delivery pincode | `530017` (also `530016`, `530048`, `530026`) — the sample store in Visakhapatnam |
| Customer phone | Any 10-digit mobile, for example `9999900301` (a sample customer with an address) |
| Customer sign-in code (OTP) | No SMS is sent. Open the API link (port 5080) and add `/api/v1/dev/sms?phone=+919999900301` to the end of the address, using `+91` and your 10 digits. The newest message is first; the code is in its `text`. |
| Admin e-mail | `sample.admin@freshorbit.example` (or `sample.finance@freshorbit.example` for the finance role) |
| Admin password | The staff password printed by `codespace-up.sh` |

Lost the staff password? Run `bash infra/scripts/codespace-up.sh --reset`. This deletes all test data (orders
too), loads the sample store again and prints a new password.

Things to try: pick pincode 530017, add fish to the cart, choose a slot, place a cash-on-delivery order, then in
the admin find the order, pack it, dispatch it and mark it delivered; watch the e-mails in Mailpit.

### Next time

Open github.com → **Code** → **Codespaces** and click your existing codespace (it keeps the data). Then run
`bash infra/scripts/codespace-up.sh` again. If it says something is "already running", that is fine.

### Stop it so it does not use your hours

- **Stop when you finish:** github.com → **Code** → **Codespaces** → the **…** next to your codespace →
  **Stop codespace**. Or, inside the Codespace, press `F1`, type `stop current codespace` and press Enter.
- If you forget, GitHub stops it by itself after **30 minutes** without use.
- A stopped codespace keeps your data but uses no hours. It still counts towards storage, so **delete** it
  (same **…** menu → **Delete**) when you will not test for a while. GitHub deletes a codespace by itself after
  30 days without use.

### What it costs

- A personal GitHub account gets a **free monthly quota** of Codespaces use: **120 core-hours** and 15 GB of
  storage on the Free plan (180 core-hours and 20 GB on Pro). The 4-core machine uses 4 core-hours per hour,
  so that is about **30 hours of testing a month** on the Free plan.
- Personal accounts have a **$0 spending limit** by default. When the quota is used up, Codespaces simply stop
  working until next month; **you are not charged**. You can check use at github.com → **Settings** →
  **Billing and plans**.

### If something goes wrong

- "Docker is not running": wait one minute after the Codespace opens and run the command again.
- A link shows an error: run the command again; it starts only what is missing. The logs are in the folder
  `.codespace-logs/` (one file per part: `api.log`, `web.log`, `admin.log`, `worker.log`, `migrations.log`).
- The shop shows no products for 530017 right after starting: wait one minute and reload.

---

## For the team

- Workflow `.github/workflows/mobile-demo.yml` builds `apps/mobile` with
  `flutter build apk --profile --dart-define=DEMO=true`. Not release, because `lib/main.dart` refuses DEMO in a
  release build on a device; profile is compiled ahead of time, so it is fast and small, and Flutter signs it with
  the debug key, so CI needs no secrets. It runs on ARM phones, not on x86 emulators. A manual run from `main`
  publishes the prerelease `demo-apk-<short sha>`.
- `.devcontainer/` (.NET SDK 10.0.102, Node 22 with pnpm from corepack, docker-in-docker, 4-core) and
  `infra/scripts/codespace-up.sh` (`--stop`, `--reset`); `.github/workflows/devcontainer.yml` builds the
  container and runs the script end to end, including an OTP sign-in through the website and a staff sign-in
  through the admin.
- Development-only allowances for forwarded URLs: the website's `/bff/auth/*` origin check also accepts
  `NEXT_PUBLIC_SITE_URL` when not in production (the dev server behind the forwarder sees its own URL as
  localhost); Next.js `allowedDevOrigins` and Vite `allowedHosts` list the Codespaces domain only when
  `GITHUB_CODESPACES_PORT_FORWARDING_DOMAIN` is set. The admin talks to the API through the Vite proxy (same
  origin), so the API's CORS list is unchanged. Refresh and cart cookies are `Secure` only in production builds;
  Codespaces serves https anyway.
