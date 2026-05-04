# code-review-graph — Graph First

The repo has a knowledge graph (2200+ files, includes `assembly/`). It auto-updates on
Edit/Write/Bash via the PostToolUse hook. Use it BEFORE scanning files.

## Tool map

| Task | Tool |
|---|---|
| Find a function/class by name | `mcp__code-review-graph__semantic_search_nodes_tool` |
| Trace callers/callees/imports | `mcp__code-review-graph__query_graph_tool` |
| Blast radius of a change | `mcp__code-review-graph__get_impact_radius_tool` |
| Code review on the diff | `mcp__code-review-graph__detect_changes_tool` + `get_review_context_tool` |
| Architecture map | `mcp__code-review-graph__get_architecture_overview_tool` |
| Plan rename / dead code | `mcp__code-review-graph__refactor_tool` |

## Known limitations on this repo

- **0 embeddings** — semantic search is keyword-only. If a name doesn't match closely,
  fall back to `ctx_search` on the path you suspect.
- **2 CALLS edges** — call-graph parsing of decompiled IL2CPP (`assembly/`) is weak.
  For "who calls X" inside `assembly/`, prefer `ctx_search` on the assembly subdir.
  Inside the project's own `Mogul/` source, the graph is fine.

## Workflow

1. `semantic_search_nodes` to locate.
2. `query_graph` with `callers_of` / `callees_of` / `imports_of` to expand.
3. Only then read files (with `ctx_read`).
