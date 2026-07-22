#!/usr/bin/env python3
"""Prune-only refresh of CompatibilitySuppressions.xml against the current baseline.

Package validation (APICompat) diffs each package against two things: the
published nupkg pinned by Directory.Build.props' PackageValidationBaselineVersion,
and (for multi-targeted packages) the package's own net9.0 vs. net10.0 surface.
Both kinds of findings that a maintainer has accepted live side by side in the
same per-project CompatibilitySuppressions.xml, distinguished only by whether
IsBaselineSuppression is true.

Every time PackageValidationBaselineVersion is bumped -- see
eng/mark_api_shipped.py and eng/propagate_baseline.py, both of which bump it
in a working tree they are about to open a PR from -- some baseline-suppressed
entries can go stale: the API they suppressed a diagnostic for is no longer
different from the new baseline. On this SDK, an unnecessary suppression is
not a warning, it is a hard `dotnet pack` error ("Unnecessary suppressions
found"). Left alone, that error would surface on the *next* release's pack,
attributed to whatever innocent commit happened to trigger it. This script
fixes that by pruning stale entries in the same commit that bumps the
baseline, right after the working tree already reflects the new version.

Safety property (non-negotiable): this refresh may only REMOVE suppression
entries, never add one. `-p:ApiCompatGenerateSuppressionFile=true` regenerates
a project's suppression file from scratch -- it prunes entries no longer
needed, but it will just as happily ADD an entry for a genuine, previously
unsuppressed API break (e.g. a real regression that landed after the release
this baseline now points at). Blindly writing that regenerated file back would
silently bless a breaking change. So every project's regenerated entry set is
compared against its current file first; only if the regenerated set is a
subset of the current one (pure removal) is anything written. If regeneration
would add even one entry anywhere, NOTHING is written and the script fails
loudly -- that case needs a human suppression decision.

Mechanics, verified empirically against the 10.0.302 SDK:
  - `ApiCompatSuppressionOutputFile` (Microsoft.NET.ApiCompat.Common.targets)
    redirects where a regenerated file is WRITTEN, independently of
    `ApiCompatSuppressionFile`, which controls what is READ for comparison and
    still defaults to the project's own CompatibilitySuppressions.xml. Passing
    just `-p:ApiCompatSuppressionOutputFile=<scratch path>` therefore
    regenerates against the real, on-disk suppression file while never writing
    to it -- the original is untouched unless and until this script itself
    decides to copy a vetted result over it. This is used instead of a
    snapshot/regenerate-in-place/restore dance: with a redirect there is
    nothing to restore, because nothing real was ever written to in the first
    place. (A single global override can't be used for a solution-wide pack --
    every project would collide on the same output path -- so each project
    with a suppression file is packed individually, which also keeps this
    targeted rather than paying for a full-solution pack.)
  - When the regenerated set is empty, the tool still writes a file, just with
    a self-closing `<Suppressions ... />` root and no children.
  - Each project's own obj/ is deleted before its regeneration pack.
    RunPackageValidation is an incremental MSBuild target keyed off a semaphore
    file under obj/; without this, a second pack in the same tree can skip
    revalidation entirely and silently report the prior result.

Cost of a rewrite (only paid on a real change, never on a no-op): the
regenerated file the SDK writes carries only its own boilerplate header
comment, not whatever hand-written justification comment a maintainer had
added above the suppression list. That context is lost from the file on a
prune and needs to live in the PR description or commit history instead.
"""
import argparse
import glob
import os
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET

SUPPRESSIONS_FILENAME = "CompatibilitySuppressions.xml"


def discover_suppression_files(root):
    pattern = os.path.join(root, "**", SUPPRESSIONS_FILENAME)
    return sorted(glob.glob(pattern, recursive=True))


def find_project_file(project_dir):
    candidates = sorted(glob.glob(os.path.join(project_dir, "*.csproj")))
    if len(candidates) != 1:
        raise RuntimeError(
            f"{project_dir}: expected exactly one .csproj next to {SUPPRESSIONS_FILENAME}, "
            f"found {len(candidates)}."
        )
    return candidates[0]


def _element_text(suppression, tag):
    element = suppression.find(tag)
    return element.text.strip() if element is not None and element.text else ""


def parse_entries(path):
    """Parses a suppression file into a set of (DiagnosticId, Target, Left,
    Right, IsBaselineSuppression) tuples -- the same identity ApiCompat itself
    uses to match a diagnostic to a suppression. Comparing this tuple set
    instead of raw XML text is what lets a file that lost its hand-written
    comments, or whose entries came out in a different order, still compare
    equal to what was there before.
    """
    if not os.path.exists(path):
        return frozenset()

    root = ET.parse(path).getroot()
    entries = set()
    for suppression in root.findall("Suppression"):
        entries.add((
            _element_text(suppression, "DiagnosticId"),
            _element_text(suppression, "Target"),
            _element_text(suppression, "Left"),
            _element_text(suppression, "Right"),
            _element_text(suppression, "IsBaselineSuppression").lower() == "true",
        ))
    return frozenset(entries)


def clean_build_output(project_dir):
    for name in ("obj", "bin"):
        path = os.path.join(project_dir, name)
        if os.path.isdir(path):
            shutil.rmtree(path)


def regenerate(csproj_path, configuration, work_dir):
    """Packs a single project with suppression regeneration redirected into
    work_dir, and returns the resulting entry set. Never writes to the
    project's real CompatibilitySuppressions.xml.
    """
    project_dir = os.path.dirname(csproj_path)
    clean_build_output(project_dir)

    output_file = os.path.join(work_dir, "regenerated.xml")
    pack_out_dir = os.path.join(work_dir, "pack-out")
    command = [
        "dotnet", "pack", csproj_path,
        "-c", configuration,
        "-p:ApiCompatGenerateSuppressionFile=true",
        f"-p:ApiCompatSuppressionOutputFile={output_file}",
        "-o", pack_out_dir,
    ]
    result = subprocess.run(command, capture_output=True, text=True, check=False)
    if result.stdout:
        print(result.stdout)
    if result.stderr:
        print(result.stderr, file=sys.stderr)
    if result.returncode != 0:
        raise RuntimeError(
            f"dotnet pack failed while regenerating suppressions for {csproj_path} "
            f"(exit code {result.returncode}). This is a build/restore problem, not a "
            f"suppression finding -- see the captured output above."
        )

    return parse_entries(output_file)


def plan_refresh(suppression_path, original_entries, regenerated_entries):
    """Decides the action for one suppression file. Returns (action, added)
    where action is one of "noop", "delete", "prune", or "violation".
    """
    added = regenerated_entries - original_entries
    if added:
        return "violation", added

    removed = original_entries - regenerated_entries
    if not removed:
        return "noop", frozenset()
    if not regenerated_entries:
        return "delete", frozenset()
    return "prune", frozenset()


def format_entry(entry):
    diagnostic_id, target, left, right, is_baseline = entry
    return f"{diagnostic_id} {target} (Left={left}, Right={right}, IsBaselineSuppression={is_baseline})"


def refresh(root, configuration):
    suppression_files = discover_suppression_files(root)
    if not suppression_files:
        print(f"No {SUPPRESSIONS_FILENAME} files found under {root}; nothing to refresh.")
        return True, False

    violations = []
    actions = []

    with tempfile.TemporaryDirectory(prefix="apicompat-suppression-refresh-") as tmp_root:
        for index, suppression_path in enumerate(suppression_files):
            project_dir = os.path.dirname(suppression_path)
            csproj_path = find_project_file(project_dir)
            original_entries = parse_entries(suppression_path)

            print(f"Regenerating suppressions for {csproj_path}...")
            work_dir = os.path.join(tmp_root, str(index))
            os.makedirs(work_dir, exist_ok=True)
            regenerated_entries = regenerate(csproj_path, configuration, work_dir)

            action, added = plan_refresh(suppression_path, original_entries, regenerated_entries)
            if action == "violation":
                violations.append((suppression_path, added))
            else:
                actions.append((action, suppression_path, os.path.join(work_dir, "regenerated.xml")))

        if violations:
            print(
                "::error::Regenerating one or more suppression files would ADD entries "
                "that are not present today. That means a real API break landed after "
                "the release this baseline now points at, and needs a human suppression "
                "decision -- auto-suppressing it would silently bless a breaking change. "
                "No suppression files were modified."
            )
            for suppression_path, added in violations:
                print(f"::error::{suppression_path}: would newly require {len(added)} suppression(s):")
                for entry in sorted(added):
                    print(f"::error::  {format_entry(entry)}")
            return False, False

        # Nothing tripped the invariant, so every real suppression file is
        # still exactly as it was on disk (regeneration above only ever wrote
        # into the temporary directory). This is the only point where a real
        # file is touched.
        changed = False
        for action, suppression_path, regenerated_file in actions:
            if action == "noop":
                print(f"Up to date, no changes: {suppression_path}")
                continue
            changed = True
            if action == "delete":
                os.remove(suppression_path)
                print(f"Deleted (no suppressions still needed): {suppression_path}")
            elif action == "prune":
                shutil.copyfile(regenerated_file, suppression_path)
                print(f"Pruned stale suppressions: {suppression_path}")

    return True, changed


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", default="src", help="Directory to scan for CompatibilitySuppressions.xml (default: src)")
    parser.add_argument("--configuration", default="Release", help="Build configuration to pack with (default: Release)")
    args = parser.parse_args()

    try:
        ok, changed = refresh(args.root, args.configuration)
    except RuntimeError as error:
        print(f"::error::{error}")
        ok, changed = False, False

    output = os.environ.get("GITHUB_OUTPUT")
    if output:
        with open(output, "a", encoding="utf-8") as handle:
            handle.write(f"changed={'true' if changed else 'false'}\n")

    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
