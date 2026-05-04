# Subagents

Use Haiku subagents for **focused lookups** that return a small, bounded payload:

- "find the API surface for X in `assembly/<subdir>/`"
- "how does OTC handle Y" (read OTC source, return summary)
- "list all callers of Z across `Mogul/` and return signatures"

Keep synthesis, design, and code-writing on the main session.

## Required briefing block

Every subagent prompt must include this block verbatim:

```
Use lean-ctx MCP tools for all file access:
- ctx_read instead of Read/cat
- ctx_shell instead of Bash
- ctx_search instead of grep/rg
- ctx_tree instead of ls/find
The lean-ctx root may not match your working directory — use absolute paths.
Return under 200 words. Signatures and file:line references only, no full bodies
unless asked.
```

## Choosing the assembly path

The main agent specifies which assembly subdir(s) to search. Subagents don't infer:

- `assembly/S1MAPI_Il2Cpp/` — BuildingBuilder, PrefabPlacer, PrefabRef, Prefabs,
  NavigationBuilder, Materials
- `assembly/S1API.Il2Cpp.MelonLoader/` — GameLifecycle, Player, Money
- `assembly/Assembly-CSharp/` — game internals (DoorController, SaveManager, etc.)
- `assembly/Il2CppScheduleOne.Core/` — core game types
- `assembly/SteamNetworkLib-IL2Cpp/` — SteamNetworkClient, HostSyncVar

If unsure which subdir holds the API, brief the subagent to grep across all of
`assembly/` first, then narrow.

## When NOT to spawn a subagent

- A single-file lookup you can do in one `ctx_read` — just do it.
- Anything that requires the conversation context to make sense.
- Anything where the answer drives an immediate decision and the round-trip
  cost of a subagent exceeds the token saving.
