#!/usr/bin/env bash
# Formats (fix) or verifies (check) the C#, C and C++ sources.
#   scripts/format.sh fix|check [files...]   files are relative to the repository root
#   scripts/format.sh clang-format-version
# Without files, every tracked source is processed. Shared by .githooks/pre-commit and CI, so both
# format the same way. FORMAT_ROOT points it at a directory that is not the git checkout.
set -euo pipefail

# clang-format output changes between releases, so CI installs exactly this version.
# It is the one bundled with Visual Studio 2026.
CLANG_FORMAT_VERSION=22.1.3

mode=${1:-}
[ $# -gt 0 ] && shift
case "$mode" in
    fix | check) ;;
    clang-format-version)
        echo "$CLANG_FORMAT_VERSION"
        exit 0
        ;;
    *)
        echo "usage: $0 fix|check [files...]" >&2
        exit 2
        ;;
esac

cd "${FORMAT_ROOT:-$(git rev-parse --show-toplevel)}"

files=()
if [ $# -gt 0 ]; then
    files=("$@")
else
    while IFS= read -r f; do files+=("$f"); done < <(git ls-files)
fi

cs=()
cpp=()
for f in "${files[@]}"; do
    case "$f" in
        *.cs) cs+=("$f") ;;
        *.c | *.cpp | *.h) cpp+=("$f") ;;
    esac
done

find_clang_format() {
    if [ -n "${CLANG_FORMAT:-}" ]; then
        echo "$CLANG_FORMAT"
    elif command -v clang-format >/dev/null; then
        echo clang-format
    else
        local vswhere="/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" path
        [ -x "$vswhere" ] || return 1
        path=$("$vswhere" -latest -products '*' -find 'VC\Tools\Llvm\x64\bin\clang-format.exe' | tr -d '\r')
        [ -n "$path" ] || return 1
        cygpath -u "$path"
    fi
}

status=0

# The tools' own output is too noisy to act on, so check mode keeps it in a log and ends with a summary.
log=
if [ "$mode" = check ]; then
    log=$(mktemp)
    trap 'rm -f "$log"' EXIT
fi

run() {
    if [ -n "$log" ]; then
        "$@" >>"$log" 2>&1 || status=1
    else
        "$@" || status=1
    fi
}

report_failure() {
    local root p count bad=() unique=()
    root=$(cygpath -m "$PWD" 2>/dev/null || echo "$PWD")
    while IFS= read -r p; do
        p=${p//\\//}
        bad+=("${p#"$root"/}")
    done < <(sed -nE 's/^(.*\.(cs|c|cpp|h))(:[0-9]+:[0-9]+|\([0-9]+,[0-9]+\)): (error|warning)[ :].*/\1/p' "$log")
    if ((${#bad[@]})); then
        while IFS= read -r p; do unique+=("$p"); done < <(printf '%s\n' "${bad[@]}" | sort -u)
    fi
    count=${#unique[@]}

    if ((count)); then
        echo "Formatting check failed, ${count} file(s) need changes:"
        printf '  %s\n' "${unique[@]}"
    else
        echo "Formatting check failed."
    fi
    echo
    echo "To fix it, run this in your clone and commit the result:"
    echo "    bash scripts/format.sh fix"

    if [ -n "${GITHUB_ACTIONS:-}" ]; then
        echo "::error title=Formatting::${count} file(s) need formatting. Run 'bash scripts/format.sh fix' locally and commit the result."
        if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
            {
                echo "### Formatting check failed"
                echo
                for p in ${unique[@]+"${unique[@]}"}; do echo "- \`$p\`"; done
                echo
                echo 'Run `bash scripts/format.sh fix` locally and commit the result.'
            } >>"$GITHUB_STEP_SUMMARY"
        fi
        if ((count)); then
            echo "::group::Details"
            cat "$log"
            echo "::endgroup::"
        else
            cat "$log"
        fi
    elif ((count == 0)) || [ -n "${FORMAT_VERBOSE:-}" ]; then
        echo
        cat "$log"
    else
        echo "(FORMAT_VERBOSE=1 shows the output of the formatters themselves.)"
    fi
}

if ((${#cpp[@]})); then
    clang_format=$(find_clang_format) || {
        echo "clang-format not found: put it on PATH or set CLANG_FORMAT" >&2
        exit 1
    }
    actual=$("$clang_format" --version | grep -oE '[0-9]+\.[0-9]+\.[0-9]+' | head -n 1)
    if [ "$actual" != "$CLANG_FORMAT_VERSION" ]; then
        echo "warning: clang-format $actual found, $CLANG_FORMAT_VERSION expected; the result may differ from CI" >&2
    fi
    if [ "$mode" = fix ]; then
        run "$clang_format" -i --style=file "${cpp[@]}"
    else
        run "$clang_format" --dry-run --Werror --style=file "${cpp[@]}"
    fi
fi

# The braces rule (IDE0011) needs the whole project, so the hook, which formats a copy of the staged files only, skips it.
if ((${#cs[@]})) && [ -z "${FORMAT_ROOT:-}" ]; then
    while IFS= read -r project; do
        project_files=()
        for f in "${cs[@]}"; do
            case "$f" in
                "$(dirname "$project")"/*) project_files+=("$f") ;;
            esac
        done
        ((${#project_files[@]})) || continue
        args=(style "$project" --diagnostics IDE0011 --severity warn --include "${project_files[@]}")
        if [ "$mode" = check ]; then
            args+=(--verify-no-changes)
        fi
        run dotnet format "${args[@]}"
    done < <(git ls-files '*.csproj')
fi

if ((${#cs[@]})); then
    args=(whitespace . --folder --include "${cs[@]}")
    if [ "$mode" = check ]; then
        args+=(--verify-no-changes)
    fi
    run dotnet format "${args[@]}"
fi

if [ "$mode" = check ] && [ "$status" -ne 0 ]; then
    report_failure
fi

exit $status
