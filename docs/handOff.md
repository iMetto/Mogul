# Mogul Handoff

---

Task: Drop-off quest implementation — `shipment_westville_01`.

Done:

- `DropOffDrug` added to `MogulObjectiveEvent` enum.

- `shipment_westville_01` ("Dead Drop") quest added to `MogulQuestSystem.Quests`:
  - Available after `westville_statement` is claimed.
  - Target = 40 (OG Kush quantity, doubles as progress threshold).
  - TargetId = `EmployeeProduction.TestBudtenderProductId` ("ogkush") — used for both inventory lookup and event routing.
  - World position X=-158, Y=-3.13, Z=85 — **adjust with F5 in-game**.
  - `OnClaim = _ => MogulDropZoneSpawner.OnQuestClaimed("shipment_westville_01")` — triggers container despawn sequence.
  - 1200 Reach reward.

- `MogulDropZoneSpawner.cs` created (`Mogul/Systems/`):
  - `Tick()` wired into `MogulQuestSystem.Tick()`.
  - `DespawnAll()` wired into `Core.cs` Menu scene handler alongside `MogulQuestNpcSpawner.DespawnAll()`.
  - Spawns `Dumpster_Built` at `quest.WorldPosition` via `S1MAPI.Core.PrefabRef` + `Object.Instantiate` + `ServerManager.Spawn`.
  - After spawn: destroys `TrashContainerItem` component to prevent the vanilla trash-storage UI from intercepting player interaction.
  - Repurposes the dumpster's own `InteractableObject` (found via `GetComponentInChildren`) for the hover label — clears `onInteractStart`/`onInteractEnd` listeners, sets message. This works because the dumpster mesh children already have colliders; the hover system picks it up naturally.
  - Hover label updates live each frame: shows `[Q] Deposit 40 OG Kush (X in hand)` when player has enough, or `Need 40 OG Kush — you have X` when short.
  - Q trigger is proximity-based (`S1API.Entities.Player.Local?.Position` within 2.5 m) — does not rely on hover/raycast, so it fires reliably regardless of look direction.
  - On insufficient items: `NotificationsManager.SendNotification(...)` shows a native in-game popup.
  - On successful deposit: items consumed slot-by-slot via `TryCast<ProductItemInstance>()` + `TryCast<ProductDefinition>()`, event recorded via `RequestRecordEvent(DropOffDrug, itemId, 40)`, success notification shown.
  - On claim (via app): `OnQuestClaimed` schedules `GameObject` destroy + network despawn in **3 seconds** via `_pendingDestroy` timer dict checked each tick.

- 18/18 existing tests still pass. No new runtime-dependent tests added (spawner lifecycle is game-engine-bound).

Files touched:

- `Mogul/Core.cs` — `MogulDropZoneSpawner.DespawnAll()` on Menu scene load
- `Mogul/Systems/MogulQuestSystem.cs` — enum value, quest definition, `Tick()` wire-up
- `Mogul/Systems/MogulDropZoneSpawner.cs` (new)

Verification needed:

- Confirm dumpster spawns at the marked world position when `westville_statement` is claimed.
- Confirm `TrashContainerItem` destruction suppresses the vanilla trash UI.
- Confirm hover label shows and updates live with inventory count.
- Confirm Q fires deposit when within 2.5 m.
- Confirm `NotificationsManager` popup appears for both insufficient items and successful deposit.
- Confirm quest progress reaches 40/40 and "CLAIM REWARD" appears in app.
- Confirm container despawns 3 seconds after claim.
- Adjust world position with F5 as needed.

Next:

1. Fine-tune dumpster world position with F5.
2. Validate full loop: spawn → Q deposit → claim → 3s despawn.
3. Once confirmed, extend `MogulDropZoneSpawner` to support multiple drop-off quests generically (no quest-Id hardcoding in `OnClaim`).
4. Consider adding a map pin for the drop zone (mirrors NPC quest pin pattern).
5. Research `onInteractStart` to see if it fires on E-key — could replace proximity trigger with hover + key for tighter UX.

---

Task: Quest system polish and NPC quest target infrastructure.

Done:

- Quest tab untrack support added:
  - "Track Quest" button now toggles. When already tracking, button shows "UNTRACK" and clears `ActiveQuestId`.
  - `MogulQuestSystem.RequestUntrack()` sends `SetActiveQuest` with an empty payload.
  - `MogulNetwork.ApplyAction` updated to treat empty `SetActiveQuest` payload as a clear.
  - Button color changes to muted when untracking is available.

- Quest/Task terminology unified:
  - "TASKS" section header removed. Tasks and quests now appear together under one "QUESTS" header.
  - "TRACKING" button label replaced with "UNTRACK".

- `CustomerSpawner.cs` renamed to `NpcSpawner.cs`, class renamed `NpcSpawner`. All 12 call sites updated across `CustomerManager.cs`, `EmployeeSystem.cs`, `LocationGeometry.cs`.

- `MogulQuestNpc.cs` created — owns all quest NPC lifecycle logic:
  - `MogulQuestNpcSpawner` static class with `Tick()`, `Despawn()`, `DespawnAll()`.
  - `Tick()` (called from `MogulQuestSystem.Tick()`) spawns quest NPCs when available and not claimed, despawns when claimed or unavailable.
  - On `NPCHealth.onDieOrKnockedOut`: progress recorded via `RequestRecordEvent`, despawn scheduled 10 seconds later via `_pendingDespawn` dict checked each tick.
  - `_recorded` HashSet prevents double-firing per session.
  - `DespawnAll()` called on Menu scene load for clean teardown.

- `NpcSpawner.SpawnQuestNpc` added:
  - Same clone/NavMesh/network-spawn sequence as customers, no `Customer` component.
  - `npc.ID` set to `npcId` before `ServerManager.Spawn`.
  - Deterministic appearance via `ApplyAppearance(npc, npcId.GetHashCode())`.

- Test quest `loose_end_01` added to `MogulQuestSystem.Quests`:
  - Always available (`IsAvailable = _ => true`), `KnockoutNpc` event, `TargetId = "loose_end_mark_01"`.
  - World position X=-148, Y=-3.13, Z=68 — **adjust with F5 in-game**.
  - 300 Reach reward. Map pin appears at world position when quest is tracked.

- Background images fixed: `docs/ordersLandscape.png` and `docs/questsLandscape.png` had swapped content on disk — renamed to correct assignment.

Files touched:

- `Mogul/Core.cs`
- `Mogul/Apps/MogulApp.QuestsTab.cs`
- `Mogul/Systems/MogulQuestSystem.cs`
- `Mogul/Systems/MogulNetwork.cs`
- `Mogul/Systems/MogulQuestNpc.cs` (new)
- `Mogul/Systems/NpcSpawner.cs` (renamed from `CustomerSpawner.cs`)
- `Mogul/Systems/CustomerManager.cs`
- `Mogul/Systems/EmployeeSystem.cs`
- `Mogul/Systems/LocationGeometry.cs`

Verification needed:

- Confirm `NPCHealth.onDieOrKnockedOut` is a plain `UnityEvent` (no generic args) — listener uses `new Action(...)`. If it fails to compile, check the exact signature in `assembly/Assembly-CSharp/Il2CppScheduleOne/NPCs/NPCHealth.cs`.
- Confirm test quest NPC spawns and is visible at world position.
- Confirm map pin appears when quest is tracked.
- Confirm progress fires on true knockout, not first hit.
- Confirm NPC despawns 10 seconds after knockout.
- Confirm "CLAIM REWARD" appears post-knockout and claim works.
- Confirm "UNTRACK" clears the active quest and removes the map pin.

Next:

1. Adjust `loose_end_01` world coordinates using F5 to find a valid open-area position.
2. Validate full quest loop: spawn → track → kill → 10s despawn → claim reward.
3. Add more kill quests once the test quest is confirmed.
4. Consider `DumpBody` quest NPC support — body position check in `Tick` against `quest.WorldPosition` + `quest.Radius`.
5. Pawn shop and pickpocket hook research still pending (see `quest_task_hooks.md`).

---

Previous session — Walk-in customer QA/fix pass:

Task: Continue QA/fix pass on walk-in customers, checkout interaction, and Manage inventory refresh.

Done:

- Walk-in customer flow was rebuilt around a stricter indoor pipeline:
  - Up to 3 customers can be indoors.
  - Indoor customers reserve browse slots.
  - Outside queue customers do not go directly to the counter.
  - The lowest active ready browser becomes the next counter customer.
  - When a counter customer leaves, one outside customer can enter to browse.
- Physical browse reservations were added:
  - 3 reserved rack-facing browse spots.
  - 2 reserved grow-tent-facing browse spots.
  - Fallback indoor browse spots if rack/tent anchors are unavailable or not walkable.
  - A browse spot cannot be assigned to more than one active customer.
- Browsing was simplified:
  - Customers now walk to one reserved browse spot, wait, then become ready to order.
  - Browse timeout no longer despawns the customer; it marks them ready to order.
  - Browse arrival has a proximity fallback if S1MAPI misses the callback.
- Queue maintenance was added:
  - A periodic flow maintenance tick reruns queue advancement every 2 seconds.
  - This is intended to recover missed callbacks / ready customers that stand still.
- Counter/customer interaction changed:
  - Counter `E` prompt is disabled.
  - Synthetic customer gets a Mogul-only child `InteractableObject`.
  - Player takes a manual order by looking at the customer and pressing `Q`.
  - Vanilla NPC `intObj` is disabled for synthetic customers to suppress vanilla sell/conversation prompts.
- Checkout camera behavior changed:
  - Opening checkout focuses/pans toward the customer.
  - Player movement/look is locked while checkout UI is open.
- Counter standing anchor changed:
  - Customer counter position is derived from the cashier/staff anchor.
  - Customer stands 1.5f in front of where the cashier faces.
- Empty inventory handling changed:
  - New/pre-counter customers short-circuit when no sellable stock exists.
  - They are routed through normal leaving behavior instead of being hard-despawned.
  - This avoids destroying fresh NPCs while vanilla avatar/equipment coroutines are still active.
- Manage inventory refresh changed:
  - Open Manage panels refresh live every 0.75s while visible.
  - Inventory no longer requires backing out and re-entering Manage to update.
- Drug mix naming changed:
  - Mogul mix naming now uses vanilla `NewMixScreen.GenerateUniqueName` instead of `ogkush mix 1` style names.

Files touched in the latest passes:

- `Mogul/Core.cs`
- `Mogul/Apps/MogulApp.cs`
- `Mogul/Systems/CheckoutHandler.cs`
- `Mogul/Systems/CustomerManager.cs`
- `Mogul/Systems/CustomerSpawner.cs`
- `Mogul/Systems/QueueSlots.cs`
- `Mogul/Systems/SellDesk.cs`
- `Mogul/Systems/StrainMixingSystem.cs`

Verification:

- `dotnet build Mogul/Mogul.csproj` passes.
- `dotnet run --project Mogul.Tests/Mogul.Tests.csproj` passes, 18/18.
- Full solution build was not used; previous sessions noted solution-level assembly/langversion issues.
- Git commands were intentionally not used because the repo structure is currently unreliable.

Current manual QA result:

- Customer flow is a massive improvement.
- Most customers now browse/order/leave correctly.
- One out of roughly five can still get stuck in live testing.
- When inventory reaches zero, customers should now leave normally instead of hard despawning; this needs retest.
- Previous hard-despawn no-stock behavior produced Unity null refs in `AvatarEquippable.InitializeAnimation`; latest change should reduce/avoid that by using normal leaving.

Next:

1. Retest walk-ins with stocked inventory:
   - Spawn 5-10 customers.
   - Confirm no two customers stand on the same rack/tent browse spot.
   - Confirm only one customer goes to the counter at a time.
   - Confirm ready browsers do not stand forever after the counter clears.
2. Retest inventory depletion:
   - Let customers buy until stock reaches zero.
   - Confirm remaining pre-counter customers say no stock and physically leave.
   - Confirm no `AvatarEquippable.InitializeAnimation` null refs.
   - Confirm F6 can spawn again after restocking.
3. If a customer still gets stuck:
   - Capture state from log: `Browsing`, `WalkingIn`, `Leaving`, or queue state.
   - Note whether they are inside, outside, or at the doorway.
   - Prefer degrading stuck `Browsing`/`WalkingIn` into ready-to-order or leaving before despawning.
4. If S1MAPI interior routing remains unreliable:
   - Use S1MAPI only for entering/exiting the building.
   - Use reserved world-space anchors plus vanilla `NPCMovement.SetDestination` or controlled warp fallback for indoor browse/counter movement.

Open:

- Customer pathing is improved but still needs live soak testing.
- Door/counter geometry still depends on placement and may need visual debug markers for anchors.
- Empty-inventory behavior needs a fresh test after replacing hard despawn with normal leaving.
- Budtender grow/order UI still needs gameplay validation after hiring a budtender.
- Customer type visibility still needs UX planning.

