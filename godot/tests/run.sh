#!/usr/bin/env bash
# Runs the Godot demo's checks headless: the web-parity tests, then a scripted smoke run that
# plays the real game flow (title -> starter pick -> home -> battle 1-1 on auto -> result -> home)
# and exits with a status code.
# Usage: godot/tests/run.sh /path/to/godot-binary
# A step fails on a non-zero exit, a missing success line, or any GDScript error in its log.
set -uo pipefail
GODOT="${1:?usage: run.sh <godot binary>}"
cd "$(dirname "$0")/.."
status=0
logdir="$(mktemp -d)"

step() { # name, success-pattern, godot args...
  local name="$1" ok="$2"; shift 2
  local log="$logdir/${name// /_}.log"
  echo "== $name"
  timeout 900 "$GODOT" "$@" >"$log" 2>&1
  local code=$?
  grep -v '^$' "$log"
  if [ "$code" -ne 0 ]; then echo "!! $name exited with $code"; status=1; fi
  if grep -qE "SCRIPT ERROR|Parse Error|Compile Error" "$log"; then echo "!! $name logged a GDScript error"; status=1; fi
  if ! grep -qE "$ok" "$log"; then echo "!! $name did not report success"; status=1; fi
}

# Import once so class names and the models are registered.
timeout 900 "$GODOT" --headless --import --path . >"$logdir/import.log" 2>&1 || true

step "parity tests" "^[0-9]+ passed, 0 failed$" --headless --path . --script res://tests/run_tests.gd
step "smoke run" "^SMOKE OK" --headless --path . -- --smoke

[ "$status" -eq 0 ] && echo "All Godot checks passed." || echo "Godot checks FAILED."
exit $status
