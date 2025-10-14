#!/usr/bin/env bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UMBRELLA_DIR="$(dirname "$SCRIPT_DIR")"
PARENT_DIR="$(dirname "$UMBRELLA_DIR")"
REPOS_FILE="$UMBRELLA_DIR/repos.json"

echo "Cloning all repositories..."

for row in $(jq -r '.services[] | @base64' "$REPOS_FILE"); do
    _jq() {
        echo "${row}" | base64 --decode | jq -r "${1}"
    }

    NAME=$(_jq '.name')
    REPO=$(_jq '.repo')
    BRANCH=$(_jq '.branch')

    TARGET_DIR="$PARENT_DIR/$NAME"

    if [ -d "$TARGET_DIR" ]; then
        echo "✓ $NAME already exists, pulling latest..."
        (cd "$TARGET_DIR" && git fetch && git checkout "$BRANCH" && git pull)
    else
        echo "→ Cloning $NAME..."
        git clone -b "$BRANCH" "$REPO" "$TARGET_DIR"
    fi
done

echo "✅ All repositories cloned/updated"
