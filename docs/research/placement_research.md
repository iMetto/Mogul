# Placement And Repositioning Research

## Current Mogul State

Mogul storage racks are not player-placed buildables right now. They are spawned
from `PropertySystem.Catalog` by `LocationSpawner`:

- `StorageRackConfig`
- `LocationSpawner.SpawnBuilding`
- `PrefabPlacer.Place(... networked: true ...)`
- tracked in `LocationSpawner._rackObjects`

Their positions are fixed local offsets from the location catalog. There is no
per-rack placement save data yet.

Inventory scanning is resilient:

- `StorageScanner.Scan(buildingRoot)` finds `StorageEntity` components under the
  spawned building root.
- If a storage object stays under the building root, scanning does not need a
  hardcoded rack position.

## Vanilla Placement APIs Found

S1API wrapper:

- `S1API.Building.BuildManager.StartBuilding(ItemInstance item)`
- `S1API.Building.BuildManager.CreateGridItem(ItemInstance item, Grid grid, Vector2 originCoordinate, int rotation, string guid = "")`
- `S1API.Building.BuildManager.CreateSurfaceItem(ItemInstance item, Surface parentSurface, Vector3 relativePosition, Quaternion relativeRotation, string guid = "")`

Game classes:

- `Il2CppScheduleOne.EntityFramework.BuildableItem`
  - `PickupItem()`
  - `SetGUID(...)`
  - `GetBaseData()`
- `Il2CppScheduleOne.EntityFramework.GridItem`
  - `InitializeGridItem(...)`
  - `SetGridData(gridGUID, originCoordinate, rotation)`
  - `_originCoordinate`
  - `_rotation`
- `Il2CppScheduleOne.EntityFramework.SurfaceItem`
  - `InitializeSurfaceItem(...)`
  - `SetTransformData(parentSurfaceGUID, relativePosition, relativeRotation)`

Persistence data:

- `BuildableItemData`
- `GridItemData`
- `SurfaceItemData`

## OTC Lesson

OTC supports player placement in custom buildings, but it is not free. It patches:

- `Grid.Awake`
- `GridItem.InitializeGridItem`
- `BuildManager.CreateGridItem`
- `BuildableItem.PickupItem`
- several grow-light/grid null refs

OTC also keeps custom placement records:

- `PropertySaveData.OtcPlacedItem`
- `SavePlacedItem(buildingId, prefabId, coordX, coordZ, rotation)`
- `RemovePlacedItem(buildingId, coordX, coordZ)`
- live-grid snapshot before save to remove stale moved/deleted records

This means full vanilla-style placement in Mogul should be treated as a real
feature, not a quick toggle.

## Recommendation

For the next grow-tent slice, do not wire vanilla placement yet.

Use Mogul-owned placement records instead:

- store grow tent local position/rotation per location
- spawn/reposition the tent from that record
- let budtender locate the tent by saved location ID/object registry, not by a
  hardcoded coordinate
- scan storage by components under the building root, as we already do

Later, if we want true player placement UX, copy the OTC strategy at a smaller
scale: create a custom grid/buildable pipeline, patch initialization for Mogul
grids, and snapshot live grid items before save.

## Player-Placed Grow Tent Idea

Giving the player a real grow-tent item when hiring a budtender is a good design
direction, but it should be treated as a placement feature, not a small spawn
fix. The likely flow would be:

1. Find the real grow-tent/grow-container item or prefab name.
2. Add it to a Mogul-owned storage/rack or player inventory.
3. Let the player place it.
4. Save the placed tent's local transform/location ID.
5. Have the budtender find that saved tent and use it automatically.

The hard part is step 3. OTC's docs/research notes show vanilla/custom placement
requires grid/buildable plumbing and patches, so do not wire this until the
current placeholder and storage flows are stable.

Low-token research path for names:

- Reuse the saved prefab findings below before searching assembly again.
- If a new topic needs discovery, use a narrow targeted `rg` pass, then fold the
  reusable result back into `docs/research/`.
- If prefab names are not enough, search `docs/research/assembly_items.md`
  first, then use narrow `rg` searches around `ItemRegistry`,
  `GetDefaultInstance`, `GrowContainer`, and `BuildUpdate_GrowContainer`.

## Registered Storage/Grow Prefabs

Manual F5 dump from Main scene, total registered FishNet prefabs: 293.

Storage/grow-related names:

| Index | Prefab name |
| --- | --- |
| 6 | `FilingCabinet_Built` |
| 33 | `WallMountedShelf_Built` |
| 42 | `DisplayCabinet_Built` |
| 52 | `LEDLight` |
| 62 | `StorageRack_Medium` |
| 89 | `DryingRack_Built` |
| 99 | `MoisturePreservingPot_Built` |
| 112 | `GrowTent_Built` |
| 128 | `@Storage` |
| 135 | `StorageRack_Large` |
| 150 | `StorageRack_Small` |
| 158 | `HugeStorageCloset_Built` |
| 174 | `AirPot_Built` |
| 175 | `SoilPourer_Built` |
| 191 | `CheapPlasticPot_Built` |
| 196 | `BigSprinkler_Built` |
| 215 | `HalogenLight` |
| 219 | `FloorRack` |
| 261 | `SmallStorageCloset_Built` |
| 268 | `LargeStorageCloset_Built` |
| 283 | `StorageUnit` |
| 288 | `MediumStorageCloset_Built` |
| 292 | `FullSpectrumLight_Built` |

Useful broader grow/building prefabs from the full dump:

| Index | Prefab name |
| --- | --- |
| 26 | `MushroomBed_Built` |
| 45 | `MushroomSpawnStation_Built` |
| 64 | `Botanist` |
| 108 | `Sprinkler_Built` |
| 130 | `ChemistryStation_Built` |
| 146 | `PackagingStation_Mk2` |
| 171 | `Packager` |
| 200 | `MixingStation_Built` |
| 230 | `LabOven_Built` |
| 267 | `PackagingStation` |

`GrowTent_Built` is registered and should be spawned through `PrefabPlacer` with
`networked: true`, matching the storage rack path. If it appears, this is a much
better test target than the temporary primitive placeholder.

## Current Grow-Tent Placeholder

The first Mogul budtender slice keeps the tent deliberately lightweight:

- `EmployeeSystem.SyncGrowTent` creates a primitive local visual under the
  spawned building root when a location has a hired budtender.
- Production remains virtual via `EmployeeProduction.TestBudtenderProductId`
  (`ogkush`) at `20` packages per budtender per elapsed day.
- The Manage screen reads real storage through `StorageScanner.Scan(buildingRoot)`
  and appends virtual OG Kush from `EmployeeSystem.GetVirtualInventory`.

This is not a real buildable item yet. The next durable step is saved
per-location tent transform data, then replacing the primitive visual with a real
grow tent prefab/buildable once insertion into storage is solid.

## NPC Routing Impact

If furniture/tents become movable and should block walking, rebuild the
`NavigationBuilder` after placement changes. If they are only job targets, the
budtender can route to the current saved local position without needing static
anchors.

## Latest Manual-Test Findings

- Employee spawn positions pass through `CustomerSpawner`, which samples the
  global NavMesh. Interior targets may be snapped outside, so employees should
  spawn at an exterior anchor and route inside through `NavigationBuilder`.
- The grow-tent creation event can succeed while the visual is not apparent.
  Keep logging local and world coordinates; if the log exists, debug transform
  height/visibility before assuming role sync failed.
- `GrowTent_Built` now spawns successfully. The current Westville local Y was
  reduced from `0.80` to `0.35` after the tent appeared visibly above the floor.
  Recheck its final floor contact in-game before freezing that offset.
- Reattached storage racks can still trigger vanilla storage visualizer null
  refs on item removal. The checkout path now guards the exception, but durable
  physical-stock mutation still needs more investigation.
