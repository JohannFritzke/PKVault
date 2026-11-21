#!/usr/bin/env bash
# Installs PocketBase (behind Caddy with automatic HTTPS) on the PokéHome VM.
# Run from Windows PowerShell:
#   Get-Content deploy\oracle\setup-server.sh -Raw | ssh -i <key> ubuntu@<ip> "tr -d '\r' | DOMAIN=<domain> bash -s"
# Safe to run again: it updates the binary and rewrites the configs.
set -euo pipefail

: "${DOMAIN:?set DOMAIN, e.g. DOMAIN=pokehome-johann.duckdns.org}"

echo "== Caddy (official apt repo)"
if ! command -v caddy >/dev/null; then
    sudo apt-get install -y debian-keyring debian-archive-keyring apt-transport-https curl gpg >/dev/null
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' \
        | sudo gpg --dearmor --yes -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
    curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' \
        | sudo tee /etc/apt/sources.list.d/caddy-stable.list >/dev/null
    sudo apt-get update >/dev/null
    sudo apt-get install -y caddy >/dev/null
fi
caddy version

echo "== PocketBase (latest release)"
url=$(curl -fsSL https://api.github.com/repos/pocketbase/pocketbase/releases/latest \
    | grep -o '"browser_download_url": *"[^"]*linux_amd64.zip"' | head -1 | sed 's/.*"\(https[^"]*\)"/\1/')
[ -n "$url" ] || { echo "could not find the PocketBase download URL"; exit 1; }
echo "$url"

sudo useradd --system --home /opt/pocketbase --shell /usr/sbin/nologin pocketbase 2>/dev/null || true
sudo mkdir -p /opt/pocketbase
tmp=$(mktemp -d)
curl -fsSL "$url" -o "$tmp/pb.zip"
unzip -o -q "$tmp/pb.zip" pocketbase -d "$tmp"
sudo install -m 755 "$tmp/pocketbase" /opt/pocketbase/pocketbase
rm -rf "$tmp"
sudo chown -R pocketbase:pocketbase /opt/pocketbase
/opt/pocketbase/pocketbase --version

echo "== PocketBase service (only on localhost; Caddy is the public door)"
sudo tee /etc/systemd/system/pocketbase.service >/dev/null <<'EOF'
[Unit]
Description=PocketBase (PokéHome)
After=network.target

[Service]
Type=simple
User=pocketbase
Group=pocketbase
WorkingDirectory=/opt/pocketbase
ExecStart=/opt/pocketbase/pocketbase serve --http=127.0.0.1:8090 --dir=/opt/pocketbase/pb_data
Restart=on-failure
RestartSec=5
LimitNOFILE=4096

[Install]
WantedBy=multi-user.target
EOF

echo "== Caddy site"
sudo tee /etc/caddy/Caddyfile >/dev/null <<EOF
${DOMAIN} {
    encode gzip
    reverse_proxy 127.0.0.1:8090
}
EOF

sudo systemctl daemon-reload
sudo systemctl enable --now pocketbase >/dev/null
sudo systemctl restart pocketbase
sudo systemctl reload caddy || sudo systemctl restart caddy

sleep 3
echo "== Status"
systemctl is-active pocketbase caddy
curl -fsS http://127.0.0.1:8090/api/health && echo

echo "== First admin: open the link below in your browser (replace the host if it shows 127.0.0.1:8090)"
sudo journalctl -u pocketbase --no-pager -n 50 | grep -o 'http[^ ]*pbinstal[^ ]*' | tail -1 \
    || echo "(no install link: the admin already exists)"
