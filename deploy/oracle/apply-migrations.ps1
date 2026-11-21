# Sends deploy/oracle/pb_migrations/*.js to the PokéHome VM and restarts PocketBase,
# which applies any new migration on startup.
# Run from the repo root:
#   powershell -ExecutionPolicy Bypass -File deploy\oracle\apply-migrations.ps1
param(
    [string]$Server = "ubuntu@144.22.255.28",
    [string]$Key = "$env:USERPROFILE\.ssh\ssh-key-2026-08-16.key"
)
$ErrorActionPreference = "Stop"

$dir = Join-Path $PSScriptRoot "pb_migrations"
$files = Get-ChildItem $dir -Filter *.js
if (-not $files) { Write-Host "No migrations in $dir"; exit 0 }

ssh -i $Key $Server "mkdir -p /tmp/pb_migrations && rm -f /tmp/pb_migrations/*.js"
foreach ($f in $files) {
    Write-Host "-> $($f.Name)"
    scp -q -i $Key $f.FullName "${Server}:/tmp/pb_migrations/"
}

$remote = @'
set -e
sudo mkdir -p /opt/pocketbase/pb_migrations
sudo sh -c 'for f in /tmp/pb_migrations/*.js; do tr -d "\r" < "$f" > "/opt/pocketbase/pb_migrations/$(basename "$f")"; done'
sudo chown -R pocketbase:pocketbase /opt/pocketbase/pb_migrations
sudo systemctl restart pocketbase
sleep 3
systemctl is-active pocketbase
sudo journalctl -u pocketbase --no-pager -n 15 --since "30 seconds ago"
'@

# piped through stdin so Windows line endings never reach bash
$remote | ssh -i $Key $Server "tr -d '' | bash -s"
