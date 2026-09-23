# PDFHub — Deployment

This follows the `tools-windows-iis-deploy` skill. Anything specific to this app lives here.

## Environment

| Item | Value |
|---|---|
| Server | The factory's Windows Server, one machine (no separate QA) |
| Web server | IIS + ASP.NET Core Hosting Bundle for .NET 10 |
| Database | SQLite file `pdfhub.db` inside the data folder — no database server to install |
| Public URL | `http://<server>/PDFHub` |

## IIS layout

| Path | Component | App pool | Anonymous | Windows auth |
|---|---|---|---|---|
| `/PDFHub` | `PDFHub.Api` — serves the API **and** the React build from `wwwroot` | `PDFHub` (No Managed Code) | On | Off |

One IIS application instead of the skill's usual `/Service` + `/React` pair: the factory has one server,
so a single app avoids CORS, a second pool and a `web.config` SPA fallback. React deep links are handled
by `MapFallbackToFile("index.html")` in the API (option 3 in the skill).

## Data folder (`PdfHub:DataRoot`, default `D:\PDFHubData`)

```
D:\PDFHubData\
  pdfhub.db               database (never copy while the app runs — use backup\)
  pdf\NB\NB-06618.pdf     PDFs, one folder per section
  pdf\_archive\           replaced or deleted PDFs (never deleted automatically)
  backup\pdfhub-yyyyMMdd.db   one consistent copy per day, kept 30 days
```

## Settings that are not optional

| Setting | Where | Why |
|---|---|---|
| Data folder outside `C:\inetpub\PDFHub` | `appsettings.Production.json` | `robocopy /MIR` during deploy would otherwise delete the database and PDFs. |
| Pool identity has **Modify** on the data folder | `Setup-PDFHub-IIS.ps1` (icacls) | The app writes the database, PDFs and backups. Without it uploads fail with 403. |
| Pool `startMode=AlwaysRunning`, `idleTimeout=0`, app `preloadEnabled` | `Setup-PDFHub-IIS.ps1` | The daily database backup runs inside the app. IIS stops idle pools after 20 minutes, i.e. every night. |
| `requestLimits maxAllowedContentLength=115343360` | `web.config` | IIS rejects bodies over 30 MB before the app sees them; PDFs may be up to 100 MB. Raise both this and `PdfHub:MaxPdfSizeMB` together. |
| Anonymous on, Windows auth off | `Setup-PDFHub-IIS.ps1` | The app has its own username/password login (the factory has no domain). |
| `appsettings.Production.json` excluded from the mirror copy | `Deploy-PDFHub.ps1` | It holds the server's settings; a deploy must never overwrite it. |

## Scripts

| Script | Runs on | Purpose |
|---|---|---|
| `scripts/Setup-PDFHub-IIS.ps1` | Server, as Administrator | Creates/updates the pool, the IIS application, permissions and `appsettings.Production.json`. Safe to re-run. |
| `scripts/Deploy-PDFHub.ps1` | Server, or a workstation with `-ServerHost` | Builds React + API, stops the pool, mirrors the files, starts the pool, smoke-tests. |
| `scripts/Backup-PDFHubData.ps1` | Server | Copies PDFs and the daily DB backups to a NAS/USB; `-Register` schedules it daily. |

Build machine needs the .NET 10 SDK and Node.js 20+.

## First-time setup

1. On the server: install IIS (Server Manager → Web Server) and the **.NET 10 Hosting Bundle**, then `iisreset`.
2. Copy the repository to the server (or build elsewhere and deploy with `-ServerHost`).
3. Run as Administrator:
   ```powershell
   .\scripts\Setup-PDFHub-IIS.ps1 -DataRoot D:\PDFHubData
   .\scripts\Deploy-PDFHub.ps1
   ```
4. Open `http://<server>/PDFHub`, log in as `admin` / `admin` and set a new password (forced on first login).
5. Import → choose the old Excel file → **ตรวจสอบ** → **นำเข้า**. Then upload the old `DrawingNMB2026` folder with
   **เลือกทั้งโฟลเดอร์**.
6. Schedule the off-server backup:
   ```powershell
   .\scripts\Backup-PDFHubData.ps1 -Destination \\NAS\Backup\PDFHub -Register -At 22:00
   ```

## Manual backup page (Admin → สำรองข้อมูล)

The admin types a folder **on the server** and presses the button; the copy runs in the background.
Each run writes `<destination>\PDFHub\db\pdfhub-yyyyMMdd-HHmmss.db` (a new consistent copy) and copies
new or changed PDFs to `<destination>\PDFHub\pdf\`. Nothing at the destination is ever deleted.

The files are written by the app pool identity (`IIS AppPool\PDFHub`), so the destination must allow it:

| Destination | What to set up |
|---|---|
| Second disk / USB drive on the server (`E:\PDFHubBackup`) | `icacls E:\PDFHubBackup /grant "IIS AppPool\PDFHub:(OI)(CI)M"` |
| Network share (`\\NAS01\Backup`) in a domain | Give the server's computer account (`DOMAIN\SERVER$`) Modify on the share and folder |
| Network share without a domain (workgroup NAS) | The pool identity cannot log on to the NAS. Run the pool as a local user that also exists on the NAS with the same password (IIS → Application Pools → PDFHub → Identity), or back up to a local disk and copy from there with `Backup-PDFHubData.ps1` |

Use **ทดสอบปลายทาง** first: it writes and deletes a test file and names the account that needs access.
A run in progress when the app restarts is marked failed; start it again.

### Restore

1. Stop the app pool.
2. Copy the chosen `PDFHub\db\pdfhub-....db` to `<DataRoot>\pdfhub.db` (delete `pdfhub.db-wal` and `pdfhub.db-shm` if present).
3. Copy `PDFHub\pdf\` back to `<DataRoot>\pdf\`.
4. Start the app pool.

## Redeploy

```powershell
.\scripts\Deploy-PDFHub.ps1              # on the server
.\scripts\Deploy-PDFHub.ps1 -SkipBuild   # re-copy the last build
```

Database migrations run automatically at startup. Take a copy of `backup\` before a release that changes the schema.

## Smoke tests (run by the deploy script)

`/`, `/drawings` (React deep link), `/api/session/me`, `/api/drawings?pageSize=10` → 200; `/api/no-such-route` → 404.

## Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `500.19` | Hosting Bundle missing | Install it, `iisreset` |
| `503` and no log | Pool stopped or crashing on start | Set `stdoutLogEnabled="true"` in `web.config`, reload once, read `logs\`; set it back to `false` after |
| Upload fails with 403 | Pool identity cannot write the data folder | Re-run `Setup-PDFHub-IIS.ps1` |
| Upload of a large PDF fails with 404.13 | IIS request limit | Raise `maxAllowedContentLength` and `MaxPdfSizeMB` |
| Page loads but shows an old version | Browser cached `index.html` from before | The app sends `no-cache` for it; press Ctrl+F5 once |
| No new file in `backup\` for days | Pool not AlwaysRunning | Re-run `Setup-PDFHub-IIS.ps1` |
