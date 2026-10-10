#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="$(tr -d '[:space:]' < "$ROOT/version.txt")"

python_bin() {
  if command -v python3 >/dev/null 2>&1; then
    echo python3
  else
    echo python
  fi
}

DLL="$ROOT/src/EvuMiscCompat/bin/Release/EvuMiscCompat.dll"
CORE="$ROOT/src/EvuMiscCompat/bin/Release/EvuMiscCompat.Core.dll"
STAGE="$ROOT/dist/EvuMiscCompat"
ZIP="$ROOT/dist/EvuMiscCompat-${VERSION}.zip"

if [[ ! -f "$DLL" || ! -f "$CORE" ]]; then
  echo "package: missing plugin output. Run make build first." >&2
  exit 1
fi

for required in "$ROOT/manifest.json" "$ROOT/icon.png" "$ROOT/README.md" "$ROOT/CHANGELOG.md"; do
  if [[ ! -f "$required" ]]; then
    echo "package: missing $required" >&2
    exit 1
  fi
done

"$(python_bin)" - "$ROOT/icon.png" <<'PY'
import struct
import sys
from pathlib import Path

data = Path(sys.argv[1]).read_bytes()
if data[12:16] != b"IHDR":
    raise SystemExit("package: icon.png is not a PNG")
width, height = struct.unpack(">II", data[16:24])
if (width, height) != (256, 256):
    raise SystemExit(f"package: icon.png must be 256x256, got {width}x{height}")
PY

rm -rf "$STAGE" "$ZIP"
mkdir -p "$STAGE"
cp "$DLL" "$CORE" "$ROOT/icon.png" "$ROOT/README.md" "$ROOT/CHANGELOG.md" "$STAGE/"

"$(python_bin)" - "$ROOT/manifest.json" "$STAGE/manifest.json" "$VERSION" <<'PY'
import json
import sys
from pathlib import Path

source, dest, version = sys.argv[1:]
manifest = json.loads(Path(source).read_text(encoding="utf-8"))
manifest["version_number"] = version
Path(dest).write_text(json.dumps(manifest, indent=4) + "\n", encoding="utf-8")
PY

"$(python_bin)" - "$STAGE" "$ZIP" <<'PY'
import sys
import zipfile
from pathlib import Path

stage = Path(sys.argv[1])
archive = Path(sys.argv[2])
with zipfile.ZipFile(archive, "w", compression=zipfile.ZIP_DEFLATED) as zf:
    for path in sorted(stage.rglob("*")):
        if path.is_file():
            zf.write(path, path.relative_to(stage).as_posix())
PY
echo "package: $ZIP"
