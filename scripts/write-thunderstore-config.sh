#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME_CATEGORY="${1:-deep-north-update}"
VERSION="$(tr -d '[:space:]' < "$ROOT/version.txt")"
OUT="$ROOT/dist/thunderstore.toml"

if ! [[ "$GAME_CATEGORY" =~ ^[a-z0-9]+(-[a-z0-9]+)*$ ]]; then
  echo "thunderstore: category slug must look like deep-north-update, got: $GAME_CATEGORY" >&2
  exit 1
fi

mkdir -p "$ROOT/dist"
python3 - "$ROOT/manifest.json" "$OUT" "$VERSION" "$GAME_CATEGORY" <<'PY'
import json
import sys
from pathlib import Path

manifest_path, dest, version, game_category = sys.argv[1:]
manifest = json.loads(Path(manifest_path).read_text(encoding="utf-8"))
categories = ["mods", "ai-generated", "client-side", "server-side", game_category]
seen = []
for category in categories:
    if category not in seen:
        seen.append(category)
quoted = ", ".join(f'"{category}"' for category in seen)
text = f"""[config]
schemaVersion = "0.0.1"

[package]
namespace = "EvuMods"
name = "{manifest["name"]}"
versionNumber = "{version}"
description = "{manifest["description"]}"
websiteUrl = "{manifest["website_url"]}"
containsNsfwContent = false

[publish]
repository = "https://thunderstore.io"
communities = ["valheim"]

[publish.categories]
valheim = [{quoted}]
"""
Path(dest).write_text(text, encoding="utf-8")
PY
echo "thunderstore: $OUT"
