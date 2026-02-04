# Local HTTPS setup (Contoso Orders)

## Goal
Expose **order-notification** on:

- HTTP:  http://localhost:5006
- HTTPS: https://localhost:5007 (recommended)

…and allow the SPA (http://localhost:4200) to call:

- `POST /dev/token`
- SignalR hub: `/hubs/order-status` via **WSS**

## One-time (per machine)

### Windows / macOS
Run:

```powershell
dotnet dev-certs https --trust
```

### Linux
`dotnet dev-certs https --trust` may not integrate with your distro trust store.
You can still use `curl -k` for local testing, or manually add the dev cert to your trust store.

## Docker Compose
`infra/local/up.ps1` / `infra/local/up.sh` will:

1. Export the dotnet dev cert to `infra/local/certs/contoso-devcert.pfx`
2. Mount that PFX into the `order-notification` container
3. Start Kestrel on `8080` (HTTP) and `8081` (HTTPS), mapped to host `5006/5007`

The ports/password are defined in `infra/local/.env` (created from `.env.example`).

## Smoke tests

```bash
curl -vk https://localhost:5007/healthz
curl -vk https://localhost:5007/dev/token   -H "Content-Type: application/json"   -d '{"userId":"CUST-0001"}'
```

If HTTPS is listening but the cert is untrusted, you will see `SSL certificate problem` in curl
and `NET::ERR_CERT_AUTHORITY_INVALID` in the browser.
