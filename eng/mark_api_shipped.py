#!/usr/bin/env python3
"""Promotes unshipped public API entries into the shipped surface.

The Microsoft.CodeAnalysis.PublicApiAnalyzers tracks each assembly's public
surface across a pair of files: PublicAPI.Shipped.txt (released APIs) and
PublicAPI.Unshipped.txt (APIs added or changed since the last release). When a
release is cut, the unshipped entries become shipped.

For every PublicAPI.Unshipped.txt under the search root this moves its entries
into the sibling PublicAPI.Shipped.txt and resets the unshipped file to the
"#nullable enable" header alone. Entries carrying the analyzer's "*REMOVED*"
marker delete the named API from the shipped file instead of being appended.
Existing line order is preserved (the analyzer emits entries in declaration
order, not sorted), so promotion adds no reordering churn. The count of
promoted files is written to the GITHUB_OUTPUT "promoted" key for the caller.

When --version is given, this also bumps Directory.Build.props'
PackageValidationBaselineVersion to that version, so the next pack validates
against the release that just shipped instead of going stale.
"""
import argparse
import glob
import os
import re
import sys

NULLABLE_HEADER = "#nullable enable"
REMOVED_PREFIX = "*REMOVED*"
UNSHIPPED_NAME = "PublicAPI.Unshipped.txt"
SHIPPED_NAME = "PublicAPI.Shipped.txt"
BASELINE_FILE = "Directory.Build.props"
BASELINE_PATTERN = re.compile(
    r"(<PackageValidationBaselineVersion>)[^<]*(</PackageValidationBaselineVersion>)"
)


def read_entries(path):
    if not os.path.exists(path):
        return False, []
    # utf-8-sig strips a leading BOM so the header compares equal to
    # NULLABLE_HEADER; plain utf-8 leaves the BOM on the first line, which would
    # both defeat header detection and leak a bogus "﻿#nullable enable"
    # entry into the shipped surface. Writes stay BOM-free (utf-8).
    with open(path, encoding="utf-8-sig") as handle:
        lines = [line.rstrip("\n").rstrip("\r") for line in handle]
    has_header = any(line == NULLABLE_HEADER for line in lines)
    entries = [line for line in lines if line and line != NULLABLE_HEADER]
    return has_header, entries


def write_api_file(path, has_header, entries):
    body = ([NULLABLE_HEADER] if has_header else []) + entries
    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write("\n".join(body) + "\n")


def merge_shipped(shipped_entries, unshipped_entries):
    removed = {
        entry[len(REMOVED_PREFIX):]
        for entry in unshipped_entries
        if entry.startswith(REMOVED_PREFIX)
    }
    additions = [entry for entry in unshipped_entries if not entry.startswith(REMOVED_PREFIX)]

    merged = [entry for entry in shipped_entries if entry not in removed]
    seen = set(merged)
    for entry in additions:
        if entry not in seen:
            merged.append(entry)
            seen.add(entry)

    missing = removed - set(shipped_entries)
    return merged, missing


def promote(unshipped_path):
    shipped_path = os.path.join(os.path.dirname(unshipped_path), SHIPPED_NAME)

    unshipped_header, unshipped_entries = read_entries(unshipped_path)
    if not unshipped_entries:
        return False

    shipped_header, shipped_entries = read_entries(shipped_path)
    merged, missing = merge_shipped(shipped_entries, unshipped_entries)

    for api in sorted(missing):
        print(f"::warning::{shipped_path}: *REMOVED* entry not found in shipped surface: {api}")

    write_api_file(shipped_path, shipped_header or unshipped_header, merged)
    write_api_file(unshipped_path, True, [])
    print(f"Promoted {len(unshipped_entries)} entries: {unshipped_path} -> {shipped_path}")
    return True


def bump_validation_baseline(version, path=BASELINE_FILE):
    with open(path, encoding="utf-8") as handle:
        content = handle.read()

    updated, count = BASELINE_PATTERN.subn(rf"\g<1>{version}\g<2>", content)
    if count == 0:
        print(f"::warning::{path}: no <PackageValidationBaselineVersion> element found; skipped.")
        return False

    with open(path, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(updated)
    print(f"Bumped package validation baseline: {path} -> {version}")
    return True


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default="src", help="Directory to scan (default: src)")
    parser.add_argument("--version", help="Version just released; also bumps PackageValidationBaselineVersion")
    args = parser.parse_args()

    pattern = os.path.join(args.root, "**", UNSHIPPED_NAME)
    promoted = sum(
        1 for path in sorted(glob.glob(pattern, recursive=True)) if promote(path)
    )

    print(f"Promoted public API in {promoted} project(s).")

    if args.version:
        bump_validation_baseline(args.version)

    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as handle:
            handle.write(f"promoted={promoted}\n")

    return 0


if __name__ == "__main__":
    sys.exit(main())
