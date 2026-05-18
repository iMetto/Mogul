# Drop-Off Quest Research
Date: 2026-05-18 (updated after live implementation)

Drop-off quests let a contact commission a bulk drug delivery to a specific map location.
The player brings the required product to a spawned dumpster and deposits it via Q press.
This doc covers the full working technical path, including real gotchas found during implementation.

---

## Why Vanilla Storage Does Not Work Directly

Three vanilla container systems were evaluated:

### TrashContainerItem (Dumpster_Built, TrashCan_Built)
- Component: `Il2CppScheduleOne.ObjectScripts.TrashContainerItem : GridItem`
- Fields: `TrashContainer Container`, `List<ItemSlot> InputSlots/OutputSlots`, `IsAcceptingItems`
- Purpose: trash bags for the Cleaner NPC system (`BagTrashCanBehaviour`)
- **Cannot** be opened by the player as a standard storage UI
- Slots accept `TrashItem`/`TrashBag`, not arbitrary drug items

### WorldStorageEntity (Dead Drops)
- Component: `Il2CppScheduleOne.Storage.WorldStorageEntity : StorageEntity`
- Used by `Il2CppScheduleOne.Economy.DeadDrop` — pre-placed map containers
- Has `static List<WorldStorageEntity> All`, `onContentsChanged`, persistence/GUID
- Dead drops are baked into the map; dynamic runtime placement is not supported
  without setting up the full persistence/GUID pipeline

### PlaceableStorageEntity (Safe, FilingCabinet, WallMountedShelf…)
- Component: `Il2CppScheduleOne.ObjectScripts.PlaceableStorageEntity : GridItem`
- Has vanilla storage UI, works with `StorageManager.FindByName()`
- Requires placement on a prop grid — not safe to spawn at arbitrary world positions

---

## Working Approach: Mogul-Owned Deposit Zone

Use the dumpster as a **visual prop only**. Mogul owns the interaction and item transfer.
Items are consumed from the player's inventory; nothing is placed inside the dumpster.

### Container Spawning

```csharp
// S1MAPI namespace is S1MAPI.Core — NOT α1.Core (α1 is a lean-ctx display alias only)
var prefab = new S1MAPI.Core.PrefabRef("Dumpster_Built");
var prefabGo = prefab.Find();                     // host-only; returns null if not found
var go = UnityEngine.Object.Instantiate(prefabGo);
go.name = "Mogul_DropZone_" + questId;
go.transform.position = quest.WorldPosition;
go.SetActive(true);

var netObj = go.GetComponent<NetworkObject>();
if (netObj != null)
    InstanceFinder.ServerManager.Spawn(netObj);
```

Prefab names confirmed in S1MAPI Prefabs.cs:
- `"Dumpster_Built"` — large outdoor dumpster ✓ used
- `"TrashCan_Built"` — smaller bin
- `"SmallTrashCan_Built"` — small indoor can

### CRITICAL: Destroy TrashContainerItem After Spawn

The dumpster prefab has a `TrashContainerItem` component that opens a vanilla trash storage
UI when the player interacts with it. This **intercepts all interaction** — the player will
open the trash UI instead of the Mogul deposit prompt.

Destroy it immediately after spawning:

```csharp
var trash = go.GetComponent<TrashContainerItem>();
if (trash != null) UnityEngine.Object.Destroy(trash);
```

Wrap in try/catch — the component may have partially initialised and throw on Destroy.

### Hover Label: Repurpose the Dumpster's Own InteractableObject

**Do NOT add a new child `InteractableObject` for the hover label.** A new empty child GO
has no collider; `InteractionManager` raycasts will never hit it so the label never shows.

The dumpster mesh children already have colliders. Use `GetComponentInChildren<InteractableObject>()`
to find the vanilla interactable, clear its listeners, and repurpose it:

```csharp
var interactable = go.GetComponentInChildren<InteractableObject>();
if (interactable != null)
{
    try { interactable.onInteractStart?.RemoveAllListeners(); } catch { }
    try { interactable.onInteractEnd?.RemoveAllListeners(); } catch { }
    interactable.SetMessage($"[Q] Deposit {quest.Target} OG Kush");
    interactable.SetInteractableState(InteractableObject.EInteractableState.Label);
}
```

Note: `InteractableObject` has `onInteractStart` and `onInteractEnd` (both `UnityEvent`).
There is **no `onInteract` field** — the research doc placeholder was wrong.

Update the message live each tick while the player is hovering to show inventory count:

```csharp
var hovered = Singleton<InteractionManager>.Instance?.HoveredInteractableObject;
if (hovered == myInteractable)
{
    int have = CountItemInInventory(quest.TargetId);
    string msg = have >= quest.Target
        ? $"[Q] Deposit {quest.Target} OG Kush ({have} in hand)"
        : $"Need {quest.Target} OG Kush — you have {have}";
    hovered.SetMessage(msg);
}
```

### Q Key Trigger: Proximity-Based (Not Hover-Based)

Do **not** rely on `HoveredInteractableObject == interactable` to trigger the deposit.
The hover check is unreliable for prop-spawned objects.

Use a proximity check instead — works regardless of look direction:

```csharp
var playerPos = S1API.Entities.Player.Local?.Position;   // returns Vector3? (nullable)
bool qDown = Input.GetKeyDown(KeyCode.Q) && !GameInput.IsTyping;

if (playerPos.HasValue && qDown)
{
    if (Vector3.Distance(playerPos.Value, go.transform.position) <= 2.5f)
        OnDropZoneInteract(questId);
}
```

### Player Inventory Access

`PlayerInventory` is a `PlayerSingleton<T>` — access via the singleton, not via Player.Local:

```csharp
// CORRECT
var inv = Il2CppScheduleOne.DevUtilities.PlayerSingleton<PlayerInventory>.Instance;

// WRONG — Player.Local has no S1Player property in this context
// var inv = Player.Local.S1Player.GetComponent<PlayerInventory>();
```

### Item ID Access Pattern

Cast through `ProductItemInstance` and `ProductDefinition` — the plain `BaseItemDefinition`
cast does not reliably expose `.ID` for packaged drug items in Il2Cpp:

```csharp
// CORRECT
var product = slot.ItemInstance.TryCast<ProductItemInstance>();
if (product == null) continue;
var def = product.Definition?.TryCast<ProductDefinition>();
if (def?.ID == targetItemId) found += slot.Quantity;

// WRONG — BaseItemDefinition cast may silently fail for product items
// ((BaseItemDefinition)slot.ItemInstance.Definition).ID
```

### Counting and Removing Items

```csharp
// Count
int found = 0;
foreach (var slot in inv.GetAllInventorySlots())
{
    if (slot?.ItemInstance == null) continue;
    var product = slot.ItemInstance.TryCast<ProductItemInstance>();
    if (product == null) continue;
    var def = product.Definition?.TryCast<ProductDefinition>();
    if (def?.ID == itemId) found += slot.Quantity;
}

// Remove (partial — take only what's needed)
int remaining = required;
foreach (var slot in inv.GetAllInventorySlots())
{
    if (remaining <= 0) break;
    if (slot?.ItemInstance == null) continue;
    var product = slot.ItemInstance.TryCast<ProductItemInstance>();
    if (product == null) continue;
    var def = product.Definition?.TryCast<ProductDefinition>();
    if (def?.ID != itemId) continue;

    int take = Math.Min(remaining, slot.Quantity);
    remaining -= take;
    if (take >= slot.Quantity)
        slot.ClearStoredInstance();
    else
        slot.ChangeQuantity(-take);
}
```

### Player Feedback

Use `NotificationsManager` for in-game popups — console only is not sufficient:

```csharp
// namespace: Il2CppScheduleOne.UI
// Singleton<T> from Il2CppScheduleOne.DevUtilities

// Insufficient items warning
Singleton<NotificationsManager>.Instance?.SendNotification(
    "Dead Drop",
    $"Need {required} OG Kush — you have {found}",
    null, 3f, false);   // null sprite is safe; playSound=false for warnings

// Success
Singleton<NotificationsManager>.Instance?.SendNotification(
    "Dead Drop",
    "Delivered. Claim your reward in the app.",
    null, 4f, true);
```

Signature: `SendNotification(string title, string subtitle, Sprite icon, float duration = 5f, bool playSound = true)`

---

## Quest Definition Shape (Confirmed Working)

```csharp
new MogulQuestDefinition
{
    Id          = "shipment_westville_01",
    Type        = MogulObjectiveType.Quest,
    Title       = "Dead Drop",
    Description = "A contact has a storage unit near the property. Get forty packs of OG Kush inside it. No names, no trail.",
    Objective   = "Drop 40 OG Kush at the storage unit",
    Target      = 40,                                         // quantity = progress threshold
    ReachReward = 1200,
    Event       = MogulObjectiveEvent.DropOffDrug,
    TargetId    = EmployeeProduction.TestBudtenderProductId,  // "ogkush" — item ID for inventory + event routing
    WorldX      = -158f, WorldY = -3.13f, WorldZ = 85f,      // near Westville — adjust with F5
    Radius      = 5f,
    IsAvailable = data => HasClaimed(data, "westville_statement"),
    OnClaim     = _ => MogulDropZoneSpawner.OnQuestClaimed("shipment_westville_01"),
}
```

Key points:
- `Target = 40` — progress threshold AND the item quantity checked/consumed.
- `TargetId = "ogkush"` — doubles as the item definition ID for inventory scan AND the event routing key (`DropOffDrug:ogkush`). Each drop-off quest should use a unique item+event pairing, or give each its own opaque TargetId to avoid cross-quest progress bleed.
- `OnClaim` schedules container despawn. Future quests should not hardcode the Id here — refactor to a generic handler once there are multiple drop-off quests.

---

## Event Recording

```csharp
// Records 40 progress to "DropOffDrug:ogkush" → propagates to "shipment_westville_01"
// Quest reaches Target=40 → IsComplete = true
MogulQuestSystem.RequestRecordEvent(MogulObjectiveEvent.DropOffDrug, itemId, required);
```

---

## Container Despawn

After `OnQuestClaimed` is invoked from the quest's `OnClaim` callback, schedule destruction:

```csharp
// In OnQuestClaimed:
_pendingDestroy[questId] = Time.time + ContainerDespawnDelay;  // e.g. 3f for testing

// In Tick():
foreach (var kvp in new Dictionary<string, float>(_pendingDestroy))
{
    if (Time.time >= kvp.Value)
    {
        _pendingDestroy.Remove(kvp.Key);
        DestroyContainer(kvp.Key);  // network despawn + Object.Destroy
    }
}
```

Use `new Dictionary<...>(dict)` snapshot when iterating to avoid modifying the collection
during foreach.

---

## StorageManager / WorldStorageEntity (Reference Only)

Not used for drop-off quests, but documented for future vanilla-storage quests:

```csharp
StorageInstance? s = S1API.Storages.StorageManager.FindByName("AlleySafe");
s?.OnContentsChanged += () => { /* check contents */ };
```

`StorageInstance` exposes `GetItems()`, `AddItem()`, `TryRemoveQuantity(id, qty)`,
`GetContentsDictionary()`, `OnContentsChanged` event.

---

## Gotchas Summary

| Gotcha | Fix |
|--------|-----|
| `TrashContainerItem` intercepts interaction with vanilla trash UI | `Object.Destroy(go.GetComponent<TrashContainerItem>())` after spawn |
| Child `InteractableObject` on empty GO never gets hovered (no collider) | Use `GetComponentInChildren<InteractableObject>()` on the dumpster root instead |
| `onInteract` field does not exist on `InteractableObject` | Use `onInteractStart` / `onInteractEnd`, or skip listener binding entirely and use proximity Q check |
| Hover-based Q trigger unreliable for prop-spawned objects | Proximity distance check + `KeyCode.Q` in `Tick()` — always works |
| `Player.Local.S1Player.GetComponent<PlayerInventory>()` fails | `PlayerSingleton<PlayerInventory>.Instance` — it's a singleton |
| `(BaseItemDefinition)slot.ItemInstance.Definition).ID` may return null | `TryCast<ProductItemInstance>()` then `TryCast<ProductDefinition>()` |
| `α1.Core.PrefabRef` — compile error | Real namespace is `S1MAPI.Core` — `α1` is lean-ctx display alias |
| `NotificationsManager` spam if called every frame | Guard with `_deposited` set and single-fire on state change only |
