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

## Current Budtender Grow Tent

The current Mogul budtender slice uses a real `GrowTent_Built` prefab spawned
through `PrefabPlacer` and a Mogul-owned visual plant under
`Model/IntObj/PlantContainer`.

Plant visual source:
- Reliable path is `Registry.GetItem<SeedDefinition>(seedId).PlantPrefab`.
- Exact weed seed ids from runtime `SeedDefinition` dump:
  - `ogkushseed` -> `OGKush_Plant`
  - `sourdieselseed` -> `SourDieselPlant`
  - `greencrackseed` -> `GreenCrackPlant`
  - `granddaddypurpleseed` -> `GranddaddyPurplePlant`
- `cocaseed` exists and maps to `CocaPlant`, but it is not in the first
  budtender order set.

Budtender visual timing:
- Working day is `08:00-17:00`.
- The pot visual is empty before the order's workday starts.
- At `08:00` on the order's ready/work day, the tent visual uses the ordered
  product's seed `PlantPrefab` and forces the final plant growth stage visible.
- At `17:00`, the order completes and the visual clears after delivery/removal.
- Grow visuals are synced on workday phase edges only, not every frame:
  before-work, workday, after-work, and new day.

Production/storage:
- A budtender order creates one stack (`20`) of the selected product.
- Completion deposits into physical `StorageEntity` slots through
  `StorageScanner.TryAddProductStack`; virtual stock remains a fallback if
  physical insertion fails.

The slot model is currently implemented as slot `0`, but the grow visual keying
uses `locationId:grow_<slot>` so additional tents can be added later without
rewriting the visual state model.

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
- Manual movement status as of 2026-05-14: counter, grow tent, and storage rack
  are still fixed Mogul-owned placements, not vanilla player-movable buildables.
  Moving them manually in-game would require placement/save plumbing similar to
  the OTC custom-grid path above. For now, tune local transforms in the location
  catalog/spawn helpers and keep storing reusable measured positions here.
- Latest Westville measurements:
  - Cashier desired world `(-161.33, -2.44, 77.19)`, local `(10.11, 0.96,
    7.19)`, yaw `180.6` / local yaw `-179.4`.
  - Grow tent desired world `(-170.95, -2.44, 77.48)`, local `(0.49, 0.96,
    7.48)`, yaw/local yaw `90.4`.
- Hardcoded Westville defaults from 2026-05-14 dump:
  - S1API built root `(-167.08, -3.53, 73.55)`, room size `7.50 x 5.50 x
    3.00`. Catalog `WorldPosition.y` is `-3.13` because `LocationSpawner`
    subtracts the `0.4` foundation height at spawn time.
  - Desk local `(1.05, 0.00, 1.25)`, yaw `270.0`.
  - Cashier local `(0.80, 0.00, 0.45)`, yaw `5.6`.
  - Grow tent local `(3.94, 0.25, 5.00)`, yaw `180.4`.
  - Storage rack live local `(1.25, 0.90, 5.05)`, yaw `0.0`; catalog
    stores `y=0.40` because rack spawning adds `0.50` to configured rack
    height.
- Hardcoded Downtown defaults from 2026-05-14 dump:
  - S1API built root `(105.00, 0.75, -3.29)`, room size `7.00 x 9.00 x
    3.00`. Catalog `WorldPosition.y` is `1.15` because `LocationSpawner`
    subtracts the `0.4` foundation height at spawn time.
  - Desk local `(1.50, 0.00, 4.50)`, yaw `0.0`.
  - Cashier standing position catalog local `(0.70, 0.00, 4.63)`, yaw `105.0`.
  - Grow tent local `(6.50, 0.25, 4.80)`, yaw `271.0`.
  - Storage racks are stacked against the wall near measured world `(108.48,
    1.31, -2.78)`: local `(3.48, 0.50, 0.51)` and `(5.10, 0.50, 0.51)`.
    Catalog stores `y=0.00` for both because rack spawning adds `0.50`.
  - Rebuild/reposition handling reapplies catalog or saved local transforms to
    every live rack after reparenting, so non-movable extra racks follow the
    moved building instead of preserving old world positions.

## Mogul-Only Move Mode
First implementation:
- Pressing `F9` near an owned Mogul location starts the mode without opening the
  phone. Pressing `F9` again cancels.
- The location Manage panel also has a `MOVE OBJECTS` button.
- The mode is keyboard/list driven: click an object in the small left overlay or
  press `1`-`4`, `WASD`/arrow keys move, `Q`/`E` rotate, `PageUp`/`PageDown`
  adjust height, `Enter` saves, and `Esc` cancels.
- Saved transforms are stored per location/object in
  `MogulSaveData.LocationObjectPlacements`.
- Current object IDs are `desk`, `cashier`, `grow_tent`, and `storage_0`.
- The cashier anchor is visualized with a yellow pole and blue forward bar while
  move mode is active.
- Saving or cancelling dumps every editable object's local position/yaw to the
  Melon log with `[PLACE]` lines so the values can be copied back into
  `PropertySystem.Catalog`.

Runtime effects:
- Moving `desk` moves the counter/register GameObject and recomputes the queue
  anchor from the desk transform. `CustomerManager.ClearQueueCache()` is called
  so future queue slots rebuild from the new anchor.
- Moving `cashier` changes the staff anchor/facing used by future cashier
  spawns and warps the live cashier there if one exists.
- Moving `grow_tent` moves the live grow tent and saves its respawn transform.
- Moving `storage_0` moves the live rack transform. Storage scanning should
  still work because the rack remains parented under the building root.

Known limits:
- This is not vanilla build placement. It only moves Mogul-owned registered
  objects.
- It does not yet preview or validate NPC paths. A bad counter/rack position can
  still block or confuse routing until validation is added.
- It does not rebuild interior navigation when objects move. The current objects
  are treated as job/interaction targets, not navmesh blockers.

Placement defaults live together in `PropertySystem.Catalog`:
- desk/cashier/register under `SellDeskConfig`
- grow tent under `GrowTentConfig`
- storage racks under `StorageRackConfig`

## Weather And Indoor Handling

Mogul buildings are spawned structural rooms, but vanilla weather/NPC systems do
not automatically treat all custom structures as indoor buildings.

Current implementation:
- `LocationSpawner.ConfigureWeatherEnclosure` adds a `WeatherEnclosure` and a
  `BasicEnclosure` volume to each spawned Mogul building and registers it with
  `EnvironmentManager`.
- The enclosure is sized from the effective room dimensions and foundation
  height.
- Mogul worker NPCs are marked `IsUnderCover = true` while active.

Important caveats:
- Calling `NPC.Actions.UpdateUmbrellaUse()` on spawned Mogul workers caused
  IL2CPP null refs when umbrella internals were not initialized. Do not call
  `SetCanUseUmbrella`/`UpdateUmbrellaUse` unless the vanilla NPC action state is
  known ready.
- A separate vanilla `Wheel.OnWeatherChange` null ref has appeared from
  `EnvironmentManager.UpdateWeatherEntities`; treat this as a weather entity
  registration side effect to watch while testing weather enclosures.
