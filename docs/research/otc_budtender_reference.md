# OTC Budtender Reference

Source checked:

- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Logic/BudtenderSpawner.cs`

## What Matters For Mogul

OTC budtenders are not spawned through vanilla `Employee` setup. They use OTC's
shared CivilianNPC infrastructure:

- `NpcSpawner.SpawnCivilianNpc(...)`
- deterministic ID/object name
- deterministic first/last name from seed
- specialized Avatar appearance
- voice database assigned through customer/NPC helper logic

This validates Mogul's current lightweight worker approach: spawn visible NPCs
and layer job behavior in our own system instead of trying to configure vanilla
employees immediately.

## Appearance Details Worth Reusing

OTC gives budtenders a distinct uniform:

- tucked T-shirt
- dark jeans
- cap
- sneakers
- cap-friendly hair pools to avoid clipping
- face layer is always set to prevent black-face rendering
- eye fields are initialized before `LoadAvatarSettings`

Mogul now mirrors this idea in `CustomerSpawner.SpawnWorkerNPC`:

- Cashier: green shirt/cap
- Budtender: blue shirt/cap
- Runner: brown/gold shirt/cap

## Name Pattern

OTC uses seed-based first/last name pools. Mogul currently uses fixed starter
names from `MogulNetwork.ApplyAction(HireEmployee)`:

- `Casey Cashier`
- `Bailey Budtender`
- `Riley Runner`

Later improvement: move names into a deterministic helper and avoid duplicate
first names per location, using the OTC pattern.

## What Not To Pull Yet

Do not copy OTC's per-counter staffing, checkout lock, sales log, or GreenTab
state machinery yet. Mogul has one sell desk per property and a simpler host
state model. Pulling the full OTC staffing model now would add code without
solving the next playable milestone.

Next useful OTC files if cashier automation grows:

- `Logic/CustomerManager.cs` - look for `IsStaffed`, `CheckoutArrivalTime`,
  and `ShowBudtenderPOS`.
- `Logic/Placement/CheckoutCounterInstance.cs` - counter-level staffing state.
- `Logic/BudtenderStorageSearch.cs` - storage search/fetch behavior.

