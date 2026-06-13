#!/usr/bin/env python3
"""Bumps the pinned Major.Minor.Patch version in version.json without committing.

Only the "version" field is touched; every other nbgv setting (the release
config and cloud-build options) is preserved verbatim. The version is written as
a full three-segment value so the released NuGet package version is exactly that
value -- nbgv puts the git height in the unused fourth integer, which never
appears in the package version. The new version is also written to the
GITHUB_OUTPUT "version" key for the calling workflow.

Bump types:
    major   1.4.2 -> 2.0.0
    minor   1.4.2 -> 1.5.0
    patch   1.4.2 -> 1.4.3
"""
import json
import os
import sys

VERSION_FILE = "version.json"
VALID_BUMPS = ("major", "minor", "patch")


def next_version(raw, bump_type):
    core = raw.split("-", 1)[0]
    parts = core.split(".")
    major = int(parts[0]) if len(parts) > 0 and parts[0] else 0
    minor = int(parts[1]) if len(parts) > 1 and parts[1] else 0
    patch = int(parts[2]) if len(parts) > 2 and parts[2] else 0

    if bump_type == "major":
        return f"{major + 1}.0.0"
    if bump_type == "minor":
        return f"{major}.{minor + 1}.0"
    return f"{major}.{minor}.{patch + 1}"


def main():
    bump_type = sys.argv[1] if len(sys.argv) > 1 else "minor"
    if bump_type not in VALID_BUMPS:
        sys.exit(
            f"Unknown bump type '{bump_type}'; expected one of {', '.join(VALID_BUMPS)}."
        )

    with open(VERSION_FILE, encoding="utf-8") as handle:
        data = json.load(handle)

    raw = str(data.get("version", "0.0.0"))
    new_version = next_version(raw, bump_type)
    data["version"] = new_version

    with open(VERSION_FILE, "w", encoding="utf-8") as handle:
        json.dump(data, handle, indent=2)
        handle.write("\n")

    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as handle:
            handle.write(f"version={new_version}\n")

    print(f"Bumped version: {raw} -> {new_version}")


if __name__ == "__main__":
    main()
