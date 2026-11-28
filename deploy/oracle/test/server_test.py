"""
End-to-end tests of the PokéHome PocketBase setup (migrations + pb_hooks), against a LOCAL PocketBase.

    pocketbase superuser upsert admin@test.local TestAdmin12345 --dir ./inst/pb_data --migrationsDir <repo>/deploy/oracle/pb_migrations
    pocketbase serve --http=127.0.0.1:8099 --dir ./inst/pb_data --migrationsDir <repo>/deploy/oracle/pb_migrations --hooksDir <repo>/deploy/oracle/pb_hooks
    python server_test.py

Use a throwaway data dir: the script creates test users, trades and backups. Never run it against the real server.
"""
import json, uuid, urllib.request, urllib.error, sys, time, io, zipfile, hashlib

BASE = "http://127.0.0.1:8099"
results = []

def req(method, path, token=None, body=None, files=None, raw=False):
    headers = {}
    data = None
    if files is not None:
        boundary = uuid.uuid4().hex
        parts = []
        for k, v in (body or {}).items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
        for k, (fname, content) in files.items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"; filename="{fname}"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + content + b"\r\n")
        parts.append(f"--{boundary}--\r\n".encode())
        data = b"".join(parts)
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
    elif body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = token
    r = urllib.request.Request(BASE + path, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(r) as resp:
            content = resp.read()
            return resp.status, (content if raw else (json.loads(content) if content else None))
    except urllib.error.HTTPError as e:
        content = e.read()
        try:
            return e.code, json.loads(content)
        except Exception:
            return e.code, content

def check(name, cond, detail=""):
    results.append((name, bool(cond)))
    print(("PASS " if cond else "FAIL ") + name + ("" if cond else f"  -> {detail}"))

# --- setup
st, r = req("POST", "/api/collections/_superusers/auth-with-password", body={"identity": "admin@test.local", "password": "TestAdmin12345"})
admin = r["token"]

def make_user(name, status="APPROVED"):
    """Users log in only with OAuth2: the test creates them as superuser and takes a token by impersonation."""
    email = f"{name.lower()}@test.local"
    body = {"email": email, "password": "Passw0rd!123", "passwordConfirm": "Passw0rd!123", "name": name}
    if status is not None:
        body["status"] = status
    st, r = req("POST", "/api/collections/users/records", admin, body)
    user_id = r["id"]
    st, r = req("POST", f"/api/collections/users/impersonate/{user_id}", admin, {"duration": 3600})
    return r["token"], user_id

A, a_id = make_user("Ash")
B, b_id = make_user("Brock")
C, c_id = make_user("Misty")
P, p_id = make_user("Pending", status=None)   # created without status: starts PENDING

# --- approval
st, r = req("GET", f"/api/collections/users/records/{p_id}", admin)
check("approval: a new account starts PENDING", r.get("status") == "PENDING", r)
st, r = req("GET", f"/api/collections/users/records/{p_id}", P)
check("approval: a pending user sees its own status", st == 200 and r["status"] == "PENDING", (st, r))
st, r = req("GET", "/api/collections/users/records", P)
check("approval: a pending user sees nobody else", st == 200 and [u["id"] for u in r["items"]] == [p_id], (st, r))
st, r = req("GET", "/api/collections/users/records", A)
check("approval: approved users do not see pending ones", st == 200 and p_id not in {u["id"] for u in r["items"]}, r)
st, r = req("PATCH", f"/api/collections/users/records/{p_id}", P, {"status": "APPROVED"})
check("approval: a user cannot approve itself", st in (403, 404), (st, r))
st, r = req("GET", f"/api/collections/users/records/{p_id}", admin)
check("approval: status unchanged after the attempt", r.get("status") == "PENDING", r)
st, r = req("POST", "/api/collections/users/records", None, {"email": "intruder@test.local", "password": "Passw0rd!123", "passwordConfirm": "Passw0rd!123"})
check("approval: anonymous account creation refused", st in (400, 403), (st, r))
st, r = req("POST", "/api/collections/users/records", P, {"email": "intruder2@test.local", "password": "Passw0rd!123", "passwordConfirm": "Passw0rd!123", "status": "APPROVED"})
check("approval: a user cannot create accounts", st in (400, 403), (st, r))
st, r = req("POST", "/api/collections/users/auth-with-password", body={"identity": "ash@test.local", "password": "Passw0rd!123"})
check("approval: email + password login is off for users", st in (400, 403), (st, r))
st, r = req("DELETE", f"/api/collections/users/records/{a_id}", A)
check("approval: a user cannot delete its account", st in (403, 404), (st, r))
buf = io.BytesIO()
with zipfile.ZipFile(buf, "w") as z:
    z.writestr("n.txt", "pending")
st, r = req("POST", "/api/collections/vault_backups/records", P, {"owner": p_id, "sha256": hashlib.sha256(buf.getvalue()).hexdigest(), "device": "PC"}, {"file": ("p.zip", buf.getvalue())})
check("approval: a pending user cannot upload backups", st in (400, 403), (st, r))
st, r = req("GET", "/api/settings", admin)
check("approval: rate limiter on", st == 200 and r["rateLimits"]["enabled"] is True and len(r["rateLimits"]["rules"]) > 0, (st, r.get("rateLimits") if isinstance(r, dict) else r))

st, r = req("GET", "/api/collections/users/records", A)
check("users: a user can list friends", st == 200 and {u["id"] for u in r["items"]} >= {b_id, c_id}, (st, r))
check("users: emails stay hidden", all("email" not in u or u["email"] == "" for u in r["items"] if u["id"] != a_id), r)
st, r = req("GET", "/api/collections/users/records")
check("users: anonymous cannot list", st == 200 and r["totalItems"] == 0 or st in (401, 403), (st, r))

def pk(fp, species=25, nick="PIKA"):
    return {"fingerprint": fp, "format": "pk3", "species": species, "form": 0, "nickname": nick, "level": 12,
            "shiny": "true", "gender": 1, "otName": "ASH", "tid": 12345, "version": 3}

PIKA = b"PK3-PIKACHU-BYTES"
EEVEE = b"PK3-EEVEE-BYTES"

def offer(token, to, fp="fp-pika", content=PIKA, message="quero um eevee", kind="TRADE"):
    return req("POST", "/api/pokehome/trades", token, {**pk(fp), "to": to, "message": message, "kind": kind}, {"file": ("pika.pk3", content)})

# --- login session
st, r = req("GET", "/api/collections/users", admin)
check("session: user tokens last 30 days", st == 200 and r["authToken"]["duration"] == 30 * 24 * 60 * 60, (st, r.get("authToken") if isinstance(r, dict) else r))

# --- validation
st, r = req("POST", "/api/pokehome/trades", None, {**pk("x"), "to": b_id}, {"file": ("x.pk3", b"x")})
check("offer: anonymous refused", st == 401, (st, r))
st, r = offer(A, a_id)
check("offer: to self refused", st == 400, (st, r))
st, r = req("POST", "/api/pokehome/trades", A, {**pk("x"), "to": b_id})
check("offer: without file refused", st == 400, (st, r))
st, r = req("POST", "/api/collections/trades/records", A, {"from": a_id, "to": b_id, "status": "DONE"})
check("records API: direct trade creation refused", st in (400, 403), (st, r))
st, r = offer(P, a_id, fp="fp-pending")
check("approval: a pending user cannot offer", st == 403, (st, r))
st, r = offer(A, p_id, fp="fp-to-pending")
check("approval: cannot offer to a pending user", st == 400, (st, r))

# --- exchange: offer -> counter -> accept
st, t = offer(A, b_id)
check("offer: created OPEN", st == 200 and t["status"] == "OPEN" and t["kind"] == "TRADE", (st, t))
tid = t["id"]
offered_id = t["offered"]
check("offer: summary stored", t.get("offeredSummary", {}).get("species") == 25 and t["offeredSummary"]["shiny"] is True, t)

st, r = req("GET", f"/api/collections/trades/records/{tid}", B)
check("read: receiver sees trade", st == 200, (st, r))
st, r = req("GET", f"/api/collections/trades/records/{tid}", C)
check("read: stranger cannot see trade", st == 404, (st, r))
st, r = req("GET", f"/api/collections/trade_pokemon/records/{offered_id}", B)
check("read: receiver sees offered pokemon", st == 200 and r["status"] == "CUSTODY", (st, r))
st, r = req("GET", f"/api/collections/trade_pokemon/records/{offered_id}", C)
check("read: stranger cannot see offered pokemon", st == 404, (st, r))

st, r = req("POST", f"/api/pokehome/trades/{tid}/accept", C)
check("accept: stranger refused", st == 400, (st, r))
st, r = req("POST", f"/api/pokehome/trades/{tid}/accept", A)
check("accept: sender cannot accept an OPEN trade", st == 400, (st, r))
st, r = req("POST", f"/api/pokehome/trades/{tid}/accept", B)
check("accept: receiver cannot take a TRADE without answering", st == 400, (st, r))
st, r = offer(A, b_id, "x", kind="LOAN")
check("offer: unknown kind refused", st == 400, (st, r))
st, r = req("POST", f"/api/pokehome/trades/{tid}/counter", A, pk("fp-eevee", 133, "EEVEE"), {"file": ("e.pk3", EEVEE)})
check("counter: sender refused", st == 403, (st, r))

st, t = req("POST", f"/api/pokehome/trades/{tid}/counter", B, {**pk("fp-eevee", 133, "EEVEE"), "counterMessage": "toma"}, {"file": ("e.pk3", EEVEE)})
check("counter: receiver -> COUNTERED", st == 200 and t["status"] == "COUNTERED" and t["countered"], (st, t))
countered_id = t["countered"]

st, r = req("POST", f"/api/pokehome/trades/{tid}/accept", B)
check("accept: receiver cannot accept own counter", st == 400, (st, r))
st, t = req("POST", f"/api/pokehome/trades/{tid}/accept", A)
check("accept: sender -> DONE", st == 200 and t["status"] == "DONE" and t["closedAt"], (st, t))

st, r = req("GET", "/api/collections/trade_pokemon/records?filter=" + urllib.parse.quote("status='TO_CLAIM'"), B)
mine_b = [p for p in r["items"] if p["owner"] == b_id]
check("swap: Brock now owns Pikachu to claim", len(mine_b) == 1 and mine_b[0]["species"] == 25, r)
st, r = req("GET", "/api/collections/trade_pokemon/records?filter=" + urllib.parse.quote("status='TO_CLAIM'"), A)
mine_a = [p for p in r["items"] if p["owner"] == a_id]
check("swap: Ash now owns Eevee to claim", len(mine_a) == 1 and mine_a[0]["species"] == 133, r)

st, r = req("GET", "/api/collections/pokemon_history/records?filter=" + urllib.parse.quote(f"trade='{tid}'"), C)
check("history: 2 entries, readable by any user", st == 200 and r["totalItems"] == 2, (st, r))

# download with file token
p = mine_b[0]
st, ft = req("POST", "/api/files/token", B)
st, content = req("GET", f"/api/files/{p['collectionId']}/{p['id']}/{p['file']}?token={ft['token']}", raw=True)
check("download: new owner gets the exact bytes", st == 200 and content == PIKA, (st, content[:40] if isinstance(content, bytes) else content))
st, ftc = req("POST", "/api/files/token", C)
st, content = req("GET", f"/api/files/{p['collectionId']}/{p['id']}/{p['file']}?token={ftc['token']}", raw=True)
check("download: stranger refused", st in (403, 404), st)

st, r = req("POST", f"/api/pokehome/pokemon/{p['id']}/claimed", A)
check("claim: someone else's pokemon refused", st == 400, (st, r))
st, r = req("POST", f"/api/pokehome/pokemon/{p['id']}/claimed", B)
check("claim: owner claims", st == 200, (st, r))
st, r = req("GET", f"/api/collections/trade_pokemon/records/{p['id']}", B)
check("claim: record deleted", st == 404, (st, r))
st, r = req("POST", f"/api/pokehome/pokemon/{p['id']}/claimed", B)
check("claim: twice is harmless", st == 200, (st, r))
st, r = req("GET", f"/api/collections/trades/records/{tid}", A)
check("claim: trade kept with summary after claim", st == 200 and r["offeredSummary"]["species"] == 25 and r["offered"] == "", (st, r))
req("POST", f"/api/pokehome/pokemon/{mine_a[0]['id']}/claimed", A)

# --- gift
st, t = offer(A, b_id, "fp-gift", kind="GIFT")
check("gift: created as GIFT", st == 200 and t["kind"] == "GIFT", (st, t))
st, r = req("POST", f"/api/pokehome/trades/{t['id']}/counter", B, pk("fp-g2", 1, "B"), {"file": ("b.pk3", b"B")})
check("gift: cannot be countered", st == 400, (st, r))
st, t = req("POST", f"/api/pokehome/trades/{t['id']}/accept", B)
check("gift: receiver accepts OPEN -> DONE", st == 200 and t["status"] == "DONE", (st, t))
st, r = req("GET", f"/api/collections/trade_pokemon/records/{t['offered']}", B)
check("gift: receiver owns it to claim", st == 200 and r["owner"] == b_id and r["status"] == "TO_CLAIM", (st, r))

# --- decline
st, t = offer(A, b_id, "fp-decline")
st, t2 = req("POST", f"/api/pokehome/trades/{t['id']}/decline", B)
check("decline: -> DECLINED", st == 200 and t2["status"] == "DECLINED", (st, t2))
check("decline: closedBy = who declined", t2.get("closedBy") == b_id, t2)
st, r = req("GET", f"/api/collections/trade_pokemon/records/{t['offered']}", A)
check("decline: pokemon back to sender", st == 200 and r["owner"] == a_id and r["status"] == "TO_CLAIM", (st, r))
st, r = req("POST", f"/api/pokehome/trades/{t['id']}/accept", B)
check("decline: closed trade cannot be accepted", st == 400, (st, r))

# --- cancel after counter
st, t = offer(A, b_id, "fp-cancel")
st, t = req("POST", f"/api/pokehome/trades/{t['id']}/counter", B, pk("fp-cancel-b", 1, "BULBA"), {"file": ("b.pk3", b"B")})
st, r = req("POST", f"/api/pokehome/trades/{t['id']}/cancel", B)
check("cancel: receiver cannot cancel", st == 400, (st, r))
st, t2 = req("POST", f"/api/pokehome/trades/{t['id']}/cancel", A)
check("cancel: sender -> CANCELLED", st == 200 and t2["status"] == "CANCELLED", (st, t2))
check("cancel: closedBy = sender", t2.get("closedBy") == a_id, t2)
st, r1 = req("GET", f"/api/collections/trade_pokemon/records/{t['offered']}", A)
st, r2 = req("GET", f"/api/collections/trade_pokemon/records/{t['countered']}", B)
check("cancel: both pokemon back to their owners", r1["owner"] == a_id and r2["owner"] == b_id and r1["status"] == r2["status"] == "TO_CLAIM", (r1, r2))

# --- expiry (cron run on demand)
st, t = offer(A, c_id, "fp-expire")
req("PATCH", f"/api/collections/trades/records/{t['id']}", admin, {"expiresAt": "2020-01-01 00:00:00.000Z"})
st, r = req("POST", "/api/crons/pokehome_trades_expire", admin)
check("expiry: cron runs", st in (200, 204), (st, r))
time.sleep(1)
st, t2 = req("GET", f"/api/collections/trades/records/{t['id']}", A)
check("expiry: old trade -> EXPIRED", t2["status"] == "EXPIRED", t2)
st, r = req("GET", f"/api/collections/trade_pokemon/records/{t['offered']}", A)
check("expiry: pokemon back to sender", r["owner"] == a_id and r["status"] == "TO_CLAIM", r)

# --- backups prune (32 uploads -> 30 kept)
for i in range(32):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as z:
        z.writestr("n.txt", str(i))
    data = buf.getvalue()
    sha = hashlib.sha256(data).hexdigest()
    st, r = req("POST", "/api/collections/vault_backups/records", A, {"owner": a_id, "sha256": sha, "device": "PC"}, {"file": (f"b{i}.zip", data)})
    if st != 200:
        check("prune: upload", False, (i, st, r)); break
    time.sleep(0.3)   # stay under the rate limiter (create: 20 per 5 s)
st, r = req("GET", "/api/collections/vault_backups/records?perPage=50", A)
check("prune: 30 newest kept", r["totalItems"] == 30, r["totalItems"])

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed")
