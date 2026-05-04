## Project Vision

Mogul is a Schedule One mod that adds an online-reach economy on top of the vanilla game without altering vanilla mechanics. It does not interfere with the player's existing dealers, deals, or vanilla NPCs — it only adds new functionality.

**Core loop:** reach (online following) is the central resource. Players grow reach by completing increasingly antisocial quests (e.g. pickpocket a target → upload it → followers up). Higher reach unlocks:
- More non-vanilla NPCs visiting your stores
- Bigger online orders from high-spenders
- Access to more property purchases
- New quest tiers, supplier discounts, etc.

**The Mogul phone app is the central hub.** It handles:
- Property purchases & ownership management
- Reach / online following display
- Quest list and progression
- Per-property management (designs, employees, stock, prices)
- Hire/fire employees: NPC sellers (counter staff for a daily fee) and budtenders (grow weed/meth/cocaine for material cost only)
- Supplier purchasing — different suppliers, quest-driven discounts, bulk pricing

**Properties** are sales locations the player buys, customises, and either mans personally or staffs with hired NPCs. They generate income via:
- Walk-in synthetic NPC customers (count scales with reach)
- Online orders from big spenders (volume scales with reach)

**Synthetic NPCs only.** Customers are spawned on demand (one block away, around a corner) and routed to the store, not pulled from the vanilla NPC pool.

**Phase roadmap:**
- Phase 1 — properties, designs, basic spawn (✅ in progress)
- Phase 2 — sell desk (counter + register + storage), introduce online reach + quests
- Phase 3 — synthetic NPC customers, queue system, fulfilment from any in-building storage
- Phase 4 — shipment quests, car deliveries, cops/cartel pursuit
- Future — employees, suppliers, post-purchase customisation upgrades

**Computer terminal: deferred / dropped.** Originally planned as an in-shop management UI; phone app covers all of it, no need for a computer in the desk.

## Location Architecture

To add a new location, add one entry to `PropertySystem.Catalog` in `PropertySystem.cs`. That's it.

Each `MogulLocation` carries all its own data:
- `WorldPosition` — world coords. Use F5 in-game to log player position, F6 for a corner.
- `BuildingSize` — Small / Medium / Large. Room dimensions are derived automatically from this in `MogulLocation.RoomSize`. No changes needed in `LocationSpawner`.
- `Door` (`WallSide`) — which wall the door faces. Use in-game observation after spawning to verify.

**Data ownership:**
- Room dimensions → `MogulLocation.RoomSize` (one switch, one place)
- Building config (S1MAPI) → `LocationSpawner` (spawning concern only)
- All location data → `PropertySystem.Catalog`

`LocationSpawner` is dimension-agnostic — it reads `location.RoomSize`, never switches on `BuildingSize` for dimensions.

## Assemblies

Decompiled source for all referenced libraries lives in `assembly/`:
- `assembly/S1MAPI_Il2Cpp/` — BuildingBuilder, PrefabPlacer, PrefabRef, Prefabs, NavigationBuilder, Materials
- `assembly/S1API.Il2Cpp.MelonLoader/` — GameLifecycle, Player, Money
- `assembly/Assembly-CSharp/` — game internals (DoorController, SaveManager, etc.)
- `assembly/Il2CppScheduleOne.Core/` — core game types
- `assembly/SteamNetworkLib-IL2Cpp/` — SteamNetworkClient, HostSyncVar

Native `Read` on `assembly/` is **blocked by hook** — use `ctx_read mode=signatures` or spawn the `lookup-assembly` skill instead.

## Reference repo

OTC-S1-Mod at `/home/imetto/projects/mods/OTC-S1-Mod/` is the functional reference. It has poor multiplayer hygiene — copy patterns, not MP plumbing. Use the `lookup-otc` skill for lookups.

## Operating rules

- **Tooling rules** — see `.claude/rules/` (lean-ctx, code-graph, collaboration, subagents).
- **Workflow** — see `docs/workflow.md` for research order, subagent usage, and compaction guidance.
- **Educational project** — explain before writing unless the user says "implement"/"do it"/"write it". See `.claude/rules/collaboration.md`.
