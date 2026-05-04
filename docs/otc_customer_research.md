# OTC CustomerInstance Research

Source files read:
- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Logic/CustomerInstance.cs` (all specified ranges)
- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Logic/CheckoutProcess.cs` (key sections)
- `/home/imetto/projects/mods/Mogul/Mogul/Systems/CustomerManager.cs` (full file, for gap analysis)

---

## 1. CustomerInstance Data Model

### Identity fields
| Field | Type | Notes |
|---|---|---|
| `Id` | `string` | Unique customer ID (e.g. `"deal_Jesse_Smith_4321"` for deal customers) |
| `SpawnSeed` | `int` | Seeded random source for all deterministic choices |
| `SpawnPoint` | `CustomerSpawnPoints.SpawnPoint` | Where the NPC entered from |
| `GameNpc` | `NPC` | The underlying game NPC object |
| `NetworkObjectId` | `int` | FishNet ObjectId for client-side adoption |
| `IsAdopted` | `bool` | True if adopted from a FishNet-replicated NPC (no clone spawned) |
| `IsValid` | `bool` | `GameNpc != null && GameNpc.gameObject != null` |
| `Position` | `Vector3?` | Current world position, null if invalid |

### Customer type flags
| Field | Type | Notes |
|---|---|---|
| `IsDealCustomer` | `bool` | True = redirected from vanilla deal system; false = synthetic walk-in |
| `VanillaCustomer` | `Customer` | Set for deal customers only; null for walk-ins |
| `WarpReturnPosition` | `Vector3?` | Where deal NPC returns to after exiting |

### Preferences struct (`CustomerPreferences`)
Nested public struct on `CustomerInstance`. All preference data lives here.

| Field | Type | Range / Meaning |
|---|---|---|
| `QualityExpectation` | `float` | 0.0 (Trash) to 0.75 (Premium); represents the quality scalar threshold |
| `PreferredEffectIds` | `string[]` | Always 3 entries (lowercased ScriptableObject names, e.g. `"euphoric"`) |
| `MaxBudgetPerItem` | `float` | Hard per-product price ceiling ($); exceeded products are skipped |
| `TotalOrderBudget` | `float` | Total $ to spend this visit. For deal customers this equals vanilla daily budget; for walk-ins it's a weekly-budget fallback |
| `WeedAffinity` | `float` | -1 to 1; contributes `WeedAffinity * 0.3f` to raw enjoyment score |

### Familiarity
`Familiarity` (float, 0–1) controls how many products the budtender recommends. For walk-ins it is seeded random; for deal customers it is `RelationDelta / 5f` (vanilla relationship).

### Product selection outputs
| Field / Type | Notes |
|---|---|
| `SelectedProducts` (List<SelectedProduct>) | Products decided after browsing / consultation. Read-only externally (getter only). |
| `EnjoyPremium` (float) | Accumulated enjoy-scale surplus across all selected products. Used as the base for the skill-check tip. |
| `LastRejection` (RejectionReason enum) | `None`, `EmptyShelves`, `TooExpensive`, or `LowAppeal` — drives the disappointed voice line. |

### `SelectedProduct` struct (lines 246–256)
```
ProductId       string   — ProductDefinition.ID
PackagingId     string   — always null after DecidePurchases; checkout assigns packaging
ProductName     string
Price           float    — per-UNIT price (not per-package)
QualityLevel    int      — 0=Trash 1=Poor 2=Standard 3=Premium 4=Heavenly (EQuality cast)
Quantity        int      — units to buy (not packages)
EffectIds       List<string>  — effect IDs on this product (for tip calc)
```

`PackagingId` is intentionally null at the time of selection. CheckoutProcess assigns concrete packaging during `SearchAndShowAvailable`.

### State machine fields
| Field | Type | Notes |
|---|---|---|
| `State` | `CustomerState` | Enum; setting it resets `_stateEnteredTime` and `NavRetries` |
| `TimeInCurrentState` | `float` | Seconds in current state |
| `NavRetries` | `int` | Nav-stuck retry count; incremented by `ResetStateTimer()` |
| `ArrivedAtDestination` | `bool` | Set by nav callback; polled by state handlers |
| `CheckoutArrivalTime` | `float` | Time.time when NPC arrived at counter; 0 means not yet |
| `CheckoutStartHour` | `int` | Game hour (0-23) when queue was joined |
| `CheckoutStartTime` | `int` | Full HHMM game time (minute precision) |
| `AssignedCounter` | `CheckoutCounterInstance` | Counter this customer is queued at |
| `LookAroundEndTime` | `float` | Used when no storage is found |

### Browse state fields (private)
| Field | Notes |
|---|---|
| `_browsePositions` | Local-space stand positions in front of each shelf (with random XZ offset ±0.3m) |
| `_browseShelfPositions` | Shelf centers in local space (used for NPC facing direction) |
| `_browseTargetIndex` | Which shelf the NPC is currently walking to |
| `_browsePauseEndTime` | Time.time when current shelf pause ends (5s per shelf) |
| `_seenProducts` | Private list of `ObservedProduct` — products memorized from shelves but not yet scored |
| `_willVocalizeWhileBrowsing` | True for ~30% of customers (seed % 10 < 3) |

### `ObservedProduct` struct (private, lines 270–281)
Used only during scanning; never exposed outside the class.
```
ProductId, PackagingId, ProductName  — string
Price           float   — per-unit; from PricingSaveData or ProductDefinition.MarketValue
MarketValue     float   — vanilla market value per unit
QualityLevel    int
AvailableQuantity int   — unit count AFTER dedup (converted from packages in DecidePurchases)
PkgMultiplier   int     — units per package (baggie=1, jar=5, brick=20)
EffectIds       List<string>
```

---

## 2. Product Selection Flow

The flow has two paths: **physical browsing** (walk-ins visiting shelves) and **budtender consultation** (skill check with the player). Both ultimately call `DecidePurchases()`.

### Physical browsing path (walk-ins, `StartBrowsing` → `TickBrowsing`)

1. `StartBrowsing(localStandPositions, localShelfCenters)` is called externally.
   - Clears `_seenProducts` and `SelectedProducts`.
   - Adds random ±0.3m XZ offsets to each stand position.
   - Sends NPC to the first browse position via `SendToInteriorBrowseTarget(0)`.

2. `TickBrowsing()` is called every frame:
   - If pausing at a shelf: waits for `BrowsePauseDuration` (5 seconds).
   - On arrival at a shelf: stops NPC movement, faces the shelf center, starts pause, calls `ObserveShelf()`.
   - `ObserveShelf()` finds the nearest `StorageEntity` to the shelf position and calls `ScanStorageEntity()`.
   - `ScanStorageEntity()` iterates all slots, skips disabled products (`PricingSaveData.IsSellingDisabled`), and adds each `ProductItemInstance` to `_seenProducts`.
   - Price comes from `PricingSaveData.GetPrice(prodDef)` or falls back to `prodDef.MarketValue`.
   - Quality comes from `(int)productItem.Quality` (EQuality cast to int).
   - After all shelves are visited, `TickBrowsing()` returns `true` — caller triggers `DecidePurchases()`.

3. Note in source: `ObserveShelf` is marked **"Legacy — used only if browsing is ever re-enabled."** The current live path is the budtender consultation.

### Budtender consultation path (current live path)

1. `CheckoutProcess.FinishSkillCheck()` (after player presses SPACE on the skill bar):
   - Calls `BudtenderStorageSearch.GetAllAccessibleStorages(_counter)` to get all storage entities.
   - Calls `customer.ObserveFromStorageList(storages)` — clears `_seenProducts`, scans all storages.
   - Calls `customer.ObservePlayerInventory()` — appends any packaged products from the player's hotbar.
   - Calls `customer.FilterByFamiliarity()` — limits visible products based on `Familiarity` (0=2 products max, 1=full menu).
   - Calls `customer.DecidePurchases()`.

2. `FilterByFamiliarity()` (lines 1300–1328):
   - Counts unique ProductIds in `_seenProducts`.
   - If ≤2 unique products, skips filtering.
   - Otherwise uses `Lerp(2, totalUnique, Familiarity)` to determine how many product types the budtender "mentions".
   - Removes all `_seenProducts` entries whose ProductId is not in the selected subset (seeded Fisher-Yates shuffle, seed = `SpawnSeed + 8831`).

### `DecidePurchases()` — step-by-step (lines 1334–1644)

1. **Empty check**: if `_seenProducts` is empty → `LastRejection = EmptyShelves`, return.

2. **Deduplication** (by ProductId): keeps highest QualityLevel; sums available units across all packaging types.

3. **Scoring** (vanilla `GetProductEnjoyment` formula replicated):
   - `drugScore = Preferences.WeedAffinity * 0.3f`
   - `effectScore = (matchCount / 3) * 0.4f` (how many of the 3 preferred effects are present)
   - `qualityDelta = (obs.QualityLevel * 0.25f) - Preferences.QualityExpectation`
   - `qualityStep`: +1.0 if delta ≥ 0.25, +0.5 if delta ≥ 0, -0.5 if delta ≥ -0.25, else -1.0
   - `qualityScore = qualityStep * 0.3f`
   - `rawEnjoyment = drugScore + effectScore + qualityScore`
   - `enjoyment = InverseLerp(-0.6, 1.0, rawEnjoyment)` → normalized 0–1
   - Products where `obs.Price > Preferences.MaxBudgetPerItem` are hard-skipped (counted as `overBudgetCount`).

4. **Sorting**: by `appeal` (= normalized enjoyment) descending.

5. **Budget-driven selection loop** (lines 1507–1621):
   - `remainingBudget`: for deal customers = `Preferences.TotalOrderBudget`; for walk-ins = `TryGetFirstDealBudget()` (vanilla zero-relation daily budget / 3) or falls back to `Preferences.TotalOrderBudget / WalkInBudgetDivisor` (÷3).
   - Each iteration: 50% chance to pick the top product, 50% chance to pick a random one from the rest.
   - Quantity formula: `enjoyScale = Lerp(0.66, 1.5, appeal)`, then `qty = round(remainingBudget * enjoyScale / price)`, clamped to available stock and [1, 1000].
   - Vanilla acceptance gate: calls `EvaluateVanillaOffer()` which invokes `Customer.GetOfferSuccessChance` on the actual vanilla Customer component (if available); if the random roll fails, product is skipped (`rejectedByChanceCount++`).
   - On accept: adds to `SelectedProducts`; subtracts `price * qty` from `remainingBudget`; accumulates `EnjoyPremium += price * qty * (enjoyScale - 1)`.
   - Loop continues until budget exhausted or no products remain.

6. **Rejection classification**:
   - `SelectedProducts.Count == 0 && overBudgetCount > 0` → `TooExpensive`
   - `SelectedProducts.Count == 0 && rejectedByChanceCount > 0` → `TooExpensive`
   - `SelectedProducts.Count == 0 && neither` → `LowAppeal`

---

## 3. Quality System

### Storage
- Quality is stored as `EQuality` enum on `QualityItemInstance` (base of `ProductItemInstance`).
- Cast to `int`: 0=Trash, 1=Poor, 2=Standard, 3=Premium, 4=Heavenly.
- Read: `(int)productItem.Quality` inside `ScanStorageEntity`.
- Stored as `QualityLevel: int` in both `ObservedProduct` and `SelectedProduct`.

### Scoring
- Quality scalar = `QualityLevel * 0.25f` (so Trash=0.0, Poor=0.25, Standard=0.5, Premium=0.75, Heavenly=1.0).
- `QualityExpectation` in `CustomerPreferences` uses the same 0–1 range.
- Delta: `qualityScalar - QualityExpectation`.
- Stepped to ±0.5 or ±1.0 multiplied by weight 0.3f.
- This means a Premium product (0.75) shown to a Trash-expecting customer (0.0) scores +0.3 on quality; shown to a Heavenly-expecting one (1.0) it scores -0.15.

### Where `QualityExpectation` comes from
- **Walk-ins** (`GeneratePreferences`): seeded random, skewed low:
  - 30% chance: 0.00–0.12 (Trash tier)
  - 35% chance: 0.13–0.30 (Poor tier)
  - 25% chance: 0.31–0.55 (Standard tier)
  - 10% chance: 0.56–0.75 (Premium tier)
- **Deal customers** (`ExtractVanillaPreferences`): `CustomerData.GetQualityScalar(data.Standards.GetCorrespondingQuality())` — uses the vanilla NPC's actual quality standard.

### Quality in checkout
- `CounterProduct.QualityLevel` is stored and passed to `saveData.RecordSale`.
- For the vanilla acceptance gate: `qInst.SetQuality((EQuality)obs.QualityLevel)` is called on the temp item instance before `GetOfferSuccessChance`.

---

## 4. Pricing

### Per-unit price source
- During scanning (`ScanStorageEntity`, `ObservePlayerInventory`):
  `Price = PricingSaveData.Instance?.GetPrice(prodDef) ?? prodDef.MarketValue`
  — the player's set price, falling back to the vanilla market value.

### Per-customer modifiers
- There are **no multiplicative per-customer price modifiers**. The price is the same price from `PricingSaveData` for every customer.
- What does vary per-customer is **quantity**: `enjoyScale = Lerp(0.66, 1.5, appeal)` scales the budget before dividing by price, so high-appeal customers buy more units at the same price.
- `MaxBudgetPerItem` is a hard ceiling: if `obs.Price > Preferences.MaxBudgetPerItem`, the product is entirely skipped. This effectively prices out low-budget customers.

### Walk-in budget calculation
1. Primary: `TryGetFirstDealBudget()` — reads the NPC's actual `CustomerData`:
   - `weekly = data.GetAdjustedWeeklySpend(0f)` (zero relation)
   - `days = data.GetOrderDays(0f, 0f).Count` (zero addiction, zero relation)
   - Returns `weekly / days / 3f`
2. Fallback (vanilla Customer unavailable):
   `Preferences.TotalOrderBudget / 3f`
   where `TotalOrderBudget` from `GeneratePreferences` = `VanillaMinWeeklySpend * rankMultiplier` = `200f * rankMult`.

### Deal customer budget
- Mirrored from vanilla `TryGenerateContract`:
  - `TryGetVanillaDailyBudget(customer)` = `GetAdjustedWeeklySpend(relationDelta/5) / orderDays.Count`
  - This becomes `Preferences.TotalOrderBudget`.
  - No WalkInBudgetDivisor is applied.

### Per-package price in checkout
- `SearchAndShowAvailable` converts per-unit price to per-package:
  `Price = selection.Price * c.mult` (where `c.mult` = pkg units, e.g. jar=5 → price×5).
- `_totalPlacedPrice` accumulates per-package prices of placed items.

### Tip calculation
- `EnjoyPremium`: accumulated in `DecidePurchases` as `price * qty * (enjoyScale - 1)`.
  Represents the enjoy-scale surplus that in vanilla would have been a markup.
- Skill-check multiplier: Miss/timeout=0.5×, Green=1.1×, Gold=1.4×.
- Deal tip (`DispensaryDealManager.GetTipAmount`): additional tip for deal customers (separate system, not explored).
- Total tip = `dealTip + EnjoyPremium * skillCheckBonus`.

---

## 5. Browsing Stage (lines 984–1904)

The browsing state machine is tick-based (polled every frame by `CustomerManager`). There are two modes, but only the **budtender consultation** path is currently active in production.

### State machine walkthrough

**Physical browse (legacy path):**

| Step | What happens |
|---|---|
| `StartBrowsing()` | Initializes `_browsePositions` with jittered stand positions; sends NPC to first position |
| NPC walking | `TickBrowsing()` waits for `ArrivedAtDestination = true` (set by nav callback) |
| Arrived at shelf | Stops movement, faces shelf, starts 5s pause, calls `ObserveShelf()` |
| `ObserveShelf()` | Finds nearest `StorageEntity` (via `BuildingGridFactory.GridContainers`), calls `ScanStorageEntity()` |
| `ScanStorageEntity()` | Iterates `ItemSlots`, adds `ObservedProduct` to `_seenProducts` for each `ProductItemInstance` |
| Pause end | Advances `_browseTargetIndex`; if no more shelves, returns `true` → caller calls `DecidePurchases()` |
| Voice line | 30% of customers play `EVOLineType.Think` once during browse (`_willVocalizeWhileBrowsing`) |

**Budtender consultation (live path):**

| Step | What happens |
|---|---|
| `CheckoutProcess.Tick()` — CameraPanning state | After `CameraPanWait` (0.6s): if `SelectedProducts.Count == 0`, enters SkillCheck state |
| Skill check (`TickSkillCheck`) | 4s bounce-bar; SPACE key captures zone hit; voice lines at 1.5s (Question) and 3.0s (Acknowledge) |
| Zone result | Miss=0.5×, Green=1.1×, Gold=1.4× tip multiplier stored in `_skillCheckTipBonus` |
| `FinishSkillCheck()` | Scans storage, filters by familiarity, calls `DecidePurchases()` |
| No products | `ShowDisappointed()` → camera returns, customer leaves angry |
| Products found | `SearchAndShowAvailable()` then transitions to `WaitingForPlacement` |

**WaitingForPlacement state:**

| Step | What happens |
|---|---|
| `SearchAndShowAvailable()` | Reads `_customer.SelectedProducts`; for each requested product, finds matching packages in counter storage and player inventory; creates `AvailableProduct` list |
| Greedy fill | Largest packaging first (brick→jar→baggie); fractional remainder triggers `EmitBreakdownFragments()` |
| Breakdown fragments | Virtual pledges against a source package; source slot untouched until `CommitBreakdownGroups()` at sale commit |
| HUD | `BudtenderHUD` shows sprite cards for each available package; player clicks to place |
| Placement (`OnSpriteClicked`) | Calls `TrackPlacedProduct` → adds to `_counterProducts`; calls `ConsumeFromSource` → decrements storage/hotbar; updates `_totalPlacedPrice` and `_placedUnitCounts` |
| All placed | If no missing products: auto-transitions to `CustomerPickup`. If missing: shows "Complete Sale" button |
| R/Tab back-out | Pauses checkout; right-click on counter visuals returns last-placed product to inventory |

**CustomerPickup → PaymentAppearing → WaitingForPayment → CashFlying → CameraReturning:**

| Step | What happens |
|---|---|
| Pickup | NPC plays pickup animation; placed products fly/shrink |
| PaymentAppearing | 0.3s delay, then spawns payment object (cash model) |
| WaitingForPayment | Player must click the payment object |
| CashFlying | 0.8s cash-fly animation; deposits `_totalPlacedPrice` to register |
| CameraReturning | 0.6s delay; calls `CompleteCheckout()` |

### Key internal calls on CustomerInstance during checkout
- `customer.SelectedProducts` — read to build sprite HUD
- `customer.EnjoyPremium` — read for tip
- `customer.AssignedCounter` — to find the register
- `customer.IsDealCustomer` — to decide deal rewards
- `customer.VanillaCustomer` — for addiction change and `GetOfferSuccessChance`
- `customer.CheckoutArrivalTime = 0f` — signals customer done
- `customer.State = CustomerState.ExitingStore` — on completion
- `customer.RecallFromBuilding()` — routes NPC out

---

## 6. Checkout Handoff

### What CheckoutProcess receives from CustomerInstance

`CheckoutProcess` is constructed with `(CustomerInstance customer, CheckoutCounterInstance counter)` and holds `_customer` for the entire checkout lifetime.

The fields it reads:

| Field | Used for |
|---|---|
| `_customer.SelectedProducts` | Building the sprite HUD; tracking which units still need to be placed |
| `_customer.EnjoyPremium` | Computing the enjoy-premium tip (multiplied by `_skillCheckTipBonus`) |
| `_customer.Preferences.TotalOrderBudget` | (Via `DecidePurchases`; not re-read in checkout directly) |
| `_customer.AssignedCounter` | Finding the register for deposit and for `BuildingId` in sale records |
| `_customer.IsDealCustomer` | Whether to call `ApplyDealRewardsNonMonetary` |
| `_customer.VanillaCustomer` | Addiction change after sale; `GetOfferSuccessChance` in `DecidePurchases` |
| `_customer.GameNpc` | Animation triggers, voice lines, `fullName` for sale records |
| `_customer.IsValid` | Safety abort check |
| `_customer.CheckoutArrivalTime` | Set to 0f on completion |
| `_customer.ArrivedAtDestination` | Set to false on completion |
| `_customer.State` | Set to `ExitingStore` on completion |
| `_customer.Id` | Used as key in sale record and lock protocol |

### Payment flow

1. Player clicks payment object → `TickPaymentClick()` detects it.
2. Payment object destroyed; `CashFlying` coroutine starts.
3. `_counter.DepositToRegister(_totalPlacedPrice)` — sale total to register.
4. `CompleteCheckout()` called after 0.8s fly + 0.6s camera return.
5. In `CompleteCheckout`:
   - `RecordSale` called per product: `(ProductId, ProductName, qty=1, Price, QualityLevel, gameDay, custName, gameHour, txId, tip, buildingId)`.
   - Tip = `DispensaryDealManager.GetTipAmount(customer, totalPlacedPrice) + customer.EnjoyPremium * _skillCheckTipBonus`.
   - Tip deposited separately: `counter.DepositToRegister(totalTip)`.
   - Floating notification shown above register.
   - If `IsDealCustomer`: `ApplyDealRewardsNonMonetary` (XP, relationship, cooldown).
   - Addiction applied: `VanillaCustomer.ChangeAddiction(highestAddictiveness / 5f)`.
   - `CustomerManager.OnCheckoutComplete(customerId)` called.
6. Client path: whole sale serialized as `prodId,name,price,quality~prod2,...:skillBonus:highestAddiction` and sent to host via P2P or SyncVar.

### Register flow
- Register is not a vanilla `MoneyManager`; it is `CheckoutCounterInstance.RegisterBalance`.
- `DepositToRegister` adds to `RegisterBalance` (no immediate player cash).
- Player must physically collect from the register (E key, or P2P reg_req/res for clients).
- On collect: `Money.ChangeCashBalance(amount)` gives the player the cash.

---

## 7. Gaps / What Mogul Needs

### Mogul's current `CustomerEntry` vs OTC's model

| OTC system | Mogul currently | Gap |
|---|---|---|
| `CustomerPreferences` struct with quality expectation, 3 preferred effects, max budget per item, total order budget, weed affinity | Hardcoded `Product = "weed"`, `Quantity = 1`, `Price = 50f` | **No preference system at all**. Need `QualityExpectation`, budget fields, and effect preferences. |
| `SelectedProduct` list with ProductId, per-unit price, quality level, quantity, effect IDs | None; product is picked dynamically from first non-null shelf slot | **No product selection**. Mogul picks whatever is on the shelf without scoring. |
| `EnjoyPremium` tip accumulation | None | **No tip system**. |
| `Familiarity` (0–1 from seed or relationship) controls product visibility | None | **No familiarity**. All stock is visible to all customers. |
| `DecidePurchases()` — vanilla enjoyment formula, budget cap, acceptance gate, quantity formula | None | **No demand simulation**. |
| `LastRejection` + disappointed voice line | `NoStock()` exists but only triggers when shelf is empty | **No `TooExpensive` or `LowAppeal` rejection paths**. |
| `IsDealCustomer` + `VanillaCustomer` — deal NPC redirect path | None | **No deal customer support** (Phase 2 concern). |
| `WarpReturnPosition` for deal NPCs | None | Phase 2 concern |
| `NetworkObjectId` / `IsAdopted` for multiplayer | None | Phase 4+ concern |

### Concrete fields to add to `CustomerEntry`

```csharp
// Preference / demand
public float QualityExpectation;    // 0.0–0.75
public string[] PreferredEffectIds; // 3 effect IDs
public float MaxBudgetPerItem;      // price ceiling per product
public float TotalOrderBudget;      // total spend this visit
public float WeedAffinity;          // -1 to 1
public float Familiarity;           // 0–1; affects product visibility in consultation

// Product selection outputs (replacing stub fields)
public List<SelectedProduct> SelectedProducts;  // decided after scanning
public float EnjoyPremium;                      // tip base

// Rejection tracking
public RejectionReason LastRejection;
```

### Logic Mogul needs to implement

1. **Preference generation** — port `GeneratePreferences(seed)` or a simplified version.
   - Skewed quality distribution appropriate for the player's location/reach tier.
   - 3 random effect IDs from the full effect pool.
   - Budget from vanilla weekly spend (or a Mogul-defined reach-scaled formula).

2. **Storage scan** — equivalent of `ObserveFromStorageList` / `ScanStorageEntity`.
   - Reads `PricingSaveData` price or market value.
   - Reads `(int)productItem.Quality`.
   - Respects disabled products.

3. **Product scoring** — port `DecidePurchases()`.
   - Drug affinity × 0.3 + effect match × 0.4 + quality delta × 0.3.
   - Normalize via `InverseLerp(-0.6, 1.0, raw)`.
   - Budget-driven quantity: `enjoyScale = Lerp(0.66, 1.5, appeal)`, `qty = round(budget * enjoyScale / price)`.
   - Vanilla acceptance gate via `Customer.GetOfferSuccessChance` (optional but makes price acceptance realistic).

4. **Disappointed rejection path** — `LastRejection` enum + per-reason voice lines and text.

5. **Tip system** — accumulate `EnjoyPremium` during selection; modulate by a skill or interaction zone.

6. **Register model** — OTC uses a separate `RegisterBalance` on the counter (not direct player cash). Mogul's `CompleteCheckout` calls `Money.ChangeCashBalance(entry.Price)` directly, which works for now but won't scale to multi-employee/AFK selling.

### What Mogul does that OTC doesn't (and should keep)
- BFS-based queue slot computation is solid and notably more robust than OTC's static slot system.
- The glow-highlight mechanic for storage is Mogul-specific UX; OTC uses a HUD instead.
