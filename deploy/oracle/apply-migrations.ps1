# Sends deploy/oracle/pb_migrations/*.js, deploy/oracle/pb_hooks/*.pb.js and deploy/oracle/pb_public/*.html
# (public pages, e.g. the privacy policy) to the PokéHome VM
# and restarts PocketBase, which applies new migrations and loads hooks on startup.
# Run from the repo root:
#   powershell -ExecutionPolicy Bypass -File deploy\oracle\apply-migrations.ps1
param(
    [string]$Server = "ubuntu@144.22.255.28",
    [string]$Key = "$env:USERPROFILE\.ssh\ssh-key-2026-08-16.key"
)
$ErrorActionPreference = "Stop"

$folders = @("pb_migrations", "pb_hooks", "pb_public")

ssh -i $Key $Server "rm -rf /tmp/pb_deploy && mkdir -p /tmp/pb_deploy/pb_migrations /tmp/pb_deploy/pb_hooks /tmp/pb_deploy/pb_public"
foreach ($folder in $folders) {
    $dir = Join-Path $PSScriptRoot $folder
    if (-not (Test-Path $dir)) { continue }
    foreach ($f in Get-ChildItem $dir -File | Where-Object { $_.Extension -in '.js', '.html' }) {
        Write-Host "-> $folder/$($f.Name)"
        scp -q -i $Key $f.FullName "${Server}:/tmp/pb_deploy/$folder/"
    }
}

$remote = @'
set -e
for folder in pb_migrations pb_hooks pb_public; do
    sudo mkdir -p "/opt/pocketbase/$folder"
    for f in /tmp/pb_deploy/$folder/*; do
        [ -e "$f" ] || continue
        sudo sh -c "tr -d '\r' < '$f' > '/opt/pocketbase/$folder/$(basename "$f")'"
    done
    sudo chown -R pocketbase:pocketbase "/opt/pocketbase/$folder"
done
sudo systemctl restart pocketbase
sleep 3
systemctl is-active pocketbase
sudo journalctl -u pocketbase --no-pager -n 15 --since "30 seconds ago"
'@

# piped through stdin so Windows line endings never reach bash
$remote | ssh -i $Key $Server "tr -d '\r' | bash -s"
