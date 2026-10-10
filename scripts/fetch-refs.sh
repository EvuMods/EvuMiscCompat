#!/usr/bin/env bash
# Download BepInEx, Jotunn, and Valheim dedicated-server assemblies into .refs/.
# Game binaries are not committed. VALHEIM_INSTALL overrides the Steam download.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REFS="$ROOT/.refs"
CACHE="$REFS/cache"
BEPINEX_VERSION="$(tr -d '[:space:]' < "$ROOT/deps/bepinex-pack.version")"
JOTUNN_VERSION="$(tr -d '[:space:]' < "$ROOT/deps/jotunn.version")"
APP_ID="896660"
mkdir -p "$REFS/BepInEx" "$REFS/Valheim" "$REFS/Jotunn" "$CACHE"

require_file() {
  if [[ ! -f "$1" ]]; then
    echo "fetch-refs: missing $1" >&2
    exit 1
  fi
}

fetch_bepinex() {
  if [[ -n "${BEPINEX_CORE:-}" && -f "${BEPINEX_CORE}/BepInEx.dll" ]]; then
    cp -f "${BEPINEX_CORE}/BepInEx.dll" "${BEPINEX_CORE}/0Harmony.dll" "$REFS/BepInEx/"
    echo "$BEPINEX_VERSION" > "$REFS/bepinex.version"
    return
  fi

  if [[ -f "$REFS/BepInEx/BepInEx.dll" && "$(cat "$REFS/bepinex.version" 2>/dev/null || true)" == "$BEPINEX_VERSION" ]]; then
    return
  fi

  local zip="$CACHE/BepInExPack_Valheim-${BEPINEX_VERSION}.zip"
  if [[ ! -f "$zip" ]]; then
    curl -fL --retry 3 -A "EvuMiscCompat" -o "$zip" \
      "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/${BEPINEX_VERSION}/"
  fi
  rm -rf "$CACHE/bepinex-pack"
  unzip -q "$zip" -d "$CACHE/bepinex-pack"
  local core
  core="$(find "$CACHE/bepinex-pack" -type d -name core | head -1)"
  require_file "$core/BepInEx.dll"
  require_file "$core/0Harmony.dll"
  cp -f "$core/BepInEx.dll" "$core/0Harmony.dll" "$REFS/BepInEx/"
  echo "$BEPINEX_VERSION" > "$REFS/bepinex.version"
}

fetch_jotunn() {
  if [[ -f "$REFS/Jotunn/Jotunn.dll" && "$(cat "$REFS/jotunn.version" 2>/dev/null || true)" == "$JOTUNN_VERSION" ]]; then
    return
  fi

  local zip="$CACHE/Jotunn-${JOTUNN_VERSION}.zip"
  if [[ ! -f "$zip" ]]; then
    curl -fL --retry 3 -A "EvuMiscCompat" -o "$zip" \
      "https://thunderstore.io/package/download/ValheimModding/Jotunn/${JOTUNN_VERSION}/"
  fi
  # Thunderstore zips may use backslashes, which unzip rejects.
  "$(python_bin)" - "$zip" "$REFS/Jotunn/Jotunn.dll" <<'PY'
import sys
import zipfile
from pathlib import Path

archive, dest = sys.argv[1:]
with zipfile.ZipFile(archive) as zf:
    name = next(info.filename for info in zf.infolist() if info.filename.replace("\\", "/").endswith("Jotunn.dll"))
    Path(dest).write_bytes(zf.read(name))
PY
  require_file "$REFS/Jotunn/Jotunn.dll"
  echo "$JOTUNN_VERSION" > "$REFS/jotunn.version"
}

python_bin() {
  if command -v python3 >/dev/null 2>&1; then
    echo python3
  else
    echo python
  fi
}

current_buildid() {
  curl -fsSL "https://api.steamcmd.net/v1/info/${APP_ID}" \
    | "$(python_bin)" -c "import json,sys; d=json.load(sys.stdin); print(d['data']['${APP_ID}']['depots']['branches']['public']['buildid'])"
}

find_managed() {
  local root="$1"
  local candidate
  for candidate in \
    "$root/valheim_Data/Managed" \
    "$root/Valheim_Data/Managed" \
    "$root/valheim_server_Data/Managed" \
    "$root/Valheim_server_Data/Managed"
  do
    if [[ -f "$candidate/assembly_valheim.dll" ]]; then
      echo "$candidate"
      return 0
    fi
  done
  local found
  found="$(find "$root" -type f -name assembly_valheim.dll 2>/dev/null | head -1 || true)"
  if [[ -n "$found" ]]; then
    dirname "$found"
    return 0
  fi
  return 1
}

copy_valheim() {
  local managed="$1"
  local buildid="$2"
  find "$managed" -maxdepth 1 -type f -name '*.dll' -exec cp -f {} "$REFS/Valheim/" \;
  require_file "$REFS/Valheim/assembly_valheim.dll"
  require_file "$REFS/Valheim/UnityEngine.CoreModule.dll"
  echo "$buildid" > "$REFS/valheim.buildid"
}

fetch_valheim() {
  if [[ -n "${VALHEIM_INSTALL:-}" ]]; then
    local managed
    managed="$(find_managed "$VALHEIM_INSTALL")"
    local buildid="local"
    if [[ -f "$ROOT/.valheim-buildid" ]]; then
      buildid="$(tr -d '[:space:]' < "$ROOT/.valheim-buildid")"
    fi
    copy_valheim "$managed" "$buildid"
    return
  fi

  local buildid
  buildid="$(current_buildid)"
  if [[ -f "$REFS/Valheim/assembly_valheim.dll" && "$(cat "$REFS/valheim.buildid" 2>/dev/null || true)" == "$buildid" ]]; then
    return
  fi

  local install="$CACHE/valheim-server"
  mkdir -p "$install"
  local steamcmd="steamcmd"
  if ! command -v steamcmd >/dev/null 2>&1; then
    if [[ ! -x "$CACHE/steamcmd/steamcmd.sh" ]]; then
      mkdir -p "$CACHE/steamcmd"
      curl -fsSL "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" \
        | tar -xz -C "$CACHE/steamcmd"
    fi
    steamcmd="$CACHE/steamcmd/steamcmd.sh"
  fi

  # A fresh SteamCMD often exits 8 with "Missing configuration" on the first
  # app_update. The same command succeeds once the client has updated itself.
  local attempt status managed
  status=1
  for attempt in 1 2 3; do
    set +e
    "$steamcmd" +force_install_dir "$install" +login anonymous +app_update "$APP_ID" validate +quit
    status=$?
    set -e
    if managed="$(find_managed "$install")"; then
      copy_valheim "$managed" "$buildid"
      return
    fi
    echo "fetch-refs: steamcmd attempt ${attempt} exited ${status}; assembly_valheim.dll was not found under ${install}" >&2
    sleep 5
  done
  exit 1
}

fetch_bepinex
fetch_jotunn
fetch_valheim
echo "fetch-refs: BepInEx $BEPINEX_VERSION, Jotunn $JOTUNN_VERSION, Valheim build $(cat "$REFS/valheim.buildid")"
