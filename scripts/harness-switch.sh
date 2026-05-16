#!/usr/bin/env bash
# harness-switch.sh — nginx-style profile switch for the SplitBook harness.
#
# Both full harness sets live under harness/profiles/{backend,frontend}/.
# The paths opencode actually reads are SYMLINKS that this script repoints:
#
#   opencode.json                  -> harness/profiles/<p>/opencode.json
#   harness/README.md              -> profiles/<p>/README.md
#   harness/LESSONS.md             -> profiles/<p>/LESSONS.md
#   scripts/app.sh                 -> ../harness/profiles/<p>/app.sh
#   .opencode/agent/<name>.md      -> ../../harness/profiles/<p>/agent/<name>.md
#   .opencode/command/next-slice.md-> ../../harness/profiles/<p>/command/next-slice.md
#
# Symlinks are RELATIVE so the repo stays relocatable.
#
# Usage:
#   scripts/harness-switch.sh status
#   scripts/harness-switch.sh backend
#   scripts/harness-switch.sh frontend
#
# Safety: before clobbering a live path that is a REGULAR FILE (not yet a
# managed symlink), the script checks the file is byte-identical to at least
# one profile's copy. If it matches neither, it ABORTS and tells you to
# capture your local edits into a profile first — nothing is silently lost.

set -u
set -o pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "${REPO_ROOT}" || { echo "!! cannot cd to repo root" >&2; exit 1; }

PROFILES_DIR="harness/profiles"
AGENTS=(build spec-auditor test-writer reviewer researcher lessons-scribe)

die()  { echo "!! $*" >&2; exit 1; }
say()  { echo "-- $*"; }

# live_path -> "relative-symlink-target-suffix"
# (target is harness/profiles/<p>/<suffix-after-the-prefix>; prefix depends on
#  the live path's directory depth)
# link_one <profile> <live-path> <relative-symlink-target> <suffix-under-profile-dir>
link_one() {
  local profile="$1" live="$2" rel_target="$3" suffix="$4"

  if [[ -L "${live}" ]]; then
    :  # already a managed symlink — fine to repoint
  elif [[ -e "${live}" ]]; then
    # regular file: must be identical to SOME profile copy or we'd lose edits
    local matched="" p cand
    for p in backend frontend; do
      cand="${PROFILES_DIR}/${p}/${suffix}"
      if [[ -f "${cand}" ]] && cmp -s "${live}" "${cand}"; then matched="${p}"; break; fi
    done
    [[ -n "${matched}" ]] || die "refusing to overwrite '${live}': it is a regular file that matches NEITHER profile. Capture your edits into harness/profiles/<profile>/ first, then re-run."
  fi

  ln -sfn "${rel_target}" "${live}" || die "failed to link ${live}"
}

do_switch() {
  local profile="$1"
  [[ -d "${PROFILES_DIR}/${profile}" ]] || die "unknown profile '${profile}' (expected: backend | frontend)"

  # opencode.json  (live at repo root)
  link_one "${profile}" "opencode.json"                 "${PROFILES_DIR}/${profile}/opencode.json"        "opencode.json"
  # harness/README.md, harness/LESSONS.md  (live in harness/)
  link_one "${profile}" "harness/README.md"             "profiles/${profile}/README.md"                   "README.md"
  link_one "${profile}" "harness/LESSONS.md"            "profiles/${profile}/LESSONS.md"                  "LESSONS.md"
  # scripts/app.sh  (live in scripts/)
  link_one "${profile}" "scripts/app.sh"                "../harness/profiles/${profile}/app.sh"           "app.sh"
  # .opencode/agent/*.md
  local a
  for a in "${AGENTS[@]}"; do
    link_one "${profile}" ".opencode/agent/${a}.md"     "../../harness/profiles/${profile}/agent/${a}.md" "agent/${a}.md"
  done
  # .opencode/command/next-slice.md
  link_one "${profile}" ".opencode/command/next-slice.md" "../../harness/profiles/${profile}/command/next-slice.md" "command/next-slice.md"

  mkdir -p harness/logs/frontend

  echo "${profile}" > "${PROFILES_DIR}/.active"
  say "switched harness profile -> ${profile}"
  echo
  say "NOTE: specs/ are NOT managed by this switch (operator-owned, per the"
  say "      experiment's 'specs are fixed' rule). The frontend specs are"
  say "      currently in specs/{product,technical}-spec.md + slice-plan.md;"
  say "      the original backend specs are in specs/old_DO_NOT_READ/. If you"
  say "      switch back to 'backend' to actually run it, restore those first."
  echo
  cmd_status
}

resolve() {
  local live="$1"
  if [[ -L "${live}" ]]; then
    local t; t="$(readlink "${live}")"
    case "${t}" in
      *profiles/backend/*)  echo "backend" ;;
      *profiles/frontend/*) echo "frontend" ;;
      *)                    echo "?(${t})" ;;
    esac
  elif [[ -e "${live}" ]]; then
    echo "FILE(unmanaged)"
  else
    echo "MISSING"
  fi
}

cmd_status() {
  local active="(none)"
  [[ -f "${PROFILES_DIR}/.active" ]] && active="$(cat "${PROFILES_DIR}/.active")"
  say "active profile marker: ${active}"
  printf '   %-34s %s\n' "opencode.json"                  "$(resolve opencode.json)"
  printf '   %-34s %s\n' "harness/README.md"              "$(resolve harness/README.md)"
  printf '   %-34s %s\n' "harness/LESSONS.md"             "$(resolve harness/LESSONS.md)"
  printf '   %-34s %s\n' "scripts/app.sh"                 "$(resolve scripts/app.sh)"
  local a
  for a in "${AGENTS[@]}"; do
    printf '   %-34s %s\n' ".opencode/agent/${a}.md"      "$(resolve ".opencode/agent/${a}.md")"
  done
  printf '   %-34s %s\n' ".opencode/command/next-slice.md" "$(resolve .opencode/command/next-slice.md)"
}

case "${1:-}" in
  backend|frontend) do_switch "$1" ;;
  status|"")        cmd_status ;;
  *) die "usage: $0 {backend|frontend|status}" ;;
esac
