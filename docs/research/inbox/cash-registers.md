# Cash register / furniture spawning research

## Files inspected
- `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/Prefabs.cs`
- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Utilities/PropertyInventory.cs`
- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/UI/RegisterFloatingText.cs`
- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/SaveData/ConfigSyncData.cs`

## Important classes
- `Prefabs` (from `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/Prefabs.cs`)
- `PropertyInventory` (from `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Utilities/PropertyInventory.cs`)
- `RegisterFloatingText` (from `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/UI/RegisterFloatingText.cs`)
- `ConfigSyncData` (from `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/SaveData/ConfigSyncData.cs`)

## Important methods
- `Prefabs.CashRegister.Instantiate()` (from `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/Prefabs.cs`)
- `PropertyInventory.GetStorages()` (from `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Utilities/PropertyInventory.cs`)
- `RegisterFloatingText.Show()` (from `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/UI/RegisterFloatingText.cs`)
- `ConfigSyncData.PublishCheckoutState()` (from `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/SaveData/ConfigSyncData.cs`)

## Confirmed facts
- Cash register prefab is referenced by the `Prefabs.CashRegister` constant.
- The `PropertyInventory.GetStorages()` method includes logic to exclude checkout counters from storage enumeration.

## Observed setup flow
- Cash registers are instantiated using the `Prefabs.CashRegister.Instantiate()` method.
- The `PropertyInventory.GetStorages()` method is used to enumerate storage entities, excluding checkout counters by default.

## Prefab/object references
- `Prefabs.CashRegister` (from `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/Prefabs.cs`)
- `CheckoutCounter` (from `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Utilities/PropertyInventory.cs`)

## Manager or registration dependencies
- The `ConfigSyncData.PublishCheckoutState()` method is used to publish the state of checkout counters, including their register balances.

## Save/load involvement
- The `PropertySaveData.ApplyRegisterBalance()` method applies a saved register balance to the first checkout counter.
- The `ConfigSyncData.PublishCheckoutState()` and `ConfigSyncData.HandleCheckoutStateChanged()` methods handle synchronization of checkout states across networked clients.

## Useful notes for Mogul
- Ensure that any modifications to cash registers or other furniture objects are compatible with existing save/load systems.
- Verify that the `PropertyInventory.GetStorages()` method correctly excludes checkout counters when necessary.

## Unknowns / not confirmed
- The exact methods and classes involved in the instantiation and setup of cash registers within the game's architecture.
