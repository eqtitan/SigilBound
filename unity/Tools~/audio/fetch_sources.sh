#!/usr/bin/env bash
# Fetches the CC0 recordings make_real.py layers (see Assets/Resources/Audio/CREDITS.md) into
# $AUDIO_SRC (default ~/.cache/sigil-audio-src), pinned to the commits they were checked at.
# Only the folders used are checked out (git partial clone): about 25 MB.
set -euo pipefail
DEST="${AUDIO_SRC:-$HOME/.cache/sigil-audio-src}"
mkdir -p "$DEST"
get() { # name url commit path...
  local name="$1" url="$2" rev="$3"; shift 3
  if [ ! -d "$DEST/$name/.git" ]; then git clone -q --filter=blob:none --no-checkout "$url" "$DEST/$name"; fi
  git -C "$DEST/$name" fetch -q --depth 1 origin "$rev" 2>/dev/null || true
  git -C "$DEST/$name" checkout -q "$rev" -- "$@"
  echo "ok  $name @ ${rev:0:8}"
}
# Kenney "Impact Sounds" 1.0 (CC0), as shipped in the iree.gd sample project
get kenney-impact https://github.com/iree-gd/iree.gd 9db7d7069a9ad75c77e9b5657d1ae5bdf615a6bc sample/assets/audio/impact
# Kenney "RPG Audio" (CC0, License.txt inside)
get kenney-rpg https://github.com/Nazarwadim/School-Hooligan 940d8fa36fcf38350fbf69c6e760da69331b337c assets/kenney_rpgaudio
# Kenney Sci-fi Sounds selection (CC0), TIB Sound Library
get kenney-scifi https://github.com/ignition-bench/sound-library 2573312a2074289732fbafa2c50b502716000ec2 LICENSE sounds
# sfx-kit (CC0, physically modelled impacts, whooshes, booms)
get sfx-kit https://github.com/wyzowl/sfx-kit 92780e7ab0cdfb557497a988089c47f46e8c7412 LICENSE sounds/whooshes sounds/booms-and-bass sounds/cinematic-hits sounds/impacts sounds/reverse-whooshes
# CC0 field-recording beds (OpenGameArt / Wikimedia CC0 sources, listed in their CREDITS.md)
get cc0-ambient https://github.com/roxycon-dev/Ciallo-Reader a9c3baa4540457888824d46a67454c90a9be9e59 app/src/main/assets/ambient
echo "sources in $DEST"
