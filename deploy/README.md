# WebScraper API — Droplet deployment (S5)

Production runs **WebScraper.Api** in Docker behind **Caddy** (TLS). The app uses **Neon PostgreSQL** via env vars — there is no Postgres container in this compose file.

Local verification (SQLite, no secrets):

```bash
docker build -t webscraper-api .
docker run --rm -d --name webscraper-test -e DatabaseProvider=Sqlite -p 8080:8080 webscraper-api
curl -fsS http://localhost:8080/health/live
docker stop webscraper-test
```

---

## 1. Droplet (Ubuntu 24.04)

Recommended: **1 vCPU / 2 GB RAM** (~$12/mo). OpenSSH on port 22 only from your IP if possible.

### Install Docker Engine

```bash
sudo apt-get update
sudo apt-get install -y ca-certificates curl
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
sudo chmod a+r /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "${VERSION_CODENAME}") stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
sudo apt-get update
sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"
# log out and back in so group membership applies
```

### Firewall

```bash
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw enable
sudo ufw status
```

---

## 2. Application config

Clone or copy this repository onto the Droplet (or pull a pre-built image from your registry).

Create the env file **outside** the git tree:

```bash
sudo mkdir -p /opt/webscraper
sudo cp deploy/.env.example /opt/webscraper/.env
sudo chmod 600 /opt/webscraper/.env
sudo nano /opt/webscraper/.env
```

Fill every value (see `API_SPORTS_MIGRATION_PLAN.md` § S5):

| Variable | Notes |
|----------|--------|
| `SCRAPER_HOST` | DNS name for Caddy (e.g. `scraper.example.com`) |
| `DatabaseProvider` | `PostgreSQL` |
| `ConnectionStrings__DefaultConnection` | Neon **direct** (non-pooler) connection string; SSL required |
| `ScraperSettings__DataProvider` | `ApiSports` |
| `ScraperSettings__Providers__ApiSports__ApiKey` | api-sports.io key |
| `Jwt__SigningKey` | `openssl rand -base64 48` |
| `InitialAdmin__Email` / `InitialAdmin__Password` | First admin; rotate password after login |
| `ASPNETCORE_ENVIRONMENT` | `Production` (Swagger disabled) |

Point DNS **A/AAAA** records for `SCRAPER_HOST` at the Droplet IP.

---

## 3. Start stack

From the repository root on the Droplet:

```bash
cd deploy
export SCRAPER_HOST=scraper.example.com   # must match /opt/webscraper/.env if compose interpolates host
docker compose build
docker compose up -d
docker compose ps
```

Health: compose waits until `GET /health/live` on the API container succeeds.

Optional: restrict `/admin` to your IP in `Caddyfile` (see [Caddy matchers](https://caddyserver.com/docs/caddyfile/matchers)).

---

## 4. First boot

On first start the API:

1. Runs EF migrations against Neon (`__EFMigrationsHistory` and `__AuthMigrationsHistory` — separate from Drizzle on the chatbot DB).
2. Seeds roles and creates the initial admin when `InitialAdmin__*` is set and no users exist.

1. Open `https://<SCRAPER_HOST>/admin/login`
2. Sign in with `InitialAdmin__Email` / `InitialAdmin__Password`
3. Change the password under user management
4. Create API keys for MCP (`operate` scope for scrape jobs)

Run a test scrape from **New Scrape** or `POST /api/v1/scrape/stats` and confirm jobs in **Jobs**.

---

## 5. Operations

Follow API logs:

```bash
cd deploy
docker compose logs -f api
```

Deploy an update (rebuild on the Droplet or `docker compose pull` if you publish images to a registry):

```bash
cd deploy
git pull   # or sync your release artifact
docker compose build
docker compose up -d
docker compose logs -f api
```

**Single instance only** — the in-process scrape worker and event relay are not safe to scale horizontally without extra coordination.

---

## 6. MCP / API consumers

- Read API: `https://<SCRAPER_HOST>/api/v1/*` with `X-Api-Key`
- MCP: set `NFL_API_URL=https://<SCRAPER_HOST>` and `NFL_API_KEY=<key>` in the MCP client env
