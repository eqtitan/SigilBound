#!/usr/bin/env bash
# Screenshots of the Unity build's main screens, taken from the Linux preview build that CI
# publishes to the "unity-preview" release, run headless under xvfb with software OpenGL.
# Usage: tools/unity-shots.sh [out-dir] [width] [height]
#   out-dir defaults to ./unity-shots; size defaults to 1280x720 (landscape).
# Needs: curl, unzip, xvfb-run (and Mesa's software GL, which xvfb uses).
# Prints the PNG paths at the end. See docs/sigilbound/PLAN.md (step 3) and Client/ShotsMode.cs.
set -euo pipefail
OUT="$(realpath -m "${1:-unity-shots}")"
W="${2:-1280}"; H="${3:-720}"
URL="https://github.com/eqtitan/SigilBound/releases/download/unity-preview/Sigilbound-Linux.zip"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "== downloading the Linux preview"
curl -fsSL -o "$WORK/preview.zip" "$URL"
unzip -q "$WORK/preview.zip" -d "$WORK"
BIN="$(find "$WORK" -name 'Sigilbound.x86_64' | head -n 1)"
[ -n "$BIN" ] || { echo "no Sigilbound.x86_64 in the zip"; exit 1; }
chmod +x "$BIN"

rm -rf "$OUT" && mkdir -p "$OUT"
echo "== running the screenshot mode (${W}x${H})"
# Software GL (llvmpipe) is slow but enough for screenshots. -force-glcore avoids Vulkan.
LIBGL_ALWAYS_SOFTWARE=1 timeout 600 xvfb-run -a -s "-screen 0 ${W}x${H}x24" \
  "$BIN" -screen-fullscreen 0 -screen-width "$W" -screen-height "$H" -force-glcore \
  -logFile "$OUT/player.log" -sbShots="$OUT" || true

if grep -q "SBSHOTS DONE" "$OUT/player.log"; then
  echo "== done: $(grep -o 'SBSHOTS DONE [0-9]*' "$OUT/player.log")"
else
  echo "!! the screenshot mode did not finish; see $OUT/player.log"
  grep -iE "error|exception|SBSHOTS" "$OUT/player.log" | head -40 || true
fi
ls -1 "$OUT"/*.png 2>/dev/null || true
