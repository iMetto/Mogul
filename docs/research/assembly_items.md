# Assembly Item And Ingredient Reference

Use this for runner/ingredient/product lookup work.

## Current Entry Points

Open/search these first:

- `assembly/Assembly-CSharp/Il2CppScheduleOne/ItemFramework/ItemFilter_MixingIngredient.cs`
- `assembly/Assembly-CSharp/Il2CppScheduleOne/UI/Shop/EShopCategory.cs`
- `assembly/Il2CppScheduleOne.Core/Il2CppScheduleOne/Core/Items/Framework/EItemCategory.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Items/ItemDefinition.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Items/StorableItemDefinitionBuilder.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Stations/ChemistryStationRecipeBuilder.cs`

## Known Facts

- `EItemCategory` includes `Ingredient`.
- S1API `ItemCategory` also includes `Ingredient`.
- `ItemDefinition.CreateInstance(int quantity)` wraps
  `S1ItemDefinition.GetDefaultInstance(quantity)`.
- S1API station recipes validate ingredients through item IDs and native item
  definitions.
- `StorableItemDefinitionBuilder` warns if the station item prefab does not
  contain `IngredientModule` or `PourableModule`, which matters for real station
  tasks.

## Runner Direction

For the first runner implementation, avoid physically buying from the gas station
until item ID lookup and storage insertion are confirmed. Safer staged path:

1. Build an ingredient catalog from definitions where category is `Ingredient`.
2. Let runner maintain virtual inventory per location.
3. Once storage insertion is proven, convert virtual runner inventory into real
   `ItemInstance` stacks.

## Search Commands

```bash
rg -n "EItemCategory|ItemCategory|Ingredient" assembly/Assembly-CSharp/Il2CppScheduleOne assembly/Il2CppScheduleOne.Core assembly/S1API.Il2Cpp.MelonLoader-1
rg -n "ItemFilter_MixingIngredient|MixingIngredient" assembly/Assembly-CSharp/Il2CppScheduleOne
rg -n "GetDefaultInstance|CreateInstance|ItemDefinition" assembly/Assembly-CSharp/Il2CppScheduleOne/ItemFramework assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Items
rg -n "IngredientModule|PourableModule|StationItem|StorableItemDefinition" assembly/S1API.Il2Cpp.MelonLoader-1 assembly/Assembly-CSharp/Il2CppScheduleOne
```

## Open Questions

- Exact registry/API for enumerating all item definitions at runtime.
- Exact item IDs for gas station ingredient stock.
- Best storage insertion API for adding item stacks to Mogul property racks.

