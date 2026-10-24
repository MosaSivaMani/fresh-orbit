# Free public demo on Render and Neon

A free website, admin dashboard and API that you can share as links, so other people can try the store from their
own phone or computer. It runs the made-up **Visakhapatnam sample store**. It costs nothing and needs no card.

**It is not for real customers.** Every SMS, including the sign-in codes, can be read by anyone who has the API link.
Every page shows the banner **"DEMO, test data only"**. Testers must use made-up names, phone numbers and addresses.

| Link (if Render keeps the plain names) | What it is |
|---|---|
| `https://fresh-orbit-demo-web.onrender.com` | The customer website |
| `https://fresh-orbit-demo-admin.onrender.com` | The admin dashboard (staff) |
| `https://fresh-orbit-demo-api.onrender.com` | The API (testers need it only to read sign-in codes) |

What you need: your GitHub account (the one that owns the `fresh-orbit` repository) and about 45 minutes the
first time, most of it waiting. Nothing is installed on any computer.

The settings live in `render.yaml` at the top of the repository (on **main** since PR #10).

---

## Step 1. Create the free database (Neon)

1. Open **neon.tech** and click **Sign up**. Choose **Continue with GitHub**. Neon does not ask for a card.
2. Create a project:
   - **Project name:** `fresh-orbit-demo`
   - **Postgres version:** 17
   - **Region:** **AWS Asia Pacific (Singapore)** (the same place as the Render services, so it is fast)
   - Click **Create project**.
3. On the project page click **Connect** (or **Connection string**).
   - Switch **Connection pooling** **off**.
   - In the list of languages, choose **.NET**.
   - Click **Show password**, then **Copy**.
4. Paste it into a note for a moment. It looks like this (one line):

   `Host=ep-cool-name-123456.ap-southeast-1.aws.neon.tech; Database=neondb; Username=neondb_owner; Password=AbC123xyz; SSL Mode=VerifyFull; Channel Binding=Require;`

   If Neon only shows an address that starts with `postgresql://`, build the line yourself from its parts:
   `postgresql://USER:PASSWORD@HOST/DATABASE?sslmode=require` becomes
   `Host=HOST; Database=DATABASE; Username=USER; Password=PASSWORD; SSL Mode=Require`.

Use a **new, empty** Neon project. The demo checks this: it uses a database only if it is completely empty the first
time, and marks it as the demo's. It refuses any other database and changes nothing in it.

## Step 2. Choose the staff password

Choose a password for the sample staff accounts, for example `Sample-Demo-Pass-2026`. It needs **at least 12
characters** and must not be a common password. Write it down; you will type it in step 3 and when you sign in to
the admin dashboard.

## Step 3. Create the three services (Render)

1. Open **render.com** and click **Get Started**. Choose **GitHub** and sign in. Render does not ask for a card for
   free services.
2. When Render asks which repositories it may see, allow **fresh-orbit** (only that one is enough).
3. In the Render dashboard click **New** → **Blueprint**.
4. Pick the **fresh-orbit** repository and the branch **main**. Render reads `render.yaml` and lists three services:
   `fresh-orbit-demo-api`, `fresh-orbit-demo-web` and `fresh-orbit-demo-admin`.
5. Give the Blueprint a name, for example `fresh-orbit-demo`.
6. Render asks for two values:
   - **`ConnectionStrings__FreshOrbit`**: paste the Neon line from step 1.
   - **`FRESHORBIT_SAMPLE_STAFF_PASSWORD`**: the password from step 2.
7. Click **Apply** (or **Deploy Blueprint**).

## Step 4. Wait for the build

Render now builds the three services. The first time takes about **10 to 20 minutes** (the API and the website are
built at the same time; the admin dashboard is quicker). Each service shows **Live** with a green dot when it is
ready. If one shows **Failed**, open it, click **Logs**, and see "When something goes wrong" below.

## Step 5. Check the three addresses

Open each service in Render. Its address is at the top of its page.

- If the addresses are exactly the three in the table at the top of this page, go on to step 6.
- If Render added some letters (for example `https://fresh-orbit-demo-api-x7k2.onrender.com`), because someone else
  already uses that name, change these values, then click **Save, rebuild and deploy**:

| Service | Environment value to change | Set it to |
|---|---|---|
| `fresh-orbit-demo-api` | `Cors__AllowedOrigins__0` | the admin dashboard's address |
| `fresh-orbit-demo-api` | `FileStorage__PublicBaseUrl` | the API's address followed by `/media` |
| `fresh-orbit-demo-web` | `FRESH_API_ORIGIN` | the API's address |
| `fresh-orbit-demo-admin` | `VITE_API_ORIGIN` | the API's address |

(Service → **Environment** on the left → the pencil next to the value. Addresses have no `/` at the end.)

## Step 6. Open the links

Free services go to sleep, so wake them in this order:

1. Open the **API** address followed by `/health`, for example `https://fresh-orbit-demo-api.onrender.com/health`.
   The first time after a sleep it can take **1 to 3 minutes**. When it shows **Healthy**, the API is awake.
2. Open the **website** address. It shows the band "DEMO, test data only" at the top.
3. Open the **admin** address.

## Testing with the sample store

**Shop on the website**

1. Set the pincode **530017** (Visakhapatnam). Prices, stock and delivery slots appear.
2. Add some fish to the cart and go to checkout.
3. Sign in with any made-up Indian mobile number, for example **98765 43210**, and tap **Send code**.
4. Read the code: open the API address followed by `/api/v1/dev/sms?phone=%2B919876543210` (`%2B91`, then your 10 digits, no spaces;
   `%2B` is how a browser address writes `+`). The newest message is first; the 6-digit number in `"text"` is the code.
5. Type the code, choose **Cash on delivery** and place the order.

Other sample pincodes are in `backend/data/sample/locations.json`. Sample customers already exist with the numbers
`+91 99999 00301`, `…302` and `…303` (their codes are read the same way).

**Run the store in the admin dashboard**

- Email **`sample.admin@freshorbit.example`**, password from step 2. This account can do everything.
- Email **`sample.finance@freshorbit.example`**, same password: the finance role only.
- The order from the website appears under **Orders**. Staff can confirm, pack, dispatch and deliver it.

**Sharing:** send testers the website and admin links and this page's "Testing" part. Tell them it is test data,
that nothing is delivered, and that they must not type real personal details: anyone with the API link can read the
sign-in codes and messages.

## Limits of the free plans

- **Sleep.** A Render free service sleeps after **15 minutes** without visitors. The next visit wakes it: the first
  page then takes **about 1 to 3 minutes** (the API also re-checks the database and the sample store when it wakes).
  After that it is quick until the next sleep.
- **Pictures.** The services have no lasting disk. After a sleep the sample pictures are drawn again automatically.
  A photo that a tester uploads in the admin dashboard is lost at the next sleep (the product then shows the sample
  drawing).
- **Database.** Neon's free plan has **0.5 GB**, plenty for a demo. Neon also pauses the database after 5 minutes
  without use and wakes it in about a second. Data stays across sleeps: orders, customers and admin changes remain.
- **Monthly allowance.** Render gives 750 free service hours a month for all free services together, and a limited
  number of build minutes. A demo used now and then stays well inside. If it runs out, Render pauses the services
  until the next month.
- **Speed.** Free services are small and slow. It is good for trying things, not for measuring speed.
- **Updates.** Each change merged into the branch is built and deployed again automatically (only the parts that
  changed).
- **Free plans can change.** Render and Neon may change or end their free plans. Check their pricing pages if
  something stops working.

## Start again with fresh sample data

1. In Neon, delete the project (project → **Settings** → **Delete project**) and create a new one (step 1).
2. In Render, open `fresh-orbit-demo-api` → **Environment**, replace `ConnectionStrings__FreshOrbit` with the new
   line, and click **Save, rebuild and deploy**.

The new password for the staff accounts, if you want one, goes into `FRESHORBIT_SAMPLE_STAFF_PASSWORD` before
this step: the password is set only when the staff accounts are first created.

## Delete everything

1. **Render:** open each of the three services → **Settings** → at the bottom **Delete Web Service** (or **Delete
   Static Site**), and type the name to confirm. Then open **Blueprints** and delete `fresh-orbit-demo`.
2. **Neon:** project → **Settings** → **Delete project**.
3. Optional: on GitHub, **Settings** → **Applications** → remove Render's and Neon's access.

Nothing else is left behind, and nothing is charged.

## When something goes wrong

Open the service in Render → **Logs**.

| What you see | What it means and what to do |
|---|---|
| `Demo refused: the database is NotDemo` and the API stops | The database is not empty and was not created by the demo. Make a new Neon project (step 1) and paste its line (step "Start again") |
| `Start-up refused` with `ConnectionStrings` | The Neon line is missing or wrong. Paste it again (Environment) |
| `password` or `authentication failed` | The Neon line has the wrong password. Copy it again with **Show password** |
| `PASSWORD_TOO_WEAK` | The staff password is too short or too common. Choose another (12 or more characters) |
| You forgot the staff password | If you left `FRESHORBIT_SAMPLE_STAFF_PASSWORD` empty, the API log shows `Sample staff password (... shown once ...)` from the first start. Otherwise start again with fresh data |
| The website says it cannot reach the store | The API is still waking up. Open `<API>/health`, wait for **Healthy**, then reload |
| The admin sign-in says "network error" | The admin address or the API address differs from `render.yaml`: see step 5 |
| Out of memory (`OOM`) or the API restarts again and again | The free plan has 512 MB. Tell the team; the fallback is a paid Render plan or the Codespaces way in `runbooks/owner-testing.md` |

## How it is built (for the team)

- `render.yaml`: three free services in Singapore. Secrets are `sync: false` (asked for once) and the JWT key is
  generated by Render.
- `infra/docker/api-demo.Dockerfile` with `infra/docker/api-demo-start.sh`: one container runs the Migrations host
  (`--sample`) and then the Api, one after the other, with `ASPNETCORE_ENVIRONMENT=Demo`. The Api runs the Worker's
  background jobs itself (`Hosting:RunBackgroundJobsInApi`, `backend/src/Hosts/FreshOrbit.Api/appsettings.Demo.json`).
- What `Demo` changes and what stays strict: `runbooks/security-configuration.md` §6. Options weighed and risks:
  `research/render-free-demo-2026-10.md`.
- CI: `.github/workflows/render-demo.yml` builds the demo image and starts it in 512 MB against an empty Postgres,
  checks sign-in codes, pictures, staff sign-in, a restart, and the refusal of a database that is not the demo's.
