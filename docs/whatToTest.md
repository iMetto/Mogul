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


Result: No growtent spawned. Quest works, spawning works, inventory works. After customising
building the npc vacate and respawn but get stuck at the door when trying to enter..
Inventory gets wiped (not good). so both app and storage wiped of inventory. 

logs:
[12:33:45.196] [Il2CppInterop] Registered mono type S1MAPI.Building.InteriorNavigator in il2cpp domain
[12:33:45.199] [Mogul] [Mogul] SpawnDesk: loc_westville_01 pos=(10.00, 0.00, 6.50) rot=(0.00, 180.00, 0.00)
[12:33:45.210] [Mogul] [Mogul] Counter created for loc_westville_01. QueueAnchor=(10.00, 0.00, 6.20)
[12:33:45.237] [Mogul] [Mogul] Sell desk ready for loc_westville_01
[12:33:45.237] [Mogul] [Mogul] Spawned Westville Corner at (-171.44, -3.00, 70.00)
[12:33:45.241] [Mogul] [Mogul] Debug: purchased loc_westville_01, teleporting to bungalow
[12:33:47.167] [PhoneApp] Opened phone app: MogulApp
[12:34:07.568] [Mogul] [Mogul] CivilianNPC prefab found at index 126
[12:34:07.584] [Mogul] [Mogul] Spawned worker 9000 at (-159.14, -3.84, 75.60)
[12:34:18.357] [Mogul] [Mogul] Spawned worker 9001 at (-169.25, -3.99, 78.30)
[12:34:48.152] [Mogul] [Mogul:EffectName] product=ogkush effect[0]=Calming
[12:35:14.263] [Mogul] [Mogul] Debug: purchased loc_westville_01, teleporting to bungalow
[12:35:21.969] [Mogul] [Reach] Set to 10K → Local  normal $100–$350  outlier (20%) up to $700
[12:35:25.343] [Mogul] [Mogul] ServerManager.Despawn failed: Object reference not set to an instance of an object.
[12:35:25.344] [Mogul] [Mogul] ServerManager.Despawn failed: Object reference not set to an instance of an object.
[12:35:25.784] [Mogul] [Mogul] Rack ready for loc_westville_01: StorageRack_Large(Clone) | components: Transform, MonoBehaviour, MonoBehaviour, NetworkObject, MonoBehaviour, MonoBehaviour, MonoBehaviour, MonoBehaviour, Component
[12:35:25.788] [Mogul] [Mogul] SpawnDesk: loc_westville_01 pos=(10.00, 0.00, 6.50) rot=(0.00, 180.00, 0.00)
[12:35:25.788] [Mogul] [Mogul] Counter created for loc_westville_01. QueueAnchor=(10.00, 0.00, 6.20)
[12:35:25.790] [Mogul] [Mogul] Sell desk ready for loc_westville_01
[12:35:25.796] [Mogul] [Mogul] Spawned worker 9002 at (-159.14, -3.84, 75.60)
[12:35:25.804] [Mogul] [Mogul] Spawned worker 9003 at (-169.25, -3.99, 78.30)
[12:35:25.805] [Mogul] [Mogul] Spawned Westville Corner at (-171.44, -3.00, 70.00)
[12:35:29.019] [Mogul] [Reach] Set to 250K → Rising  normal $200–$600  outlier (20%) up to $1200
[12:35:38.622] [Mogul] [Reach] Set to 1M → Established  normal $400–$1000  outlier (20%) up to $2000
[12:35:41.809] [Mogul] [Mogul] ServerManager.Despawn failed: Object reference not set to an instance of an object.
[12:35:41.810] [Mogul] [Mogul] ServerManager.Despawn failed: Object reference not set to an instance of an object.
[12:35:42.184] [Mogul] [Mogul] Rack ready for loc_westville_01: StorageRack_Large(Clone) | components: Transform, MonoBehaviour, MonoBehaviour, NetworkObject, MonoBehaviour, MonoBehaviour, MonoBehaviour, MonoBehaviour, Component
[12:35:42.188] [Mogul] [Mogul] SpawnDesk: loc_westville_01 pos=(10.00, 0.00, 6.50) rot=(0.00, 180.00, 0.00)
[12:35:42.188] [Mogul] [Mogul] Counter created for loc_westville_01. QueueAnchor=(10.00, 0.00, 6.20)
[12:35:42.232] [Mogul] [Mogul] Sell desk ready for loc_westville_01
[12:35:42.242] [Mogul] [Mogul] Spawned worker 9004 at (-159.14, -3.84, 75.60)
[12:35:42.254] [Mogul] [Mogul] Spawned worker 9005 at (-169.25, -3.99, 78.30)
[12:35:42.256] [Mogul] [Mogul] Spawned Westville Corner at (-171.44, -3.00, 70.00)