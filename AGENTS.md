# Project Notes

Mogul is a Schedule One mod that adds an online-reach economy on top of vanilla
mechanics. Players grow reach, attract synthetic NPC customers, earn income from
properties, and unlock larger property/order loops.

Core loop: reach -> customers -> income -> properties -> higher reach quests.

Properties are buy-and-staff sales locations. Income comes from walk-in
synthetic NPCs spawned on demand, never from the vanilla pool, plus future
online orders. Players can man the counter themselves or hire lightweight NPC
staff.

## References

Use `docs/references.md` as the index for project notes and external references.
Reusable findings belong in focused files under `docs/research/`.

Keep OTC external at `/home/imetto/projects/mods/OTC-S1-Mod/`. Use it as a
behavior reference, not as code to copy wholesale.

Decompiled references live in `assembly/`:

- `assembly/S1MAPI_Il2Cpp/` - BuildingBuilder, PrefabPlacer, PrefabRef,
  Prefabs, NavigationBuilder, Materials.
- `assembly/S1API.Il2Cpp.MelonLoader-1/` - GameLifecycle, Player, Money.
- `assembly/Assembly-CSharp/` - game internals such as DoorController and
  SaveManager.
- `assembly/Il2CppScheduleOne.Core/` - core game types.
- `assembly/SteamNetworkLib-IL2Cpp/` - SteamNetworkClient, HostSyncVar.

Generated IL2CPP files are large. Search narrowly and read only the relevant
line windows around matches.

## Tool Usage

Keep tool usage proportional to the task. Do not run token-saving, graph, or
indexing tools by default for small documentation edits, simple known-file
changes, or direct user instructions.

Use lean-ctx only when explicitly requested or when a command/file read is
expected to produce large output. Plain shell reads are fine for small known
files.

Use code-review-graph only when it helps answer a structural code question, such
as locating a function or understanding what calls/uses it. Do not run graph
builds or update passes as a routine final step.

Code-review-graph does not currently expose assembly lookup reliably. On
2026-05-14, graph node searches for `DoorController` and `GrowContainer`, plus
a graph `file_summary` for
`assembly/Assembly-CSharp/Il2CppScheduleOne/Building/BuildUpdate_GrowContainer.cs`,
returned zero results. Raw `rg` does find those symbols in assembly files, so
assembly research should use `rg` plus focused line-window reads instead.

## Chat Usage

Keep chat context small. For non-trivial work, watch usage before broad research
and before switching tasks. If usage is high, recommend a fresh chat with a
short handoff instead of relying on compaction.

Handoff format:

```text
Task: <one-line goal>
Done: <files touched and decisions made>
Next: <next concrete step>
Open: <questions or risks>
```
