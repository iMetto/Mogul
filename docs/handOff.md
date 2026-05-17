# Mogul Handoff

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

