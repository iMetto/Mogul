# Map Pins

## Current implementation

Mogul uses `Mogul/Systems/MogulMapPins.cs`.

It mirrors OTC's `OverTheCounter/Utilities/PropertyPins.cs` pattern:

- create a runtime `GameObject`
- add `Il2CppScheduleOne.Map.POI`
- copy `UIPrefab` from a vanilla `Property.PoI`
- set label text with `POI.SetMainText(...)`
- place the object at the target world X/Z

Pins are local scene objects. Multiplayer works because every client receives
`MogulSaveData` through `MogulNetwork`, then builds the same local pins from the
synced state.

## Pins created

- Owned property pins for every synced `RegisteredLocationIds` entry.
- One active quest pin when `MogulSaveData.ActiveQuestId` resolves to a quest
  with a non-zero `WorldPosition`.

`MogulMapPins.SyncPins()` destroys and rebuilds pins after data changes and when
the main scene loads. This keeps the first implementation simple and avoids
stale pins after purchases, quest tracking changes, or lobby sync.

## Caveats

- This is map POI integration, not a full vanilla quest journal adapter.
- Quest pins currently track only the active Mogul app quest.
- All pins reuse the first vanilla property POI prefab found in the scene.

## Next step

For richer quest UX, use the S1API quest surface documented in
`docs/research/quests.md`:

- create a small `MogulTrackedQuest` adapter
- create S1API quests only for objectives that need vanilla journal/map behavior
- bind quest entries through `QuestEntry.POIPosition` or `QuestEntry.SetPOIToNPC`

