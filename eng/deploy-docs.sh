#!/usr/bin/env bash
# Publishes the freshly built DocFX site (docs/_site) to the gh-pages branch
# under a version slug, leaving every other version untouched. Stable releases
# also become the site default via the root redirect. Driven by docs.yml.
#
# Inputs (environment):
#   SLUG          path segment to publish under (e.g. "main" or "1.2.0")
#   MAKE_DEFAULT  "true" to point the site root at this slug
set -euo pipefail

SITE_DIR="docs/_site"
SLUG="${SLUG:?SLUG environment variable is required}"
MAKE_DEFAULT="${MAKE_DEFAULT:-false}"
WORKTREE="gh-pages-publish"

if [[ ! -d "$SITE_DIR" ]]; then
  echo "::error::DocFX output '$SITE_DIR' not found. Did the build step run?"
  exit 1
fi

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

if git ls-remote --exit-code --heads origin gh-pages >/dev/null 2>&1; then
  git fetch --no-tags origin "+refs/heads/gh-pages:refs/remotes/origin/gh-pages"
  git worktree add -B gh-pages "$WORKTREE" refs/remotes/origin/gh-pages
else
  git worktree add --detach "$WORKTREE"
  git -C "$WORKTREE" checkout --orphan gh-pages
  git -C "$WORKTREE" rm -rfq --ignore-unmatch . || true
fi

rm -rf "${WORKTREE:?}/${SLUG}"
mkdir -p "$WORKTREE/$SLUG"
cp -a "$SITE_DIR/." "$WORKTREE/$SLUG/"

touch "$WORKTREE/.nojekyll"

python3 eng/update_versions.py \
  --root "$WORKTREE" \
  --slug "$SLUG" \
  --make-default "$MAKE_DEFAULT"

git -C "$WORKTREE" add -A
if git -C "$WORKTREE" diff --cached --quiet; then
  echo "No documentation changes to publish for '$SLUG'."
else
  git -C "$WORKTREE" commit -m "docs: publish $SLUG"
  git -C "$WORKTREE" push origin gh-pages
fi

git worktree remove --force "$WORKTREE"
