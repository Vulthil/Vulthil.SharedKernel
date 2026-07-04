#!/usr/bin/env python3
"""Maintains versions.json and the root redirect for the versioned docs site.

Each documentation build calls this against the checked-out gh-pages tree to
register (or refresh) one version slug, then rewrites the root index.html so the
site root always redirects to the current default (the latest release, falling
back to "main" until the first release exists).

Registering a stable slug also prunes every other stable entry that shares its
(major, minor) line -- both the versions.json entry and its directory under
--root -- so the selector only ever lists the latest patch per minor release.
"main" and prerelease slugs are never pruned and never trigger pruning.

--make-default is still accepted, since docs.yml always passes it, but is
otherwise ignored: the default is always recomputed as the highest stable
version present after pruning, falling back to "main" when none exists. This
stops a servicing release (e.g. 1.0.3) from stealing the site default away
from a newer minor line (e.g. 1.1.0).
"""
import argparse
import json
import os
import re
import shutil


def parse_version(slug):
    match = re.match(r"^(\d+)\.(\d+)\.(\d+)", slug)
    if not match:
        return (0, 0, 0)
    return tuple(int(part) for part in match.groups())


def is_prerelease(slug):
    return slug == "main" or "-" in slug


def prune_superseded_patches(manifest, slug, root):
    major, minor, _ = parse_version(slug)
    survivors = []
    for entry in manifest["versions"]:
        other_slug = entry["slug"]
        if other_slug == slug or entry["prerelease"]:
            survivors.append(entry)
            continue
        if parse_version(other_slug)[:2] == (major, minor):
            shutil.rmtree(os.path.join(root, other_slug), ignore_errors=True)
            continue
        survivors.append(entry)
    manifest["versions"] = survivors


def compute_default(manifest):
    stable_slugs = [v["slug"] for v in manifest["versions"] if not v["prerelease"]]
    if not stable_slugs:
        return "main"
    return max(stable_slugs, key=parse_version)


def order_key(entry):
    slug = entry["slug"]
    if slug == "main":
        return (0, 0, 0, 0)
    major, minor, patch = parse_version(slug)
    return (1, -major, -minor, -patch)


def load_manifest(path):
    if os.path.exists(path):
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
        data.setdefault("versions", [])
        data.setdefault("default", None)
        return data
    return {"default": None, "versions": []}


def write_redirect(root, target):
    html = (
        "<!doctype html>\n"
        '<html lang="en">\n'
        "<head>\n"
        '  <meta charset="utf-8">\n'
        "  <title>Vulthil.SharedKernel</title>\n"
        f'  <meta http-equiv="refresh" content="0; url=./{target}/">\n'
        f'  <link rel="canonical" href="./{target}/">\n'
        "</head>\n"
        "<body>\n"
        f'  <p>Redirecting to <a href="./{target}/">./{target}/</a>&hellip;</p>\n'
        "</body>\n"
        "</html>\n"
    )
    with open(os.path.join(root, "index.html"), "w", encoding="utf-8") as handle:
        handle.write(html)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--slug", required=True)
    parser.add_argument(
        "--make-default",
        default="false",
        help="Accepted for compatibility with docs.yml; ignored. The default "
        "is always the highest stable version present after pruning.",
    )
    args = parser.parse_args()

    manifest_path = os.path.join(args.root, "versions.json")
    manifest = load_manifest(manifest_path)

    slug = args.slug
    prerelease = is_prerelease(slug)

    entry = next((v for v in manifest["versions"] if v["slug"] == slug), None)
    if entry is None:
        manifest["versions"].append({"slug": slug, "prerelease": prerelease})
    else:
        entry["prerelease"] = prerelease

    if not prerelease:
        prune_superseded_patches(manifest, slug, args.root)

    manifest["versions"].sort(key=order_key)
    manifest["default"] = compute_default(manifest)

    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)
        handle.write("\n")

    write_redirect(args.root, manifest["default"])


if __name__ == "__main__":
    main()
