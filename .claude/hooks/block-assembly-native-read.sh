#!/usr/bin/env bash
# PreToolUse hook: block native Read on assembly/ paths and nudge to ctx_read.
# Reason: assembly/ files are huge decompiled IL2CPP. Native Read burns tokens.
# Project files are fine with native Read — this only fires for assembly/.

set -euo pipefail

# Read the tool-call payload from stdin.
payload=$(cat)

# Extract the file_path; bail cleanly if jq is missing or the field is absent.
path=$(printf '%s' "$payload" | jq -r '.tool_input.file_path // ""' 2>/dev/null || true)

if [[ "$path" == */assembly/* ]]; then
  cat >&2 <<'EOF'
Blocked: assembly/ files are huge decompiled IL2CPP. Use lean-ctx instead:
  - mcp__lean-ctx__ctx_read with mode=signatures (API surface only)
  - mcp__lean-ctx__ctx_read with mode=aggressive (max compression)
  - mcp__lean-ctx__ctx_read with mode=lines:N-M (specific range)
  - mcp__lean-ctx__ctx_search for grep-style lookups
For multi-file assembly lookups, spawn the lookup-assembly skill (Haiku subagent).
EOF
  exit 2
fi

exit 0
