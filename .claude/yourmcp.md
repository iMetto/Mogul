# Your MCP Reference — lean-ctx + code-review-graph

Two tools. One job each.

**code-review-graph** = structural map of your codebase. Knows what calls what,
what breaks if you change something, what's untested. Lives in `.code-review-graph/`.

**lean-ctx** = compression layer. Makes file reads cheap. Caches files so re-reads
cost 13 tokens instead of 8,000. Compresses shell output so build logs don't eat
your context window.

They work in sequence: graph tells you *what* to read, lean-ctx reads it *cheaply*.

---

## Before Every Session — Run These in Your Terminal

```bash
cd ~/projects/YOUR-PROJECT          # always start from project root
code-review-graph build             # rebuild graph from latest commits (~10-30 seconds)
code-review-graph status            # confirm graph is current and paths are correct
lean-ctx session                    # confirm lean-ctx is rooted at the right project
```

If lean-ctx shows the wrong project root:
```bash
lean-ctx session end --force        # or just open claude from correct dir and it resets
```

If you haven't used the project in a while and want a full clean rebuild:
```bash
code-review-graph build --clean     # purges old graph, rebuilds from scratch
code-review-graph wiki              # regenerates wiki docs from new graph
```

---

## code-review-graph — Shell CLI Commands

These run in your terminal. Not inside Claude Code.

### Graph Management

```bash
code-review-graph build
```
Incremental rebuild — re-parses only files changed since last build.
**When:** Start of every session. Takes 10-30 seconds. Non-negotiable habit.

```bash
code-review-graph build --clean
```
Full rebuild from scratch. Purges everything and re-parses all files.
**When:** Paths are wrong (Windows vs WSL mismatch). Graph seems stale or wrong.
After major refactors that moved many files.

```bash
code-review-graph status
```
Shows node count, edge count, file count, last updated timestamp, and commit hash.
**When:** After build, to confirm it completed correctly. Check that commit hash
matches your current HEAD.

```bash
code-review-graph update
```
Incremental update only — same as build but explicit.
**When:** You made a few edits and want the graph current before a Claude session.

```bash
code-review-graph watch
```
Auto-updates graph on every file save. Runs in background.
**When:** Long coding sessions where you want the graph always current without
manually rebuilding.

### Understanding Your Code

```bash
code-review-graph detect-changes
```
Risk-scored analysis of your current uncommitted changes. Shows which functions
are affected, blast radius, test coverage gaps.
**When:** Before every commit. Before asking Claude to review anything.
**Output:** List of changed files with risk scores and affected functions.

```bash
code-review-graph visualize
```
Generates an interactive HTML graph you can open in a browser. Force-directed
layout, clickable nodes, community colors.
**When:** You want to see the architecture visually. Good for understanding a
new codebase or explaining structure to someone else.

```bash
code-review-graph visualize --format graphml
```
Exports as GraphML for Gephi or yEd.
**When:** You want to do deeper graph analysis in external tools.

```bash
code-review-graph visualize --format obsidian
```
Exports as an Obsidian vault with wikilinks between files.
**When:** You use Obsidian and want your codebase as a navigable knowledge base.

```bash
code-review-graph wiki
```
Generates markdown wiki files in `.code-review-graph/wiki/` — one file per
community cluster. Documents members, execution flows, dependencies.
**When:** After a clean build. After major changes. When you want human-readable
docs of your architecture. The wiki files are your map.

```bash
code-review-graph eval
```
Runs token efficiency benchmarks — measures naive full-file tokens vs graph
query tokens on your actual codebase.
**When:** You want to see how much the graph is actually saving you.

---

## code-review-graph — MCP Tools (Inside Claude Code Only)

These only work when Claude Code calls them. You cannot run them in bash.
Tell Claude to use them by name.

### Start Here Every Session

```
get_minimal_context_tool
```
Ultra-compact context (~100 tokens) about the project. Call this first in any
session where Claude needs project orientation.
**When:** First thing in a new session. Cheaper than reading CLAUDE.md for
basic orientation.

```
build_or_update_graph_tool
```
Builds or incrementally updates the graph from inside Claude Code.
**When:** If you forgot to run `code-review-graph build` in the terminal first.

### Finding Things

```
semantic_search_nodes("name or keyword")
```
Finds any function, class, variable, or type by name or meaning. Returns file
path and exact line numbers.
**When:** You know what you're looking for but not where it lives.
**Example:** `semantic_search_nodes("startPoller")` → returns file + line.
**Always use the qualified name it returns for follow-up queries.**

```
query_graph(target="qualified_name", pattern="callees_of")
```
What does this function call? Returns every function called by the target,
with file paths and line numbers for each call site.
**When:** You want to understand what a function does without reading it.
**Example:** Tells you `startPoller` calls `getSetting`, `calcMinPollerIntervalMs`,
`getAllApiKeys`, `runCycle` — and at which lines.

```
query_graph(target="qualified_name", pattern="callers_of")
```
What calls this function? Returns every production and test caller.
**When:** You want to know if something is actually being used, and from where.
**Critical use:** Finding dead code. If callers_of returns only test files,
the function has no production callers — that's a bug or dead code.

```
query_graph(target="qualified_name", pattern="tests_for")
```
Find tests that cover this function.
**When:** Before changing a function, check what tests exist for it.

```
query_graph(target="qualified_name", pattern="imports_of")
```
What files import this module?
**When:** Understanding how widely used something is before changing it.

```
traverse_graph_tool(start="qualified_name", direction="outbound", max_depth=3)
```
BFS/DFS traversal from a starting node. Follows call chains up to N levels deep.
**When:** You want to trace an execution path from a starting point without
reading individual files.

### Understanding Impact

```
get_impact_radius(files=["src/poller.ts"])
```
Everything that would break if you change this file. Functions, other files,
tests — the full blast radius.
**When:** BEFORE making any change. Run this first, understand what's at risk,
then decide how to proceed.
**Rule:** Never let Claude edit a file without running this first.

```
get_affected_flows(files=["src/poller.ts"])
```
Which execution paths go through this file? Shows you the call chains that
would be disrupted by a change.
**When:** When impact_radius feels too broad and you want to understand
specifically which user-facing flows are affected.

```
detect_changes_tool
```
Risk-scored analysis of current uncommitted changes. Same as the CLI command
but callable from inside Claude Code.
**When:** Before a commit. Before asking Claude to review your changes.

### Architecture Overview

```
get_architecture_overview()
```
Auto-generated architecture map from community structure. Shows layers, coupling
warnings, and structural patterns.
**When:** Start of a session when working on a new area. Onboarding to a
codebase you haven't touched in a while.

```
list_communities()
```
Lists all detected code communities — clusters of related functions and files.
**When:** Understanding how the codebase is organised. Finding which community
a bug lives in.

```
get_community(community_id)
```
Detailed view of one community — all members, flows, dependencies.
**When:** After list_communities identifies the relevant cluster.

```
get_hub_nodes()
```
Most connected nodes — the architectural hotspots. Changes here ripple everywhere.
**When:** Understanding which files are most dangerous to touch.
**Output:** Ranked list of functions/files by connection count.

```
get_bridge_nodes()
```
Chokepoints — nodes that connect otherwise separate parts of the codebase.
Removing or changing them splits the graph.
**When:** Refactoring. Understanding architectural dependencies.

```
get_knowledge_gaps()
```
Untested hotspots, isolated nodes, thin communities, structural weaknesses.
**When:** Understanding where your codebase is most fragile. Planning test
coverage improvements.

```
get_surprising_connections()
```
Unexpected cross-community coupling — things that shouldn't be connected but are.
**When:** Debugging mysterious bugs. Finding accidental dependencies.

```
get_suggested_questions()
```
Auto-generated review questions from graph analysis — bridges, hubs, surprises
that deserve attention.
**When:** You want the graph to tell you what to look at rather than deciding
yourself.

### Code Review

```
get_review_context(files=["src/poller.ts"])
```
Token-optimised review context — structural summary of changes with relevant
snippets. Cheaper than reading full files.
**When:** Reviewing a change. Asking Claude to check something before committing.

```
list_flows_tool
```
All execution flows in the codebase, sorted by criticality score.
**When:** Understanding what the most important code paths are.

```
get_flow_tool(flow_id)
```
Detailed view of one execution flow — the full call chain.
**When:** Tracing exactly how a request travels through your system.

### Refactoring

```
refactor_tool(operation="rename_preview", target="qualified_name", new_name="x")
```
Preview what a rename would affect without making changes. Shows every file
and line that would need updating.
**When:** Before any rename. See the full impact before committing.

```
refactor_tool(operation="dead_code")
```
Find functions and classes that are defined but never called.
**When:** Cleaning up. Understanding what can be safely deleted.

### Wiki

```
generate_wiki_tool
```
Generates markdown wiki from community structure.
**When:** You want human-readable architecture docs regenerated inside a session.

```
get_wiki_page_tool(page="src-poller")
```
Retrieve a specific wiki page by name.
**When:** Quick architecture reference during a session without reading source files.

---

## lean-ctx — Shell CLI Commands

These run in your terminal.

### Setup and Health

```bash
lean-ctx doctor
```
Full diagnostics — PATH, config, MCP config, session state, dashboard port.
**When:** Something isn't working. Start here.

```bash
lean-ctx doctor --fix
```
Attempts to automatically repair detected issues.
**When:** Doctor found problems.

```bash
lean-ctx status
```
Current setup status in brief.
**When:** Quick check that everything is configured.

```bash
lean-ctx session
```
Shows adoption statistics and current session state including project root.
**When:** Verifying lean-ctx is rooted at the correct project.

```bash
lean-ctx sessions list
```
Lists all stored sessions with token counts.
**When:** Understanding what sessions exist. Finding a stale session.

### Token Stats and Monitoring

```bash
lean-ctx gain
```
Visual terminal dashboard — tokens saved, by command, daily breakdown, USD cost.
**When:** Understanding how much lean-ctx is actually saving you. Weekly check.

```bash
lean-ctx gain --live
```
Auto-refreshing live view. Updates every second.
**When:** Watching savings accumulate during an active session.

```bash
lean-ctx gain --graph
```
30-day savings sparkline chart.
**When:** Seeing trends over time.

```bash
lean-ctx gain --daily
```
Day-by-day breakdown with USD estimates.
**When:** Understanding which days you burned the most tokens.

```bash
lean-ctx token-report
```
Full token and memory report — project context, session, CEP scores.
**When:** Diagnosing why a session is eating context faster than expected.

```bash
lean-ctx cep
```
CEP (Context Efficiency Protocol) impact report — score trends, cache hit rates,
compression modes used.
**When:** Understanding how well the compression is actually working.

```bash
lean-ctx dashboard
```
Opens web dashboard at http://localhost:3333 — charts, KPI cards, command table.
**When:** You want a visual breakdown of token usage. Shareable with others.

```bash
lean-ctx wrapped
```
Weekly savings report card.
**When:** End of week summary. Understanding your usage patterns.

### File Operations (Run Yourself in Terminal)

```bash
lean-ctx read src/poller.ts -m signatures
```
Prints compressed signatures to your terminal. Same as ctx_read but in bash.
**When:** You want to read a file cheaply before starting a Claude session.
Gather data yourself before prompting.

```bash
lean-ctx read src/poller.ts -m map
```
Dependency graph and exports only.
**When:** Quick overview of what a file imports/exports before diving in.

```bash
lean-ctx read src/poller.ts -m lines:100-150
```
Specific line range.
**When:** You know which lines matter from a graph query result.

```bash
lean-ctx grep "setPollerInterval" src/
```
Compressed grep across files.
**When:** Finding where something is used across the codebase without reading
each file.

```bash
lean-ctx ls src/
```
Compressed directory listing.
**When:** Quick overview of what's in a folder.

```bash
lean-ctx deps .
```
Project dependency summary.
**When:** Understanding what packages the project relies on.

### Discovery and Optimisation

```bash
lean-ctx discover
```
Analyses your shell history and finds commands that ran uncompressed — missed
savings opportunities.
**When:** Weekly. Find commands you're forgetting to route through lean-ctx.

```bash
lean-ctx benchmark run .
```
Runs compression benchmarks on your actual project files. Shows per-file token
reduction.
**When:** Understanding which files benefit most from compression.

```bash
lean-ctx cache list
```
Shows what's currently in the file read cache.
**When:** Understanding what Claude has already read this session.

```bash
lean-ctx cache clear
```
Clears the file read cache.
**When:** Starting a fresh session. Cache has stale content after big changes.

```bash
lean-ctx cache stats
```
Cache hit rate and savings from caching.
**When:** Understanding how much the cache is helping.

### Utilities

```bash
lean-ctx-off
```
Disables all shell aliases immediately for current session.
**When:** Something is broken and you need raw uncompressed output to debug it.

```bash
lean-ctx-on
```
Re-enables shell aliases.
**When:** After lean-ctx-off, when you're done debugging.

```bash
lean-ctx update
```
Self-updates lean-ctx to latest version.
**When:** Monthly. Check for updates.

```bash
lean-ctx gotchas list
```
Bug memory — auto-detected error patterns from your sessions.
**When:** lean-ctx has been learning from your errors. Check what it knows.

```bash
lean-ctx buddy show
```
Token Guardian — data-driven stats about your coding patterns and token habits.
**When:** Curiosity. Understanding your own usage patterns.

---

## lean-ctx — MCP Tools (Inside Claude Code Only)

### File Reading — The Core Tools

```
ctx_read(path, mode="signatures")
```
Function signatures only via tree-sitter AST. ~10-20% of full file size.
**When:** First look at any file. Understanding what functions exist without
reading implementation.
**Cost:** ~200 tokens for a large file vs ~8,000 for full read.

```
ctx_read(path, mode="map")
```
Dependency graph + exports + API surface. ~5-15% of full file size.
**When:** Understanding what a file imports and exports. Fastest orientation.

```
ctx_read(path, mode="full")
```
Full file content. Expensive first read, ~13 tokens on every subsequent read
(cached by MD5 hash).
**When:** You need to read actual implementation code. You're about to edit
this file. Always use signatures first, then full if you need more.

```
ctx_read(path, mode="lines:106-130")
```
Specific line range. Only those lines, nothing else.
**When:** Graph query gave you a line number. Read only that section.
**Most token-efficient option when you know exactly what you need.**

```
ctx_read(path, mode="diff")
```
Only lines that changed since last read. Near-zero tokens on unchanged files.
**When:** Re-reading a file after making edits. Checking what changed.

```
ctx_read(path, mode="aggressive")
```
Strips boilerplate, comments, blank lines. ~30-50% of full size.
**When:** Large files with lots of repetitive patterns. Config files. Generated code.

```
ctx_read(path, mode="entropy")
```
Shannon entropy filtering — keeps high-information lines, drops low-information ones.
**When:** Log files. Files with lots of repeated similar patterns.

```
ctx_read(path, mode="task")
```
Filters content relevant to your current session task (requires ctx_session task set).
**When:** You've set a specific task context and want task-relevant filtering.

```
ctx_read(path, mode="auto")
```
lean-ctx picks the optimal mode based on file type and size.
**When:** You're not sure which mode to use. Let it decide.

### Search and Navigation

```
ctx_search(pattern, path)
```
Compressed grep. Groups results, removes noise, shows file:line references.
**When:** Finding where something is used across multiple files.
**Never use:** For exploring — use graph tools instead. Use ctx_search only
when you have a specific pattern to find.

```
ctx_tree(path)
```
Compressed directory listing — groups by type, shows sizes.
**When:** Understanding folder structure. Faster and cheaper than ls.

### Shell Execution

```
ctx_shell(command)
```
Runs a shell command and compresses the output.
**When:** Build commands (`npm run build`), test runs (`npm test`),
git operations (`git show`, `git log`).
**Never use for:** Exploring files, finding functions, reading code.
That's what the graph and ctx_read are for.

### Context Management

```
ctx_compress
```
Compresses current conversation context into a checkpoint. Reduces context
size for long sessions.
**When:** Session is getting long and you're worried about context limits.
Before starting a new major task in the same session.

```
ctx_metrics
```
Session statistics — tokens used, savings, compression rates, USD cost estimate.
**When:** Checking how much context you've burned. Mid-session health check.

```
ctx_session
```
Shows current session state — project root, task, version.
**When:** Verifying lean-ctx knows which project you're in.

### Analysis

```
ctx_analyze(path)
```
Shannon entropy analysis + mode recommendation. Tells you which read mode
will be most efficient for a specific file.
**When:** You have a large unfamiliar file and want to know the best way to read it.

```
ctx_benchmark(path)
```
Compares all compression strategies with exact token counts for a file.
**When:** Understanding exactly how much each mode saves on a specific file.

---

## The Correct Investigation Sequence

Every time you investigate anything, follow this order exactly.
This is what prevents the 22-tool-call loops.

### Step 1 — Orient (graph, ~50 tokens total)
```
get_architecture_overview()          # where does this feature live?
list_communities()                   # which cluster is it in?
```

### Step 2 — Locate (graph, ~50 tokens per query)
```
semantic_search_nodes("name")        # find the function
query_graph callees_of               # what does it call
query_graph callers_of               # what calls it — critical for finding bugs
query_graph tests_for                # what tests cover it
```

### Step 3 — Assess impact (graph, ~100 tokens)
```
get_impact_radius(files=[...])       # what breaks if this changes
detect_changes_tool                  # risk score current uncommitted changes
```

### Step 4 — Read only what the graph pointed to (lean-ctx)
```
ctx_read(path, mode="signatures")    # first pass, cheap
ctx_read(path, mode="lines:N-M")     # specific lines the graph identified
ctx_read(path, mode="full")          # only if you need to edit it
```

### Step 5 — Stop and report
Present findings. Do not implement. Wait for Ahmed to decide what to do.

---

## Token Cost Reference

| Operation | Tokens |
|-----------|--------|
| semantic_search_nodes | ~50 |
| query_graph (one query) | ~80 |
| get_impact_radius | ~100 |
| ctx_read signatures (large file) | ~200 |
| ctx_read map | ~100 |
| ctx_read lines:N-M (20 lines) | ~50 |
| ctx_read full (large file, first read) | ~8,000 |
| ctx_read full (cached re-read) | ~13 |
| Native Read (every time) | ~8,000 |
| ctx_shell build output | ~300 |
| Native Bash ls/grep | ~500-2,000 |

---

## What Claude Should Never Do

These are the fallback behaviours that burn your tokens.
If you see Claude doing these, stop it immediately.

| Bad behaviour | What to say |
|---------------|-------------|
| Using native Read on a file | "Use ctx_read with mode=signatures instead" |
| Using Bash to grep/find | "Use ctx_search instead" |
| Using Bash to ls/tree | "Use ctx_tree instead" |
| Using ctx_shell to explore | "Use semantic_search_nodes instead" |
| Reading files without checking graph first | "Run query_graph callers_of first" |
| Making changes without impact check | "Run get_impact_radius first" |
| Continuing after investigation | "Stop. Show me what you found. Wait for me." |

---

## Quick Reference Card

```
BEFORE SESSION (terminal):
  code-review-graph build
  lean-ctx session

FIND SOMETHING:
  semantic_search_nodes("name")

TRACE CONNECTIONS:
  query_graph callees_of "qualified::name"
  query_graph callers_of "qualified::name"

CHECK IMPACT:
  get_impact_radius(files=["path"])

READ CHEAPLY:
  ctx_read path mode=signatures      # first look
  ctx_read path mode=lines:N-M       # targeted
  ctx_read path mode=full            # editing

BEFORE COMMIT (terminal):
  code-review-graph detect-changes
```
