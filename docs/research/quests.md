# Quest Research

## Current Mogul Approach

Mogul now has a lightweight app-owned quest layer:

- Definitions live in `Mogul/Systems/MogulQuestSystem.cs`.
- Save state uses:
  - `MogulSaveData.ActiveQuestId`
  - `MogulSaveData.ActiveQuestProgress`
  - `MogulSaveData.CompletedQuestIds`
- The phone app `QUESTS` tab shows progress, tracking state, claim buttons, and
  reach rewards.

This is intentionally separate from the vanilla journal for the first playable
slice. It lets us ship quests without taking on map POI/journal integration yet.

## S1API Quest Surface

Useful files:

- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Quests/QuestManager.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Quests/Quest.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Quests/QuestEntry.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Quests/QuestWrapper.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Quests/Constants/QuestState.cs`

Important API shape:

- `QuestManager.CreateQuest<T>()` creates and registers a custom S1API quest.
- Custom quests subclass `S1API.Quests.Quest`.
- A custom quest overrides:
  - `Title`
  - `Description`
  - optionally `AutoBegin`
  - optionally `QuestIcon`
- `Quest.AddEntry(title)` creates a `QuestEntry`.
- `QuestEntry.Begin()`, `Complete()`, and `SetState(...)` control objective state.
- `Quest.Begin()`, `Complete()`, `Cancel()`, `Expire()`, `Fail()`, and `End()`
  forward to the underlying vanilla quest component.
- `QuestEntry.POIPosition` can bind an objective to a world position.
- `QuestEntry.SetPOIToNPC(npc)` can bind an objective to an NPC.
- `QuestWrapper` can subscribe to completion/failure for either custom or base
  game quests.

## Caveats

S1API quest construction creates live Unity objects, icons, POI prefabs, and
underlying `Il2CppScheduleOne.Quests.Quest` components. That is useful for
vanilla journal/map integration, but it is heavier than the app-owned checklist
we need right now.

Recommended next step:

1. Keep simple Mogul progression quests in the app-owned layer.
2. Add S1API-backed quests only when a quest needs a world POI, NPC target, or
   vanilla journal visibility.
3. Wrap S1API quest creation behind a small `MogulTrackedQuest` adapter instead
   of letting app UI code touch S1API directly.
