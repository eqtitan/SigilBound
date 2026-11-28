#!/usr/bin/env bash
# Pass/fail check of a screenshot tour (tools/unity-shots.sh, or the CI smoke run in
# .github/workflows/sigilbound-unity.yml). Usage: tools/unity-smoke-check.sh DIR
# DIR holds the tour's shots.log and player.log. Passes when:
#   - shots.log has the "DONE <n>" line (the tour reached its end),
#   - the battle-mid and titan-* SBBUDGET lines are there and each reports skinned>0
#     (hero models actually loaded and animated, not stand-ins); with the scene tour (-sbShotsScenes,
#     shots.log says "scenes on") also every scene-* line (47 stops: 15 chapters x 3, plus 2 Hard lairs),
#   - every gallery hero's pose is sane: shots.log has SBBOUNDS lines and none says BAD (a body flung
#     into the sky or smeared into a sheet still counts as a "real model" with skinned>0; build 180),
#   - player.log has no NullReferenceException / MissingReferenceException /
#     IndexOutOfRangeException whose message or stack trace is in Sigilbound code,
#   - the high-detail heroes' KTX2 maps loaded (SBPERF hd lines say "KTX2 maps", no
#     "KTX2 map not loaded" warning) and no LOD1 was refused by its mesh.
set -uo pipefail
DIR="${1:?usage: unity-smoke-check.sh DIR}"
SHOTS="$DIR/shots.log"; PLAYER="$DIR/player.log"
fail=0
bad() { echo "FAIL: $*"; fail=1; }

if [ ! -f "$SHOTS" ]; then
  bad "no shots.log (the tour never started)"
elif grep -q '^DONE ' "$SHOTS"; then
  echo "ok: $(grep '^DONE ' "$SHOTS" | tail -n 1)"
else
  bad "shots.log has no DONE line (the tour did not finish); last lines:"
  tail -n 15 "$SHOTS" | sed 's/^/    /'
fi

if [ -f "$SHOTS" ]; then
  wants="battle titan"
  if grep -q '^scenes on' "$SHOTS"; then
    wants="$wants scene"
    n=$(grep -cE '^SBBUDGET scene-' "$SHOTS" || true)
    if [ "$n" -lt 47 ]; then bad "scene tour: only $n of 47 scene-* SBBUDGET lines"; else echo "ok: scene tour has $n stops"; fi
  fi
  for want in $wants; do
    lines=$(grep -E "^SBBUDGET ${want}-" "$SHOTS" || true)
    if [ -z "$lines" ]; then bad "no SBBUDGET ${want}-* line in shots.log"; continue; fi
    while IFS= read -r l; do
      n=$(sed -nE 's/.* skinned=([0-9]+).*/\1/p' <<<"$l")
      if [ -z "$n" ] || [ "$n" -le 0 ]; then bad "$l"; else echo "ok: $l"; fi
    done <<<"$lines"
  done
fi

if [ -f "$SHOTS" ]; then
  nb=$(grep -c '^SBBOUNDS ' "$SHOTS" || true)
  badb=$(grep -E '^SBBOUNDS .* BAD' "$SHOTS" || true)
  if [ "${nb:-0}" -eq 0 ]; then bad "no SBBOUNDS lines in shots.log (gallery pose check did not run)"
  elif [ -n "$badb" ]; then bad "gallery heroes with broken poses:"; sed 's/^/    /' <<<"$badb"
  else echo "ok: $nb gallery heroes have sane poses (SBBOUNDS)"; fi
fi

if [ ! -f "$PLAYER" ]; then
  bad "no player.log"
else
  # An exception block is the "XxxException: ..." line plus the stack trace under it, up to the
  # blank line Unity writes after each log entry (at most 40 lines).
  hits=$(awk '
    function flush() { if (blk != "" && blk ~ /Sigilbound/) { print blk; print "" } blk = ""; inblk = 0 }
    !/^[ \t]/ && /(NullReference|MissingReference|IndexOutOfRange)Exception/ { flush(); blk = $0; inblk = 1; n = 1; next }
    inblk && /^[ \t]*$/ { flush(); next }
    inblk && n < 40 { blk = blk "\n" $0; n++; next }
    inblk { flush() }
    END { flush() }' "$PLAYER")
  if [ -n "$hits" ]; then
    bad "exceptions from Sigilbound code in player.log ($(grep -E '(NullReference|MissingReference|IndexOutOfRange)Exception' <<<"$hits" | grep -cv '^[[:space:]]') blocks; first ones below)"
    head -n 40 <<<"$hits" | sed 's/^/    /'
  else
    echo "ok: no Null/MissingReference or IndexOutOfRange exceptions from Sigilbound code in player.log"
  fi
fi

if [ -f "$PLAYER" ]; then
  ktx=$(grep -c 'SBPERF hd .*KTX2 maps' "$PLAYER" || true)
  jpg=$(grep -c 'SBPERF hd .*JPEG maps' "$PLAYER" || true)
  nok=$(grep -cE 'Sigilbound: KTX2 (map not loaded|transcoding failed)' "$PLAYER" || true)
  lodbad=$(grep -cE "Sigilbound: LOD1 of .*(doesn't match|not built|unreadable)" "$PLAYER" || true)
  lod=$(grep -c 'SBPERF hd .*LOD1 ' "$PLAYER" || true)
  if [ "$nok" -gt 0 ] || { [ "$jpg" -gt 0 ] && [ "$ktx" -eq 0 ]; }; then
    bad "hero KTX2 maps: $ktx loaded as KTX2, $jpg as JPEG, $nok load warnings"; grep -m 3 -E 'Sigilbound: KTX2' "$PLAYER" | sed 's/^/    /'
  else echo "ok: $ktx high-detail loads used KTX2 maps ($jpg JPEG)"; fi
  if [ "$lodbad" -gt 0 ]; then bad "hero LOD1: $lodbad refused"; grep -m 3 -E 'Sigilbound: LOD1 of' "$PLAYER" | sed 's/^/    /'
  else echo "ok: $lod high-detail loads used a LOD1 mesh, none refused"; fi
fi

if [ "$fail" -eq 0 ]; then echo "SMOKE PASSED"; else echo "SMOKE FAILED"; fi
exit "$fail"
