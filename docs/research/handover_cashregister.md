# Handover: Cash Register Visual — Blocked on Prefab Name

## Status
The counter spawns correctly (dark grey, correct position). The cash register visual does **not** appear. Build is clean, late-join guardrail is in place.

## The Problem
`Prefabs.CashRegister` in S1MAPI is defined as `new PrefabRef("CashRegister")`. `PrefabRef.Find()` searches FishNet's registered spawnable prefab list by `gameObject.name`. It returns `null`, which means either:

- `"CashRegister"` is the wrong name for this game version, or
- The cash register is not a FishNet NetworkObject at all (static scene prop, vanilla shop only)

We tried three approaches, all failed:
1. `PrefabPlacer.Place(..., networked: true)` — `onReady` never fires; FishNet queues it but never resolves because nothing spawns it server-side
2. `PrefabPlacer.Place(..., networked: false)` — invisible (renderers disabled until `OnStartClient`)
3. `Prefabs.CashRegister.Instantiate()` — returns `null` because `Find()` can't locate the prefab

## What to Test Next

**Press F5 in-game** after the Main scene loads (after the building spawns). This dumps all FishNet registered prefab names to the log.

### Provide this from the log:
Paste all lines that look like:
```
[Prefabs] 47 registered prefabs:
[Prefabs]  [0] SomePrefabName
[Prefabs]  [1] AnotherPrefab
...
```

### What we're looking for:
- Is anything named `CashRegister`, `cashregister`, `Cash_Register`, `Register`, etc.?
- If yes → fix the `PrefabRef` name in S1MAPI or override it in `SellDesk.cs`
- If no → the cash register is not a FishNet prefab; we need a different approach

## Key Findings

| Approach | Outcome |
|---|---|
| OTC counter visual | Custom `CashRegister.glb` loaded via `GltfLoader`, parented non-networked to a FishNet GridItem (cloned plastictable). They never use `Prefabs.CashRegister`. |
| FishNet + SteamNetworkLib | Not alternatives — FishNet is the spawn layer, SteamNetworkLib is transport. Can't bypass FishNet for NetworkObject spawning. |
| Non-networked `Object.Instantiate` | Works for both host and client because each machine runs `SpawnDesk` independently. Visual doesn't need to be synced. |
| `PrefabRef.Find()` | Iterates `NetworkManager.GetPrefabObjects<PrefabObjects>(0)` and matches by `gameObject.name`. Returns null if name doesn't match or prefab isn't registered. |

## Decision Tree After Getting the Dump

```
F5 dump shows a matching name?
  YES → Update PrefabRef name in SellDesk.cs, re-test Instantiate() path
  NO  → Cash register is not a FishNet prefab. Options:
          A) Embed a CashRegister.glb (like OTC) — best long-term, needs a 3D model
          B) Clone vanilla "cashregister" item via BuildableItemCreator + BuildManager.CreateGridItem
             (need to verify item ID exists in Registry — try "cashregister" or "cash_register")
          C) Primitive placeholder (BoxMesh + texture) — fast, ugly
```

## Relevant Files
- `Mogul/Systems/SellDesk.cs` — `SpawnDesk()` around line 175, the `Instantiate()` call
- `Mogul/Core.cs` — `DumpFishNetPrefabs()` added, called on F5
- `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/PrefabRef.cs` — `Find()` implementation, iterates FishNet list
- `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/Prefabs.cs:21` — `CashRegister = new PrefabRef("CashRegister")`
- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Logic/Placement/CheckoutCounterInstance.cs:348` — OTC's GLB approach

## Mogul Placement Notes

`MogulLocation` now has `SellDeskConfig` for per-location sell-desk tuning.
Use it instead of hardcoding register/cashier transforms in `SellDesk`.

Current fields:

- `registerLocalPos` - register local position under the counter transform.
- `registerLocalRotation` - register local rotation under the counter transform.
- `staffLocalPos` - optional exact cashier standing local position.

Westville currently overrides the register rotation to
`Quaternion.Euler(0, 0, 0)`, a 90-degree right turn from the previous
`Quaternion.Euler(0, 270, 0)` placement.

Latest Westville tuning pass:

- User cashier reference: world `(-161.31, -2.44, 77.33)`, yaw `181.4`.
- User register-on-counter reference: world `(-161.32, -1.51, 76.63)`.
- Register local offset was nudged to `(-0.13, 0.95, 0.12)` so the visual moves
  deeper onto the intended counter spot while keeping the current tested height.
- This still needs an in-game confirmation pass because the standing reference
  and the counter-top reference came from different world heights.

F5 now logs world position, player yaw, nearest Mogul location local position,
and nearest Mogul location local yaw. Stand where the cashier/register should
be, face the desired direction, and paste those values into the test report for
config tuning.

## Local Ollama Pass

`tools/local-research/run-research.sh cash-registers` was useful as a lead
generator but not strong enough as a source of truth. It found references to
`CheckoutCounterInstance`, `RegisterBalance`, `IsStaffed`, and
`RegisterFloatingText`, but the actionable placement details still needed direct
verification in OTC source.

Verified OTC source:

- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Logic/Placement/CheckoutCounterInstance.cs`
- `SpawnCashRegister(Transform deskTransform)`
- register GLB local position: `new Vector3(-0.74f, 0.06f, 0.49f)`
- register GLB local rotation: `Quaternion.Euler(345f, 90f, 90f)`
- register GLB local scale: `Vector3.one * 0.14f`
- `RegisterPosition` returns `_registerInstance.transform.position`
- `DepositToRegister(float amount)` increments `_registerBalance`
