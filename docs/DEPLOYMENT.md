# Deploying OpsHub

How OpsHub runs outside a developer's machine: what the production setup looks like, every
setting, the first deployment, a public demo deployment, upgrades, backups and the checks
to do before going live.

**What has been verified, and how** ([§ 11](#11-how-this-was-verified)): the production build
was run locally in Production and Demo mode over HTTPS, from an empty database, and checked
in the browser and with the smoke test. The container image is built and tested by CI, behind
a real HTTPS reverse proxy. OpsHub has not been deployed to a particular cloud provider; the
instructions below are generic Docker steps.

## 1. How it runs in production

```mermaid
flowchart LR
  B[Browser] -- HTTPS --> P["Reverse proxy or platform<br/>(ends HTTPS, e.g. Caddy)"]
  P -- "HTTP + X-Forwarded-For/Proto" --> C
  subgraph C [OpsHub container]
    K["ASP.NET Core on :8080<br/>· Angular app (wwwroot)<br/>· /api<br/>· /health"]
  end
  K --> V[("/data volume<br/>opshub.db · keys/")]
```

- **One container, one origin.** The API serves the built Angular app as well as `/api`. The
  browser only ever talks to one address, so the `SameSite=Strict` session cookie works and
  no CORS is needed: the same arrangement as the development proxy.
- **HTTPS ends at the proxy.** The container speaks plain HTTP on port 8080. The proxy passes
  on the original scheme and client address in `X-Forwarded-Proto` and `X-Forwarded-For`, and
  OpsHub only believes those headers from proxies listed in `ReverseProxy` settings.
- **The API only works over HTTPS.** Its cookies are `Secure`, so an API request that arrives
  over plain HTTP gets a clear `400 HTTPS is required`. `/health` and the page itself still
  answer over HTTP, because platform health checks use it.
- **`/data` is the only state.** It holds the SQLite database and the keys that sign session
  cookies and XSRF tokens. Lose the database and you lose the data; lose the keys and everyone
  is signed out. Put `/data` on a persistent volume and back it up.
- **Exactly one instance.** SQLite is a file, so OpsHub can't be scaled out to several
  containers as it is ([§ 10](#10-limits)).

## 2. Environments

| | Development | Demo | Production |
|---|---|---|---|
| Used for | Your machine | A public portfolio demo | A real installation |
| Started by | `dotnet run` + `npm start` | Container, `ASPNETCORE_ENVIRONMENT=Demo` | Container (the default) |
| Fictional Marula data | Yes | Yes | **Never** (the app refuses to start) |
| API docs (`/scalar`) | Yes | No | No |
| HSTS | No | Yes | Yes |
| Logs | Readable text | One JSON object per line | One JSON object per line |

(`Testing` is used only by the automated tests.)

## 3. Settings

Every setting can be given as an environment variable: replace `:` with `__` (two
underscores). Lists use an index: `ReverseProxy__KnownNetworks__0`.

| Environment variable | Default (container) | What it does |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Production` or `Demo` ([§ 2](#2-environments)) |
| `ConnectionStrings__OpsHub` | `Data Source=/data/opshub.db` | The SQLite database file |
| `DataProtection__KeysDirectory` | `/data/keys` | Where the cookie-signing keys are kept. Must survive restarts |
| `DataProtection__CertificatePath` | none | A .pfx certificate that encrypts those keys ([§ 4](#4-first-deployment) step 3). Recommended |
| `DataProtection__CertificatePassword` | none | **Secret.** The certificate's password |
| `Database__ApplyMigrationsOnStartup` | `true` | Update the database schema when the app starts ([§ 6](#6-upgrading)) |
| `ReverseProxy__Enabled` | `false` | Believe `X-Forwarded-*` headers from known proxies. Turn on behind a proxy |
| `ReverseProxy__KnownProxies__0`, `__1`, … | none | Exact proxy addresses, e.g. `172.17.0.1` |
| `ReverseProxy__KnownNetworks__0`, `__1`, … | none | Proxy address ranges, e.g. `10.0.0.0/8` |
| `Bootstrap__AdminEmail` | none | First administrator's email ([§ 4](#4-first-deployment)) |
| `Bootstrap__AdminPassword` | none | **Secret.** First administrator's password, at least 12 characters. Remove after the first start |
| `Bootstrap__AdminDisplayName` | `Administrator` | First administrator's name |
| `Seed__Enabled` | `false` | Demo only: fill an empty database with the fictional data |
| `Seed__DemoPassword` | none | **Secret.** Demo only: the password of every fictional account |
| `Auth__LoginAttemptsPerMinute` | `10` | Sign-in requests allowed per client IP address per minute |
| `AllowedHosts` | `*` | Host names the app answers to, e.g. `opshub.example.com` ([§ 9](#9-checklist-before-going-live)) |
| `ASPNETCORE_HTTP_PORTS` | `8080` | Port inside the container |
| `Email__Enabled` | `false` | Send password-reset links and ticket notifications |
| `Email__From` | none | Sender address, e.g. `opshub@yourcompany.example` |
| `Email__PublicBaseUrl` | none | The address people open OpsHub at; links in emails point here |
| `Email__Smtp__Host`, `__Port`, `__UseTls` | none, `587`, `true` | Your mail provider's SMTP server (STARTTLS on port 587) |
| `Email__Smtp__Username` | none | SMTP user name |
| `Email__Smtp__Password` | none | **Secret.** SMTP password |

**Email** is off unless `Email__Enabled=true`. With it on, people can reset a forgotten password
(the sign-in page offers it only then) and get ticket notifications. Use your mail provider's
SMTP relay and keep its password in the environment file:

```bash
Email__Enabled=true
Email__From=opshub@yourcompany.example
Email__PublicBaseUrl=https://opshub.yourcompany.example
Email__Smtp__Host=smtp.yourprovider.example
Email__Smtp__Username=<user name from your provider>
Email__Smtp__Password=<password from your provider>
```

If email is on but the sender, the link address or the mail server is missing, the app stops at
startup and says which. Emails are queued and sent in the background, retried twice, and logged
by type and ticket reference only, never by address.

Secrets belong in an environment file that is never committed (as below) or in the hosting
platform's secret store, never in `appsettings.json` or in the image.

## 4. First deployment

These steps assume a Linux server with Docker, and a reverse proxy on the same server that
handles HTTPS for your domain. They use [Caddy](https://caddyserver.com/), which gets and
renews a certificate by itself; any reverse proxy works.

**1. Build the image** (from the repository root):

```bash
docker build --tag opshub .
```

**2. Write the settings to a file that only you can read.** Keeping the password out of the
command line keeps it out of your shell history and the process list.

```bash
cat > opshub.env <<'EOF'
ReverseProxy__Enabled=true
ReverseProxy__KnownProxies__0=172.17.0.1
Bootstrap__AdminEmail=you@yourcompany.example
Bootstrap__AdminDisplayName=Your Name
Bootstrap__AdminPassword=<a long password of your own>
EOF
chmod 600 opshub.env
```

`172.17.0.1` is the address a container usually sees for a proxy running on the same machine
(the Docker bridge's gateway). Check yours with
`docker network inspect bridge --format '{{(index .IPAM.Config 0).Gateway}}'`.

**3. Create a certificate that encrypts the session keys (recommended).** Without one, the
keys in `/data/keys` are stored as plain XML, and anyone who can read them, for example from
a backup, could forge a session cookie. The certificate is kept outside the data volume.

```bash
read -rs -p "Certificate password: " OPSHUB_KEYS_PASSWORD && export OPSHUB_KEYS_PASSWORD && echo
openssl req -x509 -newkey rsa:3072 -nodes -days 3650 -subj "/CN=OpsHub key protection" \
  -keyout keys.key -out keys.crt
openssl pkcs12 -export -inkey keys.key -in keys.crt -out opshub-keys.pfx -passout env:OPSHUB_KEYS_PASSWORD
rm keys.key keys.crt
sudo mkdir -p /etc/opshub && sudo mv opshub-keys.pfx /etc/opshub/
sudo chown 1654 /etc/opshub/opshub-keys.pfx && sudo chmod 400 /etc/opshub/opshub-keys.pfx
```

`1654` is the image's unprivileged `app` user, the only account that needs to read the file.
Add the two settings to `opshub.env`:

```bash
cat >> opshub.env <<'EOF'
DataProtection__CertificatePath=/run/secrets/opshub-keys.pfx
DataProtection__CertificatePassword=<the certificate password>
EOF
```

Back up `opshub-keys.pfx` and its password **separately** from the data backups. Without
them the stored keys can't be decrypted: nothing in the database is lost, but everyone is
signed out and must sign in again.

**4. Start the container.** The port is published on `127.0.0.1` only, so the outside world
can reach OpsHub only through the proxy.

```bash
docker volume create opshub-data
docker run --detach --name opshub --restart unless-stopped \
  --publish 127.0.0.1:8080:8080 \
  --volume opshub-data:/data \
  --volume /etc/opshub/opshub-keys.pfx:/run/secrets/opshub-keys.pfx:ro \
  --env-file opshub.env \
  opshub
docker logs opshub
```

If you skipped step 3, leave out the `opshub-keys.pfx` line.

On the first start the log shows the schema being created, the roles and categories being
added, and `Bootstrap created administrator you@yourcompany.example`.

**5. Point the proxy at it.** A complete Caddyfile:

```
opshub.yourcompany.example {
    reverse_proxy 127.0.0.1:8080
}
```

**6. Check the deployment** from any machine with Bash and curl:

```bash
SMOKE_PASSWORD='<the admin password>' scripts/smoke-test.sh https://opshub.yourcompany.example you@yourcompany.example
```

It checks the health endpoint, the page and its security headers, that the API refuses
anonymous calls, a real sign-in, and that ticket categories exist. It only reads.

**7. Remove the bootstrap password.** Delete the `Bootstrap__AdminPassword` line from
`opshub.env` and recreate the container (`docker rm -f opshub`, then step 4 again). The
account stays; the settings are ignored once an administrator exists. Then sign in, change
the password on the **Account** page, and add your agents and requesters under **Users**.

## 5. A public demo

To let visitors try OpsHub with the fictional Marula Logistics data:

```bash
cat > demo.env <<'EOF'
ASPNETCORE_ENVIRONMENT=Demo
Seed__Enabled=true
Seed__DemoPassword=<the password you will publish>
ReverseProxy__Enabled=true
ReverseProxy__KnownProxies__0=172.17.0.1
EOF
docker run --detach --name opshub-demo --publish 127.0.0.1:8080:8080 --env-file demo.env opshub
```

Every fictional account shares the published password, so visitors can change anything,
including other accounts' passwords and the administrator's settings. Treat the demo as
disposable:

- Don't attach a named volume, as above. Docker then gives each new container its own empty
  volume, so recreating the container starts again with fresh demo data.
- Recreate it regularly, for example nightly with a scheduled
  `docker rm --force --volumes opshub-demo` (which also deletes its volume) followed by the
  `docker run` line.
- Consider publishing only a requester and an agent account, not the administrator.

## 6. Upgrading

```bash
git pull
docker build --tag opshub .
docker rm -f opshub
docker run ...   # the same command as in § 4 step 4
```

The data is in the volume, so it survives. Pending database migrations are applied when the
new version starts. **Back up first** ([§ 7](#7-backups)): a migration can't be undone by
starting the old version again.

Migrating at startup is safe because exactly one instance runs. With a server database and
several instances, migrations should become a separate deployment step instead (for example an
EF Core migration bundle), so two instances never migrate at the same time.

## 7. Backups

SQLite keeps recent changes in `opshub.db-wal` next to the database, so copy the whole folder
while the app is stopped. That takes a few seconds of downtime:

```bash
docker stop opshub
docker run --rm --volume opshub-data:/data --volume "$PWD":/backup alpine \
  tar czf /backup/opshub-$(date +%F).tar.gz -C /data .
docker start opshub
```

The archive includes the cookie-signing keys. With a certificate ([§ 4](#4-first-deployment) step 3)
they are encrypted; without one they are plain XML, so the archive must be protected like a
secret. Either way, store backups encrypted and off the server, and keep the certificate and
its password in a separate place from them.

**Restore:** stop the container, empty the volume, unpack the archive into it, start again:

```bash
docker stop opshub
docker run --rm --volume opshub-data:/data --volume "$PWD":/backup alpine \
  sh -c 'rm -rf /data/* && tar xzf /backup/opshub-2026-10-25.tar.gz -C /data'
docker start opshub
```

## 8. Health and logs

- **`GET /health`** returns `Healthy` (200) when the app runs and the database answers, and
  503 otherwise. Point the platform's or proxy's health check at it. It works over plain HTTP.
- **Logs** go to standard output (`docker logs opshub`) as one JSON object per line, ready for
  a log platform. Worth alerting on:
  - `Refused an API request made over plain HTTP`: the proxy isn't configured under
    `ReverseProxy`, so sign-in will fail;
  - repeated `Sign-in failed` from many accounts: password guessing;
  - any `Error`.
- **Expected warnings** on a correct installation:
  - only without a key certificate: `DataProtection:CertificatePath is not set` and
    `No XML encryptor configured`; see the keys item in [§ 9](#9-checklist-before-going-live);
  - `Failed to determine the https port for redirect`: the container has no HTTPS port of its
    own; the proxy handles HTTPS.

## 9. Checklist before going live

- [ ] HTTPS works at the proxy, and plain HTTP redirects to it (Caddy does both by default).
- [ ] `ReverseProxy__Enabled=true` with the proxy's address. Test: sign-in works, and the log
      has no `Refused an API request made over plain HTTP`.
- [ ] Port 8080 is not reachable from outside the server, only through the proxy.
- [ ] `/data` is a persistent volume, and a backup has been made **and restored once** to
      prove it works.
- [ ] `Bootstrap__AdminPassword` removed after the first start, and that password changed.
- [ ] `ASPNETCORE_ENVIRONMENT` is `Production` (or unset); `Seed__Enabled` is not set.
- [ ] `AllowedHosts` set to your domain, so requests for other host names are refused. Only
      if the platform's health check calls `/health` with an IP address as the host name,
      leave it at `*`.
- [ ] Session keys encrypted with a certificate ([§ 4](#4-first-deployment) step 3), and the
      certificate and its password backed up separately. Without it, anyone who can read
      `/data/keys` or a backup can forge a session cookie.
- [ ] If email is on: ask for a password-reset link for your own account, and check it arrives
      and opens your real address.
- [ ] The smoke test passes against the real address.

## 10. Limits

- **Single instance.** SQLite and file-based keys support one running container. Scaling out
  needs a server database (EF Core supports PostgreSQL and SQL Server), shared key storage and
  migrations as a separate step.
- **Email is send-only.** Replies to notification emails aren't read, and there is no email
  verification or two-factor sign-in.
- **Categories** start as five general IT categories; administrators change them under **Categories**.
- **No image registry.** CI builds and tests the image but doesn't publish it; build it on
  the server as above.
- **Key encryption is optional.** Without the certificate the session keys are stored as plain
  XML ([§ 9](#9-checklist-before-going-live)). The certificate is a file you manage; a cloud
  key vault would be the next step.

## 11. How this was verified

**Automated, on every push** (`.github/workflows/ci.yml`, job *Container*):

1. Build the image from a clean checkout.
2. Start it with an empty volume in Production, behind Caddy as an HTTPS reverse proxy, with
   `ReverseProxy` and `Bootstrap` settings and a key certificate created with the `openssl`
   commands from [§ 4](#4-first-deployment): a first deployment.
3. Run `scripts/smoke-test.sh` through the proxy, verifying TLS against Caddy's own
   certificate authority.
4. Check that plain HTTP straight to the container reaches `/health` but the API refuses it.
5. Check that the session key on the volume is encrypted.
6. Restart the container and run the smoke test again: data, keys and the administrator must
   survive, the key must still be the only one (it was decrypted, not replaced), and no second
   administrator is created.
7. Start a second container in Demo mode and run `scripts/a11y-check.mjs`: axe-core on every
   screen and the main dialogs, under the production Content-Security-Policy.

**Integration tests** (`tests/OpsHub.Api.IntegrationTests`): first-run setup and the
bootstrap rules (`FirstRunTests`), serving the Angular app and its caching and security
headers (`FrontendHostingTests`), trusted and untrusted proxies (`ReverseProxyTests`), the
HTTPS-only API (`HttpsOnlyApiTests`) and key storage (`KeyStorageTests`). The most important
rules were mutation-tested by hand: each was broken on purpose to confirm a test fails.

**By hand** (Windows, without Docker): the published app with the Angular
build in `wwwroot`, run in Production over HTTPS from an empty database. Checks:
- the smoke test;
- in the browser:
  - sign-in through a bookmarked address;
  - filing a ticket;
  - adding a user through the dialog;
  - no Content-Security-Policy violations in the console;
- a restart, after which the session was still valid and the bootstrap step was skipped;
- Demo mode from an empty database.

**Not verified:** a deployment to a real server or cloud platform, and this guide's Docker
commands on a real server. They are standard Docker usage; the CI job covers the same image
and settings.
