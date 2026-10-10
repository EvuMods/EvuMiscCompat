#!/usr/bin/env bash
# Commit the new Valheim build id, merge it, and ask release-please to cut a patch release.
set -euo pipefail

: "${BUILD_ID:?BUILD_ID is required}"

branch="ci/valheim-${BUILD_ID}"
message="fix: rebuild against Valheim ${BUILD_ID}"

existing="$(gh pr list --head "$branch" --state open --json number --jq '.[0].number')"
if [[ -n "$existing" ]]; then
  echo "Pull request #${existing} is already open for ${branch}."
  exit 0
fi

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

if git ls-remote --exit-code --heads origin "$branch" >/dev/null 2>&1; then
  git push origin --delete "$branch"
fi

git checkout -B "$branch"
printf '%s\n' "$BUILD_ID" > .valheim-buildid
git add .valheim-buildid
git commit -m "$message"
git push -u origin "HEAD:${branch}"

gh pr create --title "$message" --body "$(cat <<EOF
Verified \`make verify\` against Valheim dedicated-server build \`${BUILD_ID}\`.

A green compile means the referenced members and the core tests still hold. It does not play the game.

EOF
)"
number="$(gh pr view --json number --jq .number)"
echo "Created pull request #${number}"

merged=0
for _ in 1 2 3 4 5 6 7 8 9 10; do
  if gh pr merge "$number" --merge; then
    merged=1
    break
  fi
  sleep 3
done
if [[ "$merged" -ne 1 ]]; then
  echo "Could not merge pull request #${number}." >&2
  exit 1
fi

gh workflow run release-please.yml --ref main
echo "Dispatched release-please."
