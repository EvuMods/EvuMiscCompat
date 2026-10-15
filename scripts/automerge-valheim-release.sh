#!/usr/bin/env bash
# Merge a release-please PR when every commit since the last release is a Valheim rebuild.
# Then dispatch release-please again so the merge publishes the GitHub release.
set -euo pipefail

if [[ -z "${PR_JSON:-}" || "${PR_JSON}" == "null" ]]; then
  echo "No release pull request to consider."
  exit 0
fi

number="$(printf '%s' "$PR_JSON" | jq -r '.number')"
if [[ -z "$number" || "$number" == "null" ]]; then
  echo "Release pull request payload had no number."
  exit 0
fi

if ! last="$(git describe --tags --abbrev=0 2>/dev/null)"; then
  echo "No tag yet. The first release stays manual."
  exit 0
fi

# The rebuild pull request lands with a merge commit, which is not a release note. Only look at real commits.
mapfile -t subjects < <(git log --no-merges "${last}..HEAD" --format=%s)
if [[ ${#subjects[@]} -eq 0 ]]; then
  echo "No commits since ${last}."
  exit 0
fi

for subject in "${subjects[@]}"; do
  if [[ ! "$subject" =~ ^fix:\ rebuild\ against\ Valheim\ [0-9]+$ ]]; then
    echo "Leaving the release pull request open: ${subject}"
    exit 0
  fi
done

echo "Merging release pull request #${number}."
gh pr merge "$number" --merge

for _ in 1 2 3 4 5 6 7 8 9 10; do
  state="$(gh pr view "$number" --json state --jq .state)"
  if [[ "$state" == "MERGED" ]]; then
    break
  fi
  sleep 2
done

gh workflow run release-please.yml --ref main
echo "Dispatched release-please to publish the merged release."
