#!/usr/bin/env bash
# Pass/fail check of a screenshot tour (tools/unity-shots.sh, or the CI smoke run in
# .github/workflows/sigilbound-unity.yml). Usage: tools/unity-smoke-check.sh DIR
# DIR holds the tour's shots.log and player.log. Passes when:
#   - shots.log has the "DONE <n>" line (the tour reached its end),
#   - the battle-mid and titan-* SBBUDGET lines are there and each reports skinned>0
#     (hero models actually loaded and animated, not stand-ins),
#   - the Titan's pose is sane in every Titan shot (SBBOUNDS titan-*)
#   - every gallery hero's pose is sane: shots.log has SBBOUNDS lines and none says BAD (a body flung
#     into the sky or smeared into a sheet still counts as a "real model" with skinned>0; build 180),
#   - player.log has no NullReferenceException / MissingReferenceException /
#     IndexOutOfRangeException whose message or stack trace is in Sigilbound code.
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
  for want in battle titan; do
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
  # The Titan's pose in its shots (build 190: limbs stretched into sheets): one SBBOUNDS titan-* line per Titan shot.
  nt=$(grep -c '^SBBOUNDS titan-' "$SHOTS" || true)
  if [ "${nt:-0}" -eq 0 ]; then bad "no SBBOUNDS titan-* lines in shots.log (Titan pose check did not run)"
  else echo "ok: $nt Titan shots pose-checked (SBBOUNDS titan-*; any BAD fails above)"; fi
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

if [ "$fail" -eq 0 ]; then echo "SMOKE PASSED"; else echo "SMOKE FAILED"; fi
exit "$fail"
