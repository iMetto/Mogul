# Rebuild Lifecycle Notes

## Customization Rebuild

`LocationSpawner.RebuildBuilding(locationId)` is the customization path used by
`BuildingPreview`. It evicts active customers and workers, preserves existing
storage rack GameObjects, destroys the old building root, then spawns the
replacement building.

Important lifecycle rule: storage racks must survive rebuilds. They can contain
player inventory, so the rebuild path detaches live rack objects from the old
building root before destroying it, then reattaches them to the new root. If no
live rack exists, `SpawnBuilding` creates fresh rack prefabs from the location's
`StorageRacks` config.

## Employee Placement

Workers are spawned through `EmployeeSystem.SyncLocation`. `CustomerSpawner`
snaps spawn positions to the global NavMesh, so using an interior staff anchor
as the spawn point can put workers outside. The current flow spawns workers at
the location's external spawn anchor, then routes them through the building's
`NavigationBuilder` to their local staff anchor.

Cashier position is dictated by the sell desk transform:

- Customer queue side: `deskPosition + deskRotation * Vector3.forward * 0.3f`
- Cashier/staff side: `deskPosition - deskRotation * Vector3.forward * 0.9f`

So if the cashier stands on the wrong side, check `deskRotation` first. For an
east-facing door/counter, `deskRotation` should be `Quaternion.Euler(0, 90, 0)`:
the customer side points toward the door and the staff side points deeper into
the room. Westville/Hills previously used 180 degrees, which put the staff anchor
on the wrong axis and caused the route to cut through the counter.

On arrival, cashiers should face the queue anchor. Without an explicit facing
step, the NPC can stop with whatever heading the navigation path left it with.

## Grow Tent

`EmployeeSystem.SyncGrowTent(locationId)` creates the budtender grow tent when
the location is owned, has a budtender, and the building root exists. Rebuilds
remove the old tent during employee eviction, then `OnBuildingReady` triggers
`SyncLocation`, which recreates the tent under the new building root.

The grow tent log includes both local and world coordinates. If the log appears
but the tent is not visible, treat it as a placement/height/material issue, not
a missing spawn event.

The first primitive placeholder logged correctly but never appeared in-game even
when F5 showed the player standing close to its world position. F5 prefab dumps
confirmed `GrowTent_Built` is a registered FishNet prefab, so the current test
path uses `PrefabPlacer.Place(new PrefabRef("GrowTent_Built"), ..., networked:
true)` like the storage rack path. Watch for:

- `GrowTent_Built requested ...`
- `GrowTent_Built ready ...`

## Storage Removal Guard

After rack preservation was added, checkout hit a vanilla
`StorageVisualizer.RefreshVisuals` null reference via `ItemSlot.ChangeQuantity`.
`StorageScanner.TakeOne` now catches removal failures and returns `false` so a
bad storage visual update does not break the customer flow. This is a guardrail,
not the final storage fix.
