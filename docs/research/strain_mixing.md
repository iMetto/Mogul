# Strain Mixing Research

Mogul goal: let budtenders produce upgraded strains by selecting a base drug and
an ordered list of supermarket/gas-station ingredients. Reach unlocks the number
of ingredient slots.

## Vanilla Model

- Product effects are stored on `ProductDefinition.Properties`, inherited from
  `PropertyItemDefinition`.
- `ProductItemInstance` stores the product definition, quality, quantity, and
  packaging. It does not carry a separate effect list.
- Effect assets are `Il2CppScheduleOne.Effects.Effect` ScriptableObjects with
  `ID`, `Name`, value modifiers, addictiveness, colors, and mix-vector fields.
- S1API property tokens, such as `S1API.Properties.Tokens.Sedating`, are thin
  identifiers. `PropertyResolver` resolves tokens to game `Effect` assets by
  scanning `Resources.LoadAll<Effect>` under `Properties/Tier1` through
  `Properties/Tier5`.
- `EffectMixCalculator.MixProperties(existingProperties, newProperty, drugType)`
  is the low-level vanilla effect-combination function.

## Vanilla Product Creation APIs

Useful decompiled entry points:

- `Il2CppScheduleOne.Product.NewMixOperation(productID, ingredientID)` stores a
  base product id plus a mixer item id.
- `ProductManager.GetMixerMap(EDrugType)` returns the drug-type mixer map.
- `ProductManager.GetRecipe(string product, string mixer)` returns known station
  recipe data for a product/ingredient pair.
- `ProductManager.GetKnownProduct(EDrugType, List<Effect>)` resolves an existing
  product definition for a final property set.
- `ProductManager.CreateWeed_Server(name, id, type, List<string> properties,
  WeedAppearanceSettings appearance)` and matching methods for meth, cocaine,
  and shrooms create definitions directly.
- `ProductManager.SendMixOperation(NewMixOperation operation, bool complete)` and
  `ProductManager.FinishAndNameMix(productID, ingredientID, mixName)` are the
  closest match to the vanilla UI workflow. `FinishAndNameMix` returns a product
  id and records/discovers the mix through ProductManager's save state.
- `ProductManager.SendMixRecipe(product, mixer, output)` records the recipe
  relation.
- `ProductManager.CalculateProductValue(baseValue, List<Effect>)` computes value
  from effects.

## Implementation Direction

Prefer using the vanilla ProductManager mix workflow instead of hand-building
product definitions first.

1. Represent a Mogul strain recipe as `baseProductId + ordered ingredientIds`.
2. Gate max ingredients by reach/budtender upgrade level.
3. When an order starts, resolve or create each step:
   - current product id starts as the selected base product.
   - for each selected ingredient id, call/check the vanilla mix path for
     `(currentProductId, ingredientId)`.
   - use the returned output id as the next current product id.
4. Store the final output product id on the budtender order.
5. When the order completes, deposit that final product id into storage or
   virtual inventory, the same way current budtender stock is deposited.
6. Customer demand can keep using `StorageScanner`: once storage contains the
   final product id, `StorageScanner` will read `ProductDefinition.Properties`
   and feed `EffectIds` into `CustomerDemand`.

## UI Direction

The manage panel should replace the four fixed budtender product buttons with a
small strain builder:

- base product selector first
- ingredient grid second
- selected ingredients display as numbered badges `1`, `2`, `3`, etc.
- disabled cells for locked slots or unavailable ingredients
- order preview: final/working name, expected effects, estimated value
- submit starts a budtender order with the encoded recipe

For first implementation, keep the grid data-driven and small: cuke, gasoline,
energy drink, horse semen, plus other items that pass vanilla
`ItemFilter_MixingIngredient` once their ids are confirmed at runtime.

## Risks

- Ingredient ids and exact product outputs are better confirmed in-game because
  item definitions are resource-backed and not all ids are obvious in the
  generated assembly.
- Multiplayer should route creation through the host/server ProductManager path,
  not direct client-only definition construction.
- Direct `CreateWeed_Server` is available but requires us to own effect
  resolution, appearance settings, recipe registration, discovery/listing, and
  save state. Use it only if `FinishAndNameMix` proves unusable from the mod.

## Mogul Implementation

Added first playable slice:

- `Mogul/Systems/StrainMixingSystem.cs`
  - ingredient catalog and candidate vanilla ids
  - reach-gated slot counts: 0/1/2/3/4 at reach 0/750/3500/10000/20000
  - order payload encoding as `location|base|ingredient,ingredient`
  - runtime chain resolution through `ProductManager.FinishAndNameMix`
- `BudtenderOrderData`
  - keeps legacy `ProductId`
  - adds `BaseProductId`, ordered `IngredientIds`, and `DisplayName`
- `MogulNetwork.StartBudtenderOrder`
  - validates ownership, budtender, base product, and unlocked slot count
  - resolves the final vanilla product id before accepting the order
- `MogulApp.BuildBudtenderOrderSection`
  - replaces fixed order buttons with base product selection, ingredient grid,
    numbered ingredient order, preview, clear, and start controls

Manual test focus:

- Verify exact runtime item ids for cuke, energy drink, gasoline, and horse
  semen. `StrainMixingSystem` tries several candidates, but the game log will
  show `[Mogul] Budtender order rejected` if a candidate is still wrong.
- Verify `FinishAndNameMix` can be called from the host action path without the
  vanilla mix UI being open.
- Verify final created product deposits into storage and `StorageScanner` reads
  the created product effects into customer demand.
