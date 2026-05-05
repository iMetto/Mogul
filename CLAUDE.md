## What this mod is

Mogul is a Schedule One mod that adds an online-reach economy on top of the vanilla game without touching vanilla mechanics. Players grow an online following by completing antisocial quests. Higher reach unlocks more synthetic NPC customers, bigger online orders, and access to more properties.

**Core loop:** reach → more customers → more income → more properties → higher reach quests.

**Properties** are buy-and-staff sales locations. Income comes from walk-in synthetic NPCs (spawned on demand, never from the vanilla pool) and online orders. Players can man the counter themselves or hire NPC staff.

**Phase roadmap:**
- Phase 1 — properties, designs, customer spawn, demand/checkout system (✅ in progress)
- Phase 2 — online reach + quests, player-set pricing, effect scoring
- Phase 3 — online orders from high-spenders, employee hiring
- Phase 4 — shipment quests, car deliveries, cops/cartel pursuit

## Location Architecture

To add a new location, add one entry to `PropertySystem.Catalog` in `PropertySystem.cs`. That's it.

Each `MogulLocation` carries all its own data:
- `WorldPosition` — world coords. Use F5 in-game to log player position, F6 for a corner.
- `BuildingSize` — Small / Medium / Large. Room dimensions derived automatically in `MogulLocation.RoomSize`.
- `Door` (`WallSide`) — which wall the door faces.

`LocationSpawner` is dimension-agnostic — it reads `location.RoomSize`, never switches on `BuildingSize`.

## Assemblies

Decompiled source for all referenced libraries lives in `assembly/`:
- `assembly/S1MAPI_Il2Cpp/` — BuildingBuilder, PrefabPlacer, PrefabRef, Prefabs, NavigationBuilder, Materials
- `assembly/S1API.Il2Cpp.MelonLoader/` — GameLifecycle, Player, Money
- `assembly/Assembly-CSharp/` — game internals (DoorController, SaveManager, etc.)
- `assembly/Il2CppScheduleOne.Core/` — core game types
- `assembly/SteamNetworkLib-IL2Cpp/` — SteamNetworkClient, HostSyncVar

Use `ctx_read mode=signatures` for assembly API lookups.

## Tools

Use `ctx_read`, `ctx_search`, `ctx_shell`, `ctx_tree` (lean-ctx MCP) instead of native Read/Grep/Bash/ls.
Use `semantic_search_nodes`, `query_graph`, `get_impact_radius` (code-review-graph MCP) before editing any `.cs` file.

## Reference repo

OTC-S1-Mod at `/home/imetto/projects/mods/OTC-S1-Mod/` is the functional reference. Copy patterns, not MP plumbing.
