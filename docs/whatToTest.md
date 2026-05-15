# What To Test

Test as host first. Repeat the multiplayer sections with one client after the
host path is stable.

## App Shell

1. Load into `Main`.
2. Open the Mogul app.
3. Confirm the top-level tabs are:
   - `PROPERTIES`
   - `ORDERS`
   - `QUESTS`
4. Confirm `PROPERTIES` remains locked until the quest unlock flag exists.
5. Confirm `QUESTS` still opens by default on a fresh/progression save.

## Properties And Pins

1. Use an existing save with owned Mogul properties, or buy one.
2. Load into `Main`.
3. Open the map.
4. Confirm owned Mogul properties create map pins.
5. Track a Mogul quest from the `QUESTS` tab.
6. Confirm the active quest creates a map pin at its world position.
7. Change tracked quest.
8. Confirm the old quest pin is replaced by the new active quest pin.
9. In multiplayer, join as a client after host has owned properties.
10. Confirm the client sees the same property/active quest pins after sync.

Notes:

- Pins reuse the first vanilla property POI prefab found in the scene.
- If no vanilla `Property.PoI.UIPrefab` is found, pin objects may exist without a
  normal-looking icon.

## Walk-In Customers And Queue

1. Own a property and make sure its building is spawned.
2. Add sellable product to the property storage rack.
3. Spawn or wait for multiple walk-in customers.
4. Confirm customers browse inside before ordering:
   - storage rack
   - grow tent when a budtender is hired
5. Confirm more than one customer can be inside without immediately spilling
   everyone outside.
6. Confirm queued customers leave space around the doorway.
7. Confirm queued customers face the counter or the person ahead.
8. Confirm leaving customers do not block queue advancement permanently.
9. Confirm stuck customers are retried/despawned instead of freezing the queue.

## Counter Checkout UI

1. Have a customer reach the counter with no cashier hired.
2. Look at the counter and press `E` to take the order.
3. Confirm one centered gray checkout panel appears.
4. Confirm the panel shows:
   - product name
   - package quantity
   - line total
   - full order total
5. Press `W`.
   - UI should close.
   - Customer should keep waiting at the counter.
6. Reopen the order and press `Q`.
   - UI should close.
   - Customer should leave.
   - No sale should be added.
7. Spawn another customer and press `E`.
   - Stock should be removed.
   - Register balance should increase.
   - Customer should leave.

## Cashier Delay

1. Hire a cashier.
2. Let a customer reach the counter.
3. Confirm the customer does not instantly vanish/sell.
4. Confirm cashier fulfillment waits about 3-4.5 seconds.
5. Confirm the register balance increases after the delay.
6. Confirm the next queued customer advances after the serviced customer leaves.

## Pricing

1. Open `PROPERTIES` -> `MANAGE` on an owned location.
2. Put at least one sellable product in that location's storage rack.
3. Confirm `INVENTORY` shows:
   - store multiplier
   - product name
   - quality
   - quantity
   - base price
   - effective price
4. Press store `-0.1` and `+0.1`.
   - Store multiplier should change in 0.1 steps.
   - Effective prices should update.
5. Press per-item `-x` and `+x`.
   - Item multiplier should change in 0.1 steps.
   - Effective price should update from base price.
6. Press per-item `-$` and `+$`.
   - Item should switch to manual price.
   - Effective price should change in fixed dollar steps.
7. Press `AUTO`.
   - Manual price should clear.
   - Effective price should return to multiplier-based pricing.
8. Sell the item to a walk-in customer.
   - Checkout total should use the effective Mogul price.
9. Generate/fulfill an online order.
   - Online order total should use the effective Mogul price.
10. In multiplayer, change pricing on host and client.
    - Confirm synced app state settles to the host-authoritative value.

## Online Orders

1. Own a property.
2. Make sure the building is spawned.
3. Put sellable product in that property's storage rack.
4. Open Mogul app -> `ORDERS`.
5. Wait up to 90 seconds.
6. Confirm an order appears if demand accepts stocked products.
7. Confirm order cards show:
   - customer type
   - buyer name
   - location
   - deadline
   - requested items
   - total value
8. Press `DECLINE`.
   - Order should leave the open list.
9. Wait for another order.
10. Press `FULFILL`.
    - Required packages should be removed from storage.
    - Register balance should increase by order total plus tip.
    - Relevant `SellDrug` quest/task progress should increase.
11. Remove needed stock, then try `FULFILL`.
    - Fulfillment should fail.
    - Order should remain open.

Customer types to verify over enough generated orders:

- `Online Buyer`
- `Bulk Buyer`
- `Gang Leader`
- `Importer`

Higher-tier buyer types need enough reach before they become eligible.

## Employees And Grow

1. Hire a cashier.
   - Cashier NPC should spawn at the cashier anchor.
2. Hire a budtender.
   - Budtender NPC should spawn.
   - Grow tent should spawn if the location supports it.
3. Start a budtender strain order.
4. Confirm the order appears in the Manage `ORDERS`/`GROW` sections.
5. Let the ready day/time pass.
6. Complete the budtender order.
   - Product should try to insert into real storage.
   - If storage insert fails, product should fall back to virtual inventory.

## Custom Placement/Rebuild

1. Open `MANAGE` for an owned property.
2. Press `MOVE OBJECTS`.
3. Move desk, cashier, storage rack, and grow tent where available.
4. Save placement.
5. Rebuild/customize the property.
6. Confirm:
   - placed objects keep their saved transforms
   - queue cache clears after desk movement
   - employees respawn
   - grow tent respawns only when appropriate
   - storage pricing still resolves for the rebuilt rack

## Known Acceptable Issues

- Online orders are app-only for now: no drop-off point, expiry, delivery NPC, or
  per-order map pin yet.
- Pricing manual controls are +/- buttons, not typed text input yet.
- Inventory pricing list currently shows the first few stocked grouped products
  in the compact Manage panel.
- Map pins are local POI objects rebuilt from synced save data, not full
  S1API-backed journal quest entries.
