"""Gemini image generation through Google's OpenAI-compatible images endpoint (~4 US cents an image).

    python3 gemini_image.py gemini-2.5-flash-image PROMPT_FILE OUT_PREFIX [n]   -> OUT_PREFIX_1.png ...

The Gemini API key is a network secret saved on the cloud environment for generativelanguage.googleapis.com;
the proxy injects it (Authorization: Bearer), replacing the placeholder below. Never paste a key here.
"""
import sys, json, base64, urllib.request
model, pf, out = sys.argv[1:4]; n = int(sys.argv[4]) if len(sys.argv) > 4 else 1
body = {"model": model, "prompt": open(pf).read(), "n": n, "response_format": "b64_json"}
r = urllib.request.Request("https://generativelanguage.googleapis.com/v1beta/openai/images/generations", json.dumps(body).encode(),
                           {"Content-Type": "application/json", "Authorization": "Bearer proxy-injected"})
try: d = json.load(urllib.request.urlopen(r, timeout=300))
except urllib.error.HTTPError as e: sys.exit(f"HTTP {e.code} {e.read().decode()[:600]}")
for i, im in enumerate(d.get("data", []), 1):
    open(f"{out}_{i}.png", "wb").write(base64.b64decode(im["b64_json"])); print("saved", f"{out}_{i}.png")
if not d.get("data"): print(json.dumps(d)[:600])
