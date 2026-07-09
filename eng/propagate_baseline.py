#!/usr/bin/env python3
"""Propagates a servicing release's baseline version upward to main.

After a servicing branch (e.g. v1.0) ships a patch, eng/mark_api_shipped.py
bumps that branch's own PackageValidationBaselineVersion. Main should also
move to the new version -- each branch validates its packages against the
newest release it must stay compatible with -- but nothing does that
automatically. This script is the piece that runs against a checkout of main
to decide whether such a bump is warranted.

The comparison is one-directional by construction: it only ever raises the
baseline, never lowers it. Calling this with an older-or-equal version is a
no-op, so pointing it at main from a servicing release is always safe even if
main has already moved past that version (e.g. via its own release).

Exit codes: 0 for both a bump and a no-op skip (older-or-equal), since neither
is an error the workflow should fail on -- the two are distinguished via the
GITHUB_OUTPUT "bumped" key. Exit 1 only when the baseline property itself
can't be found, which is a loud, workflow-failing configuration error rather
than a normal comparison outcome.
"""
import argparse
import os
import re
import sys

BASELINE_FILE = "Directory.Build.props"
BASELINE_PATTERN = re.compile(
    r"(<PackageValidationBaselineVersion>)([^<]*)(</PackageValidationBaselineVersion>)"
)


def parse_version(value):
    match = re.match(r"^(\d+)\.(\d+)\.(\d+)", value)
    if not match:
        return None
    return tuple(int(part) for part in match.groups())


def propagate(released_version, path=BASELINE_FILE):
    with open(path, encoding="utf-8") as handle:
        content = handle.read()

    match = BASELINE_PATTERN.search(content)
    if not match:
        print(f"::error::{path}: no <PackageValidationBaselineVersion> element found; cannot propagate.")
        return None

    current_version = match.group(2)
    current = parse_version(current_version)
    released = parse_version(released_version)
    if current is None or released is None:
        print(f"::error::{path}: could not parse version(s) for comparison (current='{current_version}', released='{released_version}').")
        return None

    if released <= current:
        print(
            f"Baseline unchanged: {path} is already {current_version}, "
            f"released version {released_version} is not newer. Skipping."
        )
        return False

    updated = BASELINE_PATTERN.sub(rf"\g<1>{released_version}\g<3>", content)
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(updated)
    print(f"Bumped package validation baseline: {path} {current_version} -> {released_version}")
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--released-version", required=True, help="Version that was just released")
    parser.add_argument("--path", default=BASELINE_FILE, help="Path to the Directory.Build.props to check (default: Directory.Build.props)")
    args = parser.parse_args()

    bumped = propagate(args.released_version, args.path)

    output = os.environ.get("GITHUB_OUTPUT")
    if bumped is not None and output:
        with open(output, "a", encoding="utf-8") as handle:
            handle.write(f"bumped={'true' if bumped else 'false'}\n")
            if bumped:
                handle.write(f"version={args.released_version}\n")

    return 0 if bumped is not None else 1


if __name__ == "__main__":
    sys.exit(main())
