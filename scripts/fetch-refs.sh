#!/usr/bin/env bash
# Download BepInEx, optional Jotunn, and Valheim dedicated-server assemblies into .refs/.
# Game binaries are not committed. VALHEIM_INSTALL overrides the Steam download.
set -euo pipefail

# Per-mod settings. The rest of this script is the same in every Valheim mod.
MOD_NAME=EvuMiscCompat
FETCH_JOTUNN=1
DELETE_SERVER_INSTALL=0
# An empty VALHEIM_DLLS copies every Managed *.dll.
VALHEIM_DLLS=()
REQUIRED_DLLS=(
  assembly_valheim.dll
  SoftReferenceableAssets.dll
  UnityEngine.CoreModule.dll
)

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REFS="$ROOT/.refs"
CACHE="$REFS/cache"
BEPINEX_VERSION="$(tr -d '[:space:]' < "$ROOT/deps/bepinex-pack.version")"
APP_ID="896660"
mkdir -p "$REFS/BepInEx" "$REFS/Valheim" "$CACHE"
if [[ "$FETCH_JOTUNN" == 1 ]]; then
  JOTUNN_VERSION="$(tr -d '[:space:]' < "$ROOT/deps/jotunn.version")"
  mkdir -p "$REFS/Jotunn"
fi

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
    curl -fL --retry 3 -A "$MOD_NAME" -o "$zip" \
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
    curl -fL --retry 3 -A "$MOD_NAME" -o "$zip" \
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

installed_buildid() {
  local manifest="$1/steamapps/appmanifest_${APP_ID}.acf"
  if [[ ! -f "$manifest" ]]; then
    return 1
  fi
  awk -F'"' '/"buildid"/ { print $4; exit }' "$manifest"
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

valheim_refs_present() {
  local required
  [[ -f "$REFS/valheim.buildid" ]] || return 1
  for required in "${REQUIRED_DLLS[@]}"; do
    [[ -f "$REFS/Valheim/$required" ]] || return 1
  done
}

copy_valheim() {
  local managed="$1"
  local buildid="$2"
  if [[ ${#VALHEIM_DLLS[@]} -eq 0 ]]; then
    find "$managed" -maxdepth 1 -type f -name '*.dll' -exec cp -f {} "$REFS/Valheim/" \;
  else
    local name
    for name in "${VALHEIM_DLLS[@]}"; do
      if [[ -f "$managed/$name" ]]; then
        cp -f "$managed/$name" "$REFS/Valheim/$name"
      fi
    done
  fi
  local required
  for required in "${REQUIRED_DLLS[@]}"; do
    require_file "$REFS/Valheim/$required"
  done
  echo "$buildid" > "$REFS/valheim.buildid"
}

fetch_valheim() {
  if [[ -n "${VALHEIM_INSTALL:-}" ]]; then
    local managed
    if ! managed="$(find_managed "$VALHEIM_INSTALL")"; then
      echo "fetch-refs: no assembly_valheim.dll under VALHEIM_INSTALL=${VALHEIM_INSTALL}" >&2
      exit 1
    fi
    local buildid="local"
    if [[ -f "$ROOT/.valheim-buildid" ]]; then
      buildid="$(tr -d '[:space:]' < "$ROOT/.valheim-buildid")"
    fi
    copy_valheim "$managed" "$buildid"
    return
  fi

  local buildid
  if ! buildid="$(current_buildid)"; then
    if valheim_refs_present; then
      echo "fetch-refs: could not read the current Valheim build id; keeping build $(<"$REFS/valheim.buildid") already in .refs" >&2
      return
    fi
    echo "fetch-refs: could not read the current Valheim build id and .refs has no Valheim assemblies" >&2
    exit 1
  fi
  if valheim_refs_present && [[ "$(<"$REFS/valheim.buildid")" == "$buildid" ]]; then
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

  # steamcmd follows ~/.steam/steam into the desktop client and rewrites
  # libraryfolders.vdf, which drops /mnt/spel/SteamLibrary. A private home
  # keeps that rewrite off the desktop client.
  mkdir -p "$CACHE/steamcmd-home"
  run_steamcmd() {
    env HOME="$CACHE/steamcmd-home" "$steamcmd" "$@"
  }

  # A cold SteamCMD exits 8 with "Missing configuration" until it has signed
  # in once and filled its app cache. Prime the login, then retry the update.
  set +e
  run_steamcmd +login anonymous +quit
  set -e

  local status=0
  local attempt managed installed
  for attempt in 1 2 3; do
    set +e
    run_steamcmd +force_install_dir "$install" +login anonymous +app_update "$APP_ID" validate +quit
    status=$?
    set -e
    installed="$(installed_buildid "$install" || true)"
    if [[ "$installed" == "$buildid" ]] && managed="$(find_managed "$install")"; then
      copy_valheim "$managed" "$buildid"
      if [[ "$DELETE_SERVER_INSTALL" == 1 ]]; then
        # Only the Managed DLLs are needed from here on. Dropping the server
        # install keeps the .refs cache small; a new build id downloads again.
        rm -rf "$install"
      fi
      return
    fi
    echo "fetch-refs: steamcmd attempt ${attempt} exited ${status}; installed build '${installed:-none}', wanted ${buildid}" >&2
    # Steam stops issuing request codes for a retired manifest. An old
    # appmanifest makes the update ask for that manifest, get Access Denied,
    # and leave the previous build in place.
    if [[ -n "$installed" && "$installed" != "$buildid" ]]; then
      mv -f "$install/steamapps/appmanifest_${APP_ID}.acf" \
        "$install/steamapps/appmanifest_${APP_ID}.acf.stale"
    fi
    if [[ "$attempt" -lt 3 ]]; then
      sleep 3
    fi
  done

  echo "fetch-refs: steamcmd exit ${status}; Valheim build ${buildid} was not installed under $install" >&2
  exit 1
}

fetch_bepinex
if [[ "$FETCH_JOTUNN" == 1 ]]; then
  fetch_jotunn
fi
fetch_valheim
if [[ "$FETCH_JOTUNN" == 1 ]]; then
  echo "fetch-refs: BepInEx $BEPINEX_VERSION, Jotunn $JOTUNN_VERSION, Valheim build $(<"$REFS/valheim.buildid")"
else
  echo "fetch-refs: BepInEx $BEPINEX_VERSION, Valheim build $(<"$REFS/valheim.buildid")"
fi
