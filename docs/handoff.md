# Handoff — Structural Cleanup Before New Functionality

**Session date:** 2026-05-04
**Status:** Mogul is structurally healthy at Phase 1/2. Six small things to address
before adding new features. None are urgent — none are blockers. Order them by what
you feel is the worst smell.

## What was done this session (don't redo)

- Slimmed `CLAUDE.md` from ~250 lines to ~70.
- Split tool/collaboration rules into `.claude/rules/code-graph.md`,
  `.claude/rules/collaboration.md`, `.claude/rules/subagents.md`.
- Wrote `docs/workflow.md` (agent-facing) and `docs/howto.md` (user-facing).
- Added `lookup-assembly` and `lookup-otc` Haiku-subagent skills.
- Added a PreToolUse hook (`.claude/hooks/block-assembly-native-read.sh`) that
  blocks native Read on `assembly/` paths.
- Added OTC reference repo to `permissions.additionalDirectories`.
- Confirmed graph indexes 2224 files including `assembly/`. Embeddings are off
  (semantic search is keyword-only). Skipped enabling embeddings — IL2CPP
  decompiles have mangled names; not worth the indexing pass right now.

## Code review findings — actionable items

### 1. `MogulNetwork.cs` does two jobs (priority: medium)

**File:** `Mogul/Systems/MogulNetwork.cs` (276 lines)

It's both transport (Steam, sync messages, RequestAction, OnClientAction) AND
persistence (SaveToDisk, LoadFromDisk, Commit, GetSavePath, ResetData). These
are different responsibilities and the persistence side will grow with employees,
suppliers, and quests.

**Action:** Extract `MogulPersistence` static class with the disk methods. Leave
network transport in `MogulNetwork`. Update `Core.cs` to Initialize both.

**Why before new features:** Adding employee/supplier persistence into the
already-mixed class makes the future split harder.

### 2. `DumpStoragePrefabs()` is debug code in a production class (priority: low, easy)

**File:** `Mogul/Systems/MogulNetwork.cs`

A debug method sitting in the network class. Move to a `Mogul/Debug/`
folder or guard with `#if DEBUG` / a debug-key handler.

**Action:** 5-minute fix. Either gate or relocate.

### 3. `CustomerManager.cs` mixes concerns (priority: medium)

**File:** `Mogul/Systems/CustomerManager.cs` (315 lines)

Has the customer state machine + queue routing + door geometry helpers
(`ComputeDoorExterior`, `TryGetCounterWorldPos`, `TryFindNearestLocation`).

**Action:** Move geometry helpers to either:
- Extension methods on `MogulLocation`, or
- A new `Mogul/Systems/LocationGeometry.cs` static.

Leave the state machine in `CustomerManager`. Don't split the state machine —
it's coherent.

### 4. `QueueSlots` has three slot-build paths (priority: low — investigate first)

**File:** `Mogul/Systems/QueueSlots.cs` (159 lines)

Methods: `Compute`, `BuildFromOverride`, `BuildExterior`. Worth checking whether
`BuildFromOverride` is still used or vestigial. If kept, add a one-line comment
explaining when each path runs.

**Action:** Investigate, then either delete the override path or document it.

### 5. No tests (priority: low — but trending up)

The graph reports 3 Tests, but they're all in `assembly/`. Project has zero.
Phase 1 is fine without them. Worth adding two smoke tests soon:

- `PropertySystem.Catalog` round-trip (serialize/deserialize one location).
- `MogulNetwork.MogulActionMessage.Serialize/Deserialize` round-trip.

**Action:** Add a small test project (`Mogul.Tests`) with the two round-trips.
Defer until after item 1 (since persistence will be moving).

### 6. `MogulApp.cs` is 359 lines and growing (priority: defer)

**File:** `Mogul/Apps/MogulApp.cs`

Phone-app UI builder with `BuildHeader`, `BuildPropertiesPanel`,
`BuildPickerPanel`, `ShowDesignPicker`, etc. Will triple as reach/quests/
employees/suppliers panels are added.

**Action:** **Don't split yet.** Wait until:
- The file crosses ~500 lines, OR
- You add the second non-properties panel (e.g. quests panel).

When you do split, the pattern is `Apps/Panels/<Name>Panel.cs` per panel.

## Suggested order

1. **Item 2** (`DumpStoragePrefabs` cleanup) — 5 min, builds momentum.
2. **Item 4** (`QueueSlots` investigation) — 15 min, understand before refactoring.
3. **Item 1** (`MogulNetwork` split) — biggest win, biggest care.
4. **Item 3** (`CustomerManager` geometry helpers extraction) — clean follow-up.
5. **Item 5** (smoke tests) — after item 1 lands.
6. **Item 6** — wait.

## What to tell the next session

Open the new session with:

> *"Read `docs/handoff.md`. Start with item 2 (DumpStoragePrefabs cleanup).
> Explain first, then do the change. After we land item 2, I'll pick the next one."*

That gates everything: explain-first, one item at a time, your call on what's next.

## Files NOT to touch (recently set up)

- `CLAUDE.md` — slim, intentional
- `.claude/rules/*` — wired to the agent
- `.claude/hooks/block-assembly-native-read.sh` — enforcement, working
- `.claude/skills/lookup-*.md` — Haiku skill templates
- `docs/workflow.md`, `docs/howto.md`, `docs/handoff.md` — the docs

## Uncommitted state at handoff

`git status` shows working changes on `main` (assembly graph rebuild artifacts +
the new docs/rules/skills/hooks). Review and commit when ready — nothing has
been auto-committed.
