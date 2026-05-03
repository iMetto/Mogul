## Location Architecture

To add a new location, add one entry to `PropertySystem.Catalog` in `PropertySystem.cs`. That's it.

Each `MogulLocation` carries all its own data:
- `WorldPosition` — world coords. Use F5 in-game to log player position, F6 for a corner.
- `BuildingSize` — Small / Medium / Large. Room dimensions are derived automatically from this in `MogulLocation.RoomSize`. No changes needed in `LocationSpawner`.
- `Door` (`WallSide`) — which wall the door faces. Use in-game observation after spawning to verify.

**Data ownership:**
- Room dimensions → `MogulLocation.RoomSize` (one switch, one place)
- Building config (S1MAPI) → `LocationSpawner` (spawning concern only)
- All location data → `PropertySystem.Catalog`

`LocationSpawner` is dimension-agnostic — it reads `location.RoomSize`, never switches on `BuildingSize` for dimensions.

## Assemblies
Decompiled source for all referenced libraries lives in `assembly/`:
- `assembly/S1MAPI_Il2Cpp/` — BuildingBuilder, PrefabPlacer, PrefabRef, Prefabs, NavigationBuilder, Materials, etc.
- `assembly/S1API.Il2Cpp.MelonLoader/` — GameLifecycle, Player, Money, etc.
- `assembly/Assembly-CSharp/` — game internals (DoorController, SaveManager, etc.)
- `assembly/Il2CppScheduleOne.Core/` — core game types
- `assembly/SteamNetworkLib-IL2Cpp/` — SteamNetworkClient, HostSyncVar

**Always check these before guessing at API signatures or behaviour.**

## Collaboration Style

The user is learning C# and Unity modding while building this project. They want to understand the code, not just have it written for them.

- **Explain before writing.** When a change touches something non-obvious, describe the concept first ("here's why") and let them ask questions before touching a file.
- **Don't silently fix things.** If you spot a redundancy or mistake in their code, point it out and explain it — don't just rewrite it without asking.
- **Small steps.** One concept at a time. If a fix involves two separate ideas, split them.
- **Let them write code when possible.** Guide to the answer, don't always hand it over.
- **Don't quiz.** Don't pepper them with "do you understand X?" questions — they'll ask if they don't.

<!-- code-review-graph MCP tools -->
## MCP Tools: code-review-graph

**IMPORTANT: This project has a knowledge graph. ALWAYS use the
code-review-graph MCP tools BEFORE using Grep/Glob/Read to explore
the codebase.** The graph is faster, cheaper (fewer tokens), and gives
you structural context (callers, dependents, test coverage) that file
scanning cannot.

### When to use graph tools FIRST

- **Exploring code**: `semantic_search_nodes` or `query_graph` instead of Grep
- **Understanding impact**: `get_impact_radius` instead of manually tracing imports
- **Code review**: `detect_changes` + `get_review_context` instead of reading entire files
- **Finding relationships**: `query_graph` with callers_of/callees_of/imports_of/tests_for
- **Architecture questions**: `get_architecture_overview` + `list_communities`

Fall back to Grep/Glob/Read **only** when the graph doesn't cover what you need.

### Key Tools

| Tool | Use when |
|------|----------|
| `detect_changes` | Reviewing code changes — gives risk-scored analysis |
| `get_review_context` | Need source snippets for review — token-efficient |
| `get_impact_radius` | Understanding blast radius of a change |
| `get_affected_flows` | Finding which execution paths are impacted |
| `query_graph` | Tracing callers, callees, imports, tests, dependencies |
| `semantic_search_nodes` | Finding functions/classes by name or keyword |
| `get_architecture_overview` | Understanding high-level codebase structure |
| `refactor_tool` | Planning renames, finding dead code |

### Workflow

1. The graph auto-updates on file changes (via hooks).
2. Use `detect_changes` for code review.
3. Use `get_affected_flows` to understand impact.
4. Use `query_graph` pattern="tests_for" to check coverage.
