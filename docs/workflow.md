# Workflow

One-page research and coding loop for this repo.

## Research order

For any non-trivial change, in this order:

1. **Graph first** — `semantic_search_nodes_tool` / `query_graph_tool`.
   30 seconds, cheap, gives you names and import edges.
2. **Subagent for big lookups** — spawn Haiku for `assembly/` or OTC reference reads.
   Return: bounded summary, signatures, file:line refs.
3. **Read your own code** — `ctx_read` for files in `Mogul/`. Use `mode=signatures`
   when you only need the API surface.
4. **Design with the user** — explain the approach, let them push back.
5. **Code** — only after the above.

## When to spawn a subagent vs do it inline

| Question | Inline | Subagent (Haiku) |
|---|---|---|
| "What does `LocationSpawner.SpawnRoom` do?" | ✅ one ctx_read | — |
| "How does S1MAPI's BuildingBuilder configure walls?" | — | ✅ assembly lookup |
| "How does OTC route customers to a counter?" | — | ✅ OTC lookup |
| "Who calls `PropertySystem.Catalog`?" | ✅ query_graph | — |

Rule of thumb: if the lookup spans more than 3 files in `assembly/` or OTC, subagent.

## Compaction vs new session

- **Don't auto-compact.** Compaction silently drops fidelity.
- At ~50–60% context **and** switching tasks → start a new session. Save a 10-line
  handoff to a project memory or `docs/handoff.md`.
- Mid-task and need more room → still prefer new session. The forcing function of
  writing a handoff usually surfaces drift.
- Glance at `/usage` after each major step.

## Build / test loop

```
dotnet build Mogul/Mogul.csproj
```

Run after each meaningful edit. The PostToolUse hook re-indexes the graph.

## Reference repos

- OTC reference mod: `/home/imetto/projects/mods/OTC-S1-Mod/` (added to
  `additionalDirectories` — readable directly).
- Assembly decompiles: `assembly/` in this repo. See subdir map in
  `.claude/rules/subagents.md`.
