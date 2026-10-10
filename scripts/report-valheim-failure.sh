#!/usr/bin/env bash
set -euo pipefail

: "${BUILD_ID:?BUILD_ID is required}"

title="EvuMiscCompat failed to build against Valheim ${BUILD_ID}"
log=""
if [[ -f verify.log ]]; then
  log="$(tail -n 200 verify.log)"
fi

body="$(cat <<EOF
\`make verify\` failed for Valheim dedicated-server build \`${BUILD_ID}\`. No release was published.

\`\`\`
${log}
\`\`\`
EOF
)"

number="$(gh issue list --state open --limit 50 --json number,title --jq ".[] | select(.title == \"${title}\") | .number" | head -n 1)"
if [[ -n "$number" ]]; then
  gh issue comment "$number" --body "$body"
  echo "Updated issue #${number}."
else
  gh issue create --title "$title" --body "$body"
fi
