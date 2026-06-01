#!/usr/bin/env python3
"""Maintains versions.json and the root redirect for the versioned docs site.

Each documentation build calls this against the checked-out gh-pages tree to
register (or refresh) one version slug, then rewrites the root index.html so the
site root always redirects to the current default (the latest release, falling
back to "main" until the first release exists).
"""
import argparse
import json
import os
import re


def parse_version(slug):
    match = re.match(r"^(\d+)\.(\d+)\.(\d+)", slug)
    if not match:
        return (0, 0, 0)
    return tuple(int(part) for part in match.groups())


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
    parser.add_argument("--make-default", default="false")
    args = parser.parse_args()

    make_default = str(args.make_default).strip().lower() == "true"
    manifest_path = os.path.join(args.root, "versions.json")
    manifest = load_manifest(manifest_path)

    slug = args.slug
    prerelease = slug == "main" or "-" in slug

    entry = next((v for v in manifest["versions"] if v["slug"] == slug), None)
    if entry is None:
        manifest["versions"].append({"slug": slug, "prerelease": prerelease})
    else:
        entry["prerelease"] = prerelease

    if make_default:
        manifest["default"] = slug

    manifest["versions"].sort(key=order_key)

    with open(manifest_path, "w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)
        handle.write("\n")

    write_redirect(args.root, manifest["default"] or "main")


if __name__ == "__main__":
    main()
