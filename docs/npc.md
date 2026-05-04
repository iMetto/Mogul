# Mogul Phase 3 — NPC Customer Reference

All findings from OTC source code + S1API/S1MAPI assembly research.
Use this when starting a new session to avoid re-reading every file.

---

## 1. The NPC Prefab

**Prefab name:** `"CivilianNPC"`  
**How to find it:** `networkManager.SpawnablePrefabs` (NOT `GetPrefabObjects<PrefabObjects>(0)` — different accessor, different collection).

```csharp
var nm = InstanceFinder.NetworkManager;
var spawnablePrefabs = nm.SpawnablePrefabs;   // <-- this, not GetPrefabObjects
int count = spawnablePrefabs.GetObjectCount();
for (int i = 0; i < count; i++)
{
    var obj = spawnablePrefabs.GetObject(true, i);
    if (obj?.gameObject?.name == "CivilianNPC") { ... }
}
```

The prefab contains: `NPC` component, `NavMeshAgent`, `Avatar`, `VoiceOverEmitter`, `DialogueHandler`, `NetworkObject`.  
It does NOT have a `Customer` component — that must be added manually before activation.

---

## 2. Spawning a Customer NPC (OTC NpcSpawner pattern)

Full sequence (host only, `IsHost` guard required):

```csharp
// 1. Clone prefab (while inactive — must be inactive before AddComponent)
var clone = UnityEngine.Object.Instantiate(basePrefab);
clone.gameObject.name = "Mogul_Customer_" + id;
clone.gameObject.SetActive(false);

// 2. Parent to NPC container
var npcManager = NetworkSingleton<NPCManager>.Instance;
if (npcManager?.NPCContainer != null)
    clone.gameObject.transform.SetParent(npcManager.NPCContainer, false);

// 3. Get NPC component, set identity
var npc = clone.gameObject.GetComponent<NPC>();
npc.ID = id;
npc.FirstName = firstName;
npc.LastName = lastName;

// 4. Remove from auto-registered NPC list (we manage lifecycle manually)
NPCManager.NPCRegistry?.Remove(npc);

// 5. Snap to NavMesh
if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 10f, NavMesh.AllAreas))
    spawnPos = hit.position;
clone.gameObject.transform.position = spawnPos;

// 6. Add Customer component BEFORE activation
var customerData = ScriptableObject.CreateInstance<CustomerData>();
customerData.CanBeDirectlyApproached = false;
customerData.CallPoliceChance = 0f;
customerData.DependenceMultiplier = 0f;
var customer = clone.gameObject.AddComponent<Customer>();
customer.SetCustData(customerData);
customer.enabled = false;   // prevents subscription to vanilla TimeManager events

// 7. Activate (triggers Awake/Start)
clone.gameObject.SetActive(true);

// 8. Isolate from vanilla deal system
Customer.UnlockedCustomers?.Remove(customer);
Customer.LockedCustomers?.Remove(customer);

// 9. Network-spawn (required for movement/rendering to work)
InstanceFinder.ServerManager.Spawn(clone);

// 10. Warp + face direction
npc.Movement?.Warp(spawnPos);
npc.Movement?.Stop();
npc.Movement?.FaceDirection(rotation * Vector3.forward);
npc.Movement?.SetAgentType(NPCMovement.EAgentType.Humanoid);

// 11. Enable off-mesh link traversal (required for stairs)
var agent = npc.gameObject.GetComponent<NavMeshAgent>();
if (agent != null) agent.autoTraverseOffMeshLink = true;
```

---

## 3. Random Appearance Generation

Each customer gets a deterministic random look from a seed. Same seed = same look (store seed for save/load consistency).

**Appearance is NOT all the same.** Variables per customer:
- Gender (float 0–1, ≥0.5 = female)
- Skin tone (8 tones from very light to very dark)
- Height (0.9–1.1), Weight (0.3–0.7)
- Hair style (10 male styles, 11 female styles, separate pools)
- Hair color (natural tones: dark brown, medium brown, light brown, red)
- Shirt (6 options), Pants (3 options), Shoes (4 options)
- Face expression (5 options, required to prevent black face)

**Hair style resource paths (examples):**
```
Avatar/Hair/buzzcut/BuzzCut
Avatar/Hair/bun/Bun
Avatar/Hair/afro/Afro
```
**Clothing resource paths (examples):**
```
Avatar/Layers/Top/T-Shirt
Avatar/Layers/Bottom/Jeans
Avatar/Accessories/Feet/Sneakers/Sneakers
Avatar/Layers/Face/Face_Neutral   ← REQUIRED, prevents black face
```

**Key API:**
```csharp
var settings = npc.Avatar.CurrentSettings ?? ScriptableObject.CreateInstance<AvatarSettings>();
settings.Gender = gender;
settings.SkinColor = skinTone;
settings.HairPath = hairPath;
// ... populate FaceLayerSettings, BodyLayerSettings, AccessorySettings (Il2CppList)
npc.Avatar.LoadAvatarSettings(settings);
```

---

## 4. Voice Setup

Borrow a voice database from `EmployeeManager` — clean, doesn't steal from specific NPCs.

```csharp
var empMgr = NetworkSingleton<EmployeeManager>.Instance;
bool isMale = gender < 0.5f;
var voiceDb = empMgr.GetVoice(isMale, Math.Abs(seed % 100));
npc.VoiceOverEmitter.SetDatabase(voiceDb, true);

// Vary pitch slightly
float basePitch = isMale ? 0.8f : 1.3f;
float offset = -0.1f + Mathf.Clamp01((seed % 10) / 10f) * 0.2f;
npc.VoiceOverEmitter.PitchMultiplier = basePitch + offset;
```

**Play voice lines:**
```csharp
npc.VoiceOverEmitter?.Play(EVOLineType.Greeting);   // on arrive at counter
npc.VoiceOverEmitter?.Play(EVOLineType.Thanks);     // on successful sale
npc.VoiceOverEmitter?.Play(EVOLineType.Annoyed);    // on no stock / leave empty
npc.VoiceOverEmitter?.Play(EVOLineType.Angry);      // if dismissed rudely
```

**Speech bubble:**
```csharp
npc.DialogueHandler?.WorldspaceRend?.ShowText("Anything good today?", 3f);
```

**Animation triggers:**
```csharp
npc.SendAnimationTrigger("ThumbsUp");
npc.SendAnimationTrigger("Nod");
npc.SendAnimationTrigger("GrabItem");
npc.SendAnimationTrigger("ConversationGesture1");
```

---

## 5. Routing NPCs Into Buildings

`NavigationBuilder` (from `LocationSpawner`) handles doorway approach, stair lerp, and interior pathfinding.

```csharp
// Already built in LocationSpawner.SpawnBuilding:
builder.CreateNavigationBuilder().Build();

// We need to store the NavigationBuilder reference on the SellDeskInstance
// (currently it's discarded after Build() is called — needs to be saved)

// Route NPC to a local-space position inside the building:
navBuilder.SendNPCToPosition(npc, localQueueAnchor, onArrival: () =>
{
    // NPC has arrived at queue anchor
    npc.VoiceOverEmitter?.Play(EVOLineType.Greeting);
    npc.DialogueHandler?.WorldspaceRend?.ShowText("Hey.", 2f);
});

// Route NPC out after sale:
navBuilder.RecallNPC(npc);   // sends them back through the door

// Convert world position to local (for SendNPCToPosition):
Vector3 localTarget = navBuilder.WorldToLocal(worldQueueAnchor);
```

**IMPORTANT:** `NavigationBuilder` must be stored — it's currently built and discarded in `LocationSpawner`. `SellDeskInstance` or a new `LocationRuntime` class needs to hold it.

---

## 6. Customer State Machine (Mogul simplified)

OTC has 8 states. Mogul needs 6:

```
WalkingToStore     → spawned outside, NavMesh walking to building exterior
EnteringBuilding   → NavigationBuilder.SendNPCToPosition(queueAnchor)
WaitingAtCounter   → arrived, plays Greeting voice, waits for interaction
InTransaction      → player/employee interacted with register, checkout underway
LeavingBuilding    → NavigationBuilder.RecallNPC()
Despawning         → outside, walk to despawn point, Destroy
```

One customer per location at a time for Phase 3. Queue of pending arrivals can be added in Phase 4.

---

## 7. The Transaction / Checkout Flow

**OTC's model (complex):** Camera pan, skill check bar, sprite-click HUD per item, physical cash fly animation, register accumulates balance, DealCompletionPopup.

**Mogul's Phase 3 model (implemented):**

The **counter's InteractableObject** is the trigger — player presses E on the counter when a customer is waiting.

Implemented flow:
1. Customer arrives at counter → `CustomerManager.OnArrived` scans storage immediately. If empty, NPC leaves annoyed. If stock found, shelf glows green and NPC waits.
2. Player presses E on counter → `CheckoutHandler.Open(locationId, npc, buildingRoot)` scans storage fresh.
3. `CheckoutUI` (IMGUI overlay) shows all available products sorted **alphabetically**, with quality label, price, and stock count. Number keys 1–9 or mouse click to select. Escape to cancel.
4. Player selects a product → `CheckoutHandler.Sell(product)` removes 1 package from the first matching storage slot, pays player via `S1API.Money.Money.ChangeCashBalance`, NPC plays Thanks voice line and leaves.
5. Cancel (Escape) → NPC plays Annoyed and leaves.

**Phase 4** is where the NPC gets a specific *want*: a preferred product, quality expectation, and budget. The player will still see a list, but the NPC's request will be visible and selecting the wrong product will score differently. See `otc_customer_research.md` for the full demand simulation design.

**No camera pan, no skill check, no cash fly animation for Phase 3.** Those are polish for later.

---

## 8. Finding and Removing Items From Storage

**Scan all storage in the building:**
```csharp
var allStorage = buildingRoot.GetComponentsInChildren<StorageEntity>(true);
foreach (var storage in allStorage)
{
    foreach (var slot in storage.ItemSlots)
    {
        if (slot.ItemInstance == null) continue;
        var product = slot.ItemInstance.TryCast<ProductItemInstance>();
        if (product == null) continue;
        var def = product.Definition.TryCast<ProductDefinition>();
        // def.ID = product ID string, slot.Quantity = package count
        // product.AppliedPackaging.Quantity = units per package
    }
}
```

**Remove one package from a slot:**
```csharp
if (slot.Quantity <= 1)
    slot.ClearStoredInstance(false);   // false = don't notify (avoids network spam)
else
    slot.ChangeQuantity(-1);
```

**Insert remainder back (for partial package splits — Phase 4):**
```csharp
var newInstance = prodDef.GetDefaultInstance(1);
newInstance.TryCast<QualityItemInstance>()?.SetQuality((EQuality)qualityLevel);
slot.InsertItem(newInstance);
```

---

## 9. Payment

**No per-NPC wallet.** Money goes straight to the player. No vanilla deal system involved.

```csharp
// Add cash to player:
S1API.Money.Money.ChangeCashBalance(amount, visualizeChange: true, playCashSound: true);

// Or play cash sound separately:
NetworkSingleton<NativeMoneyManager>.Instance?.PlayCashSound();
```

**Mogul twist:** also store earnings in `MogulSaveData` per-location for the phone app dashboard:
```csharp
MogulNetwork.RequestAction(MogulActions.RecordSale, $"{locationId}:{amount}");
```
(This action doesn't exist yet — needs to be added to `MogulNetwork.ApplyAction` and `MogulSaveData`.)

---

## 10. Despawning

```csharp
public static void Despawn(NPC npc)
{
    try { NPCManager.NPCRegistry?.Remove(npc); } catch { }
    var netObj = npc.gameObject?.GetComponent<NetworkObject>();
    if (netObj != null && InstanceFinder.ServerManager != null && netObj.IsSpawned)
        InstanceFinder.ServerManager.Despawn(netObj);
    else
        UnityEngine.Object.Destroy(npc.gameObject);
}
```

---

## 11. Things That Must Change in Existing Code

### LocationSpawner.cs
- **Store the `NavigationBuilder`** — currently built and discarded. Must be returned from `SpawnBuilding` and stored somewhere accessible (e.g. a `static Dictionary<string, NavigationBuilder>` in `LocationSpawner`, or on a new `LocationRuntime` class).
- Expose `TryGetNavBuilder(string locationId, out NavigationBuilder nb)`.

### SellDeskInstance (SellDesk.cs)
- Add `public NavigationBuilder NavBuilder;` field so customer routing can access it.
- OR reference `LocationSpawner.TryGetNavBuilder(location.Id, ...)`.

### MogulSaveData.cs
- Add `Dictionary<string, float> LocationEarnings` for per-location cash tracking.

### MogulNetwork.cs
- Add `MogulActions.RecordSale` case.

---

## 12. New Files Created for Phase 3

| File | Purpose |
|------|---------|
| `Mogul/Systems/CustomerSpawner.cs` | Clone + configure CivilianNPC, random appearance, voice setup, despawn |
| `Mogul/Systems/CustomerManager.cs` | Per-location customer lifecycle: spawn timer, state machine tick, NavigationBuilder routing |
| `Mogul/Systems/StorageScanner.cs` | Scan all StorageEntity in a building root; return `List<StorageProduct>` sorted alphabetically; `TakeOne` for removal |
| `Mogul/Systems/CheckoutHandler.cs` | Single-session state machine: Open / Sell / Cancel; fires `OnClosed` event |
| `Mogul/UI/CheckoutUI.cs` | IMGUI overlay: alphabetical product list, number key shortcuts 1–9, Escape to cancel |

---

## 13. Design Decisions (Resolved)

1. **Player chooses what to sell.** ✅ The checkout UI shows all products currently on the shelves sorted alphabetically. The player picks one to sell. Phase 4 adds NPC demand: the NPC will specify a preferred product, quality, and budget (see `otc_customer_research.md` §7).
2. **One customer at a time per location.** ✅ Queue system exists for future expansion but only one NPC reaches the counter at a time.
3. **Employee auto-checkout deferred.** ✅ Will be implemented alongside the employee hiring system. Code is well-structured so `CheckoutHandler.Sell` can be called from an employee NPC instead of the player with minimal changes.

---

## 14. Assembly Locations

```
assembly/S1MAPI_Il2Cpp/S1MAPI/Building/NavigationBuilder.cs   ← SendNPCToPosition, RecallNPC, WorldToLocal
assembly/S1MAPI_Il2Cpp/S1MAPI/Building/InteriorNavigatorCore.cs ← internal routing (don't call directly)
assembly/S1API.Il2Cpp.MelonLoader/S1API/Entities/NPC.cs        ← NPC base class, Movement, VoiceOverEmitter
assembly/S1API.Il2Cpp.MelonLoader/S1API/Entities/NPCMovement.cs ← Warp, SetDestination, FaceDirection
assembly/S1API.Il2Cpp.MelonLoader/S1API/Storage/StorageEntity.cs ← S1API wrapper
assembly/Assembly-CSharp/Il2CppScheduleOne/Storage/StorageEntity.cs ← raw Il2Cpp (use for slot access)
assembly/Assembly-CSharp/Il2CppScheduleOne/Economy/Customer.cs  ← UnlockedCustomers, LockedCustomers
OTC-S1-Mod/OverTheCounter/Logic/NpcSpawner.cs                  ← full spawn blueprint
OTC-S1-Mod/OverTheCounter/Logic/CustomerInstance.cs            ← full state machine blueprint
OTC-S1-Mod/OverTheCounter/Logic/CheckoutProcess.cs             ← full checkout blueprint (3,500 lines)
```

---

## 15. What Phase 3 Is NOT (deferred)

- Camera pan / cinematic lock during checkout
- Skill check minigame
- Physical cash fly animation
- DealCompletionPopup
- Per-NPC product preferences / quality expectations
- Browsing shelves (walking around the store)
- Queue of multiple customers
- Employee auto-checkout
- Per-location earnings dashboard in the phone app (data model yes, UI no)
