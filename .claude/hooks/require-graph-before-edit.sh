#!/usr/bin/env bash
# PreToolUse hook: block Edit/Write on Mogul/**/*.cs until at least one
# code-review-graph tool has been called this session.
#
# Reason: structural changes to project source need caller verification BEFORE
# the edit, not after. The graph answers "who calls this" cheaper than reading
# files. This hook makes the missing-graph case visible instead of silent.
#
# Bypass: any single mcp__code-review-graph__* call this session satisfies the
# gate. Trivial edits (typo, comment) — call list_graph_stats_tool to clear it.

set -euo pipefail

payload=$(cat)

path=$(printf '%s' "$payload" | jq -r '.tool_input.file_path // ""' 2>/dev/null || true)
transcript=$(printf '%s' "$payload" | jq -r '.transcript_path // ""' 2>/dev/null || true)

# Only enforce on project C# source under Mogul/.
case "$path" in
  */Mogul/Mogul/*.cs|*/Mogul/Mogul/**/*.cs) ;;
  *) exit 0 ;;
esac

# No transcript → can't verify, allow.
[[ -z "$transcript" || ! -f "$transcript" ]] && exit 0

# Look for any code-review-graph tool use in the session transcript.
if grep -q '"name":"mcp__code-review-graph__' "$transcript" 2>/dev/null; then
  exit 0
fi

cat >&2 <<'EOF'
Blocked: no code-review-graph call this session.

Before editing project source, run a graph query to verify callers and impact:
  - mcp__code-review-graph__semantic_search_nodes_tool  (find by name)
  - mcp__code-review-graph__query_graph_tool            (callers_of / callees_of / imports_of)
  - mcp__code-review-graph__get_impact_radius_tool      (blast radius of a change)
  - mcp__code-review-graph__detect_changes_tool         (review the diff)

For trivial edits (typo, comment), call list_graph_stats_tool once to clear
this gate. The point is to make the skip visible, not impossible.
EOF
exit 2
