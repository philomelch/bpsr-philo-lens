#!/usr/bin/env bash
# Coding-standards gate for this plugin. Runs in CI, and on every .cs edit via the Stellar
# workspace's Claude hook.
#
# Adapted from StellarResonanceModSystem's tools/check-standards.sh (AGPL-3.0), with the
# framework-only rules dropped and the layer rule rewritten for a plugin's folder layout.
#
#   Rule                                                        Severity
#   > 800 lines per file                                        blocker
#   > 500 lines per file                                        major
#   block-scoped namespace (must be file-scoped)                blocker
#   Domain/ or Application/ referencing Stellar.*, BepInEx,
#       HarmonyLib, UnityEngine, Il2Cpp or Panda                blocker
#   path-form GameObject.Find("a/b") (full scene scan)          blocker
#   'Manager' type-name suffix                                  minor
#   interface missing the 'I' prefix                            minor
#   possible public field (use a property)                      minor
#
# NOT checked here (keep them by hand; see the workspace CLAUDE.md): method length (≤ 50 / 100
# lines), ≤ 5 parameters, ≤ 6 constructor dependencies, ≤ 8 interface members.
#
# Usage:
#   bash tools/check-standards.sh            # scan every .cs under src/ and tools/
#   bash tools/check-standards.sh FILE...    # scan only these files (others are ignored)
#
# Exit code: number of blockers + majors (0 = pass). Minors are reported but don't fail.

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
blockers=0
majors=0
minors=0

files=()
if [ "$#" -gt 0 ]; then
    for arg in "$@"; do
        arg="${arg//\\//}"
        [[ "$arg" == /* || "$arg" =~ ^[A-Za-z]: ]] || arg="$ROOT/$arg"
        if [[ -f "$arg" && "$arg" == *.cs ]]; then
            # Compare against ROOT on the resolved path so Windows drive-letter forms match.
            abs="$(cd "$(dirname "$arg")" && pwd)/$(basename "$arg")"
            [[ "$abs" == "$ROOT"/src/* || "$abs" == "$ROOT"/tools/* ]] && files+=("$abs")
        fi
    done
    [ "${#files[@]}" -eq 0 ] && exit 0
else
    while IFS= read -r f; do files+=("$f"); done \
        < <(find "$ROOT/src" "$ROOT/tools" -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*')
fi

report() {
    case "$1" in
        blocker) echo "BLOCKER: $2 — $3"; blockers=$((blockers + 1)) ;;
        major)   echo "major:   $2 — $3"; majors=$((majors + 1)) ;;
        minor)   echo "minor:   $2 — $3"; minors=$((minors + 1)) ;;
    esac
}

for f in "${files[@]}"; do
    rel="${f#"$ROOT"/}"

    lines=$(wc -l < "$f")
    if [ "$lines" -gt 800 ]; then
        report blocker "$rel" "$lines lines (> 800); split it"
    elif [ "$lines" -gt 500 ]; then
        report major "$rel" "$lines lines (> 500); split it"
    fi

    # File-scoped ends in ';'. A '{' on the same line, or nothing (brace on the next line), is block-scoped.
    if grep -qE '^namespace [A-Za-z0-9_.]+[[:space:]]*(\{|$)' "$f"; then
        report blocker "$rel" "block-scoped namespace; use file-scoped"
    fi

    # Layer rule: the inner layers stay pure so they can be unit-tested without the game.
    if [[ "$rel" == src/*/Domain/* || "$rel" == src/*/Application/* ]]; then
        while IFS= read -r hit; do
            [ -n "$hit" ] && report blocker "$rel" "inner layer references the framework or game: $hit"
        done < <(grep -nE '\b(Stellar\.(Abstractions|PluginContracts)|BepInEx|HarmonyLib|UnityEngine|Il2Cpp|Panda)\b' "$f" || true)
    fi

    if grep -qE 'GameObject\.Find\("[^"]*/' "$f"; then
        report blocker "$rel" "path-form GameObject.Find scans the whole scene; cache a root and use Transform.Find"
    fi

    if grep -qE '\b(class|interface|struct|record)[[:space:]]+[A-Za-z_][A-Za-z0-9_]*Manager\b' "$f"; then
        report minor "$rel" "'Manager' type suffix; use Service / Host / Coordinator / Registry / Repository / Factory"
    fi

    if grep -qE '^[[:space:]]*(public|internal)[[:space:]]+(partial[[:space:]]+)?interface[[:space:]]+[A-HJ-Z]' "$f"; then
        report minor "$rel" "interface missing the 'I' prefix"
    fi

    if grep -nE '^[[:space:]]*public[[:space:]]+[A-Za-z_][A-Za-z0-9_<>,?\[\]]*[[:space:]]+[A-Za-z_][A-Za-z0-9_]*[[:space:]]*;' "$f" \
        | grep -vqE '(class|interface|struct|enum|event|delegate|const|readonly|=>|=)'; then
        report minor "$rel" "possible public field; use a property"
    fi
done

echo "check-standards: $blockers blocker(s), $majors major(s), $minors minor(s)"
exit $((blockers + majors))
