#!/usr/bin/env python3
"""Bumps the Major.Minor base version in version.json without committing.

Only the "version" field is touched; every other nbgv setting (such as
versionHeightOffset and the release config) is preserved verbatim. The new
version is written to the GITHUB_OUTPUT "version" key for the calling workflow.
"""
import json
import os
import sys

VERSION_FILE = "version.json"


def main():
    bump_type = sys.argv[1] if len(sys.argv) > 1 else "minor"

    with open(VERSION_FILE, encoding="utf-8") as handle:
        data = json.load(handle)

    raw = str(data.get("version", "0.0"))
    core = raw.split("-", 1)[0]
    parts = core.split(".")
    major = int(parts[0]) if len(parts) > 0 and parts[0] else 0
    minor = int(parts[1]) if len(parts) > 1 and parts[1] else 0

    if bump_type == "major":
        major += 1
        minor = 0
    else:
        minor += 1

    new_version = f"{major}.{minor}"
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
