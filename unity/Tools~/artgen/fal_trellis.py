"""Image -> GLB with fal-ai/trellis ($0.02 per model) through fal's queue API.

    python3 fal_trellis.py IMAGE OUT.glb [--seed N] [--texture 2048]

FAL_KEY comes from the environment, a .env next to this script, or (normally) the network secret saved on the
cloud environment for queue.fal.run, which the proxy injects. It is never printed.
The request id is kept in OUT.glb.request so an unclear submit is not repeated (a second
submit is a new billable job): rerunning resumes polling that request.
"""
import argparse, base64, json, mimetypes, os, sys, time, urllib.request, urllib.error
from urllib.parse import urlparse

ENDPOINT = "fal-ai/trellis"
QUEUE = "https://queue.fal.run"

def key():
    k = os.environ.get("FAL_KEY")
    if not k:
        env = os.path.join(os.path.dirname(os.path.abspath(__file__)), ".env")
        if os.path.exists(env):
            for line in open(env):
                if line.startswith("FAL_KEY="):
                    k = line.split("=", 1)[1].strip().strip("'\"")
    # No local key: the session's network proxy injects the saved fal.ai secret (Authorization: Key ...)
    # into requests to queue.fal.run, replacing this placeholder. Never print or commit a real key.
    return k or "proxy-injected"

def call(url, body=None, method=None):
    if urlparse(url).hostname not in ("queue.fal.run", "fal.run"): sys.exit("refusing non-fal url " + url)
    h = {"Authorization": "Key " + key(), "Content-Type": "application/json"}
    r = urllib.request.Request(url, json.dumps(body).encode() if body is not None else None, h, method=method)
    try: return json.load(urllib.request.urlopen(r, timeout=120))
    except urllib.error.HTTPError as e:
        msg = e.read().decode()[:600]
        if e.code == 403: sys.exit("fal 403 (do not retry): " + msg)
        raise SystemExit(f"fal HTTP {e.code}: {msg}")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("image"); ap.add_argument("out")
    ap.add_argument("--seed", type=int); ap.add_argument("--texture", default="2048", choices=["512", "1024", "2048"])
    a = ap.parse_args()
    rid_file = a.out + ".request"
    if os.path.exists(rid_file):
        sub = json.load(open(rid_file)); print("resuming", sub["request_id"])
    else:
        mime = mimetypes.guess_type(a.image)[0] or "image/png"
        uri = f"data:{mime};base64," + base64.b64encode(open(a.image, "rb").read()).decode()
        args = {"image_url": uri, "texture_size": int(a.texture), "mesh_simplify": 0.95}
        if a.seed is not None: args["seed"] = a.seed
        sub = call(f"{QUEUE}/{ENDPOINT}", args)
        json.dump({k: sub.get(k) for k in ("request_id", "status_url", "response_url")}, open(rid_file, "w"))
        print("submitted", sub["request_id"])
    t0 = time.time()
    while True:
        st = call(sub["status_url"])
        if st.get("status") == "COMPLETED": break
        if time.time() - t0 > 900: sys.exit("still " + str(st.get("status")) + "; rerun to keep polling")
        time.sleep(4)
    res = call(sub["response_url"])
    url = res["model_mesh"]["url"]
    with urllib.request.urlopen(url, timeout=300) as f, open(a.out, "wb") as o: o.write(f.read())
    os.remove(rid_file)
    print("saved", a.out, os.path.getsize(a.out), "bytes; request", sub["request_id"], "timings", res.get("timings"))

if __name__ == "__main__":
    main()
