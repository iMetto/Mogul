# What To Test

## Employee, Inventory, And Grow Placeholder Slice

Test in a normal loaded save as host first.

1. Open the Mogul app and buy or use an owned property.
2. Confirm the app shows two top-level tabs: `PROPERTIES` and `QUESTS`.
3. Open `PROPERTIES`, press `MANAGE` on an owned property, and confirm the Manage page still opens.
4. Hire a cashier.
   - A cashier NPC should spawn inside the property.
   - Walk-in customers should auto-complete checkout.
   - Register balance should increase after sales.
5. Hire a budtender.
   - A budtender NPC should spawn inside the property.
   - A simple grow tent/plant placeholder should appear inside the property.
   - The Manage `GROW` section should show OG Kush test grow status.
6. Let at least one game day elapse with a budtender hired.
   - Logs should show budtender OG Kush production.
   - Manage inventory should show virtual OG Kush stock.
7. Put real sellable product into the property storage rack.
   - Manage `INVENTORY` should show product name, quality, package count, and price.
   - If more than six grouped products exist, the app should show a `+ more` line instead of overflowing badly.
8. Rebuild/customize the property if possible.
   - Existing employee NPCs should be evicted and respawned.
   - The grow tent placeholder should be recreated only if a budtender is still hired.
   - Manage inventory should still scan the current storage objects.
9. Switch to the `QUESTS` tab.
   - Quest cards should show progress based on current save state.
   - Completed requirements should enable the claim button.
   - Claiming should add reach once and mark the quest completed.

Known acceptable issues right now:

- `ServerManager.Despawn failed` logs can still appear from the older NPC despawn path.
- The cashier can stand on the wrong side of the counter.
- Budtender output is virtual stock only; it is not inserted into real storage yet.
- The grow tent is a primitive placeholder, not a real buildable grow tent.
