# Customer Demand System

## Overview

Two-phase system: preferences are locked in at spawn, purchases are decided when
the NPC reaches the counter. All randomness is seeded deterministically from the
NPC's instanceID so the same NPC always wants the same things.

---

## Phase 1 — Preferences at Spawn

`CustomerDemand.GeneratePreferences(seed, customerComp)` runs when the NPC spawns.

### QualityExpectation (0–1)

If the NPC has a `Customer` component with `customerData.Standards`, it maps that
enum to a 0–4 quality level × 0.25 plus tiny noise (±0.04).

Without `customerData` (generic walk-in), weighted roll:

| Roll     | Range        | Meaning                |
|----------|--------------|------------------------|
| 0–29%    | 0.00–0.12    | Doesn't care, wants cheap |
| 30–64%   | 0.13–0.30    | Medium-low taste       |
| 65–89%   | 0.31–0.55    | Medium taste           |
| 90–100%  | 0.56–0.75    | Somewhat picky         |

No generic walk-in will ever roll above ~0.75. High-quality product is wasted on
early-game customers.

### TotalBudget / MaxBudgetPerItem

With `customerData`: visit budget = random value between `MinWeeklySpend /
avgOrders` and `MaxWeeklySpend / avgOrders`.

Without it: flat `$50–$200` uniform. No weighting — a first-visit nobody is just
as likely to walk in with $200 as $50.

`MaxBudgetPerItem = TotalBudget × 0.6` — hard ceiling per package price. An NPC
with a $60 budget will refuse any product priced above $36.

### WeedAffinity (0–1)

Read from `DefaultAffinityData.ProductAffinities` for `EDrugType.Marijuana`,
remapped from the −1…+1 engine range. Generic walk-ins default to **0.8** (strong
weed preference). This is fixed per NPC at spawn.

### PreferredEffectIds

Three effect names drawn from a pool of 35 via Fisher-Yates partial shuffle.
Purely random — no weighting toward common or popular effects.

---

## Phase 2 — Purchase Decision at Counter

`CustomerDemand.DecidePurchases(prefs, stock, seed)` runs when the NPC arrives.
`StorageScanner.Scan` provides current shelf stock as a list of `StorageProduct`.

### Scoring each item in stock

1. **Price gate** — if `price > MaxBudgetPerItem`, skip entirely (counted as
   `overBudget`).
2. **Quality step** — compare item quality (0–4 × 0.25) to `QualityExpectation`:
   - ≥+0.25 above expectation → **+1.0**
   - 0 to +0.25 above → **+0.5**
   - 0 to −0.25 below → **−0.5**
   - ≥−0.25 below → **−1.0**
3. **Effect score** — how many of the NPC's 3 preferred effects appear on the
   item: `matchCount / 3 × 0.4` (max 0.4, each effect counts once).
4. **Raw appeal** = `WeedAffinity × 0.3 + effectScore + qualityStep × 0.3`
   - Min raw ≈ −0.06 (0.8 × 0.3 + 0 + (−1.0) × 0.3), Max ≈ 1.04
5. **Normalized appeal** = `InverseLerp(−0.6, 1.0, raw)` → 0–1

Items sorted by appeal descending.

### Selection loop

Runs while `remaining ≥ top-ranked item price`:

1. 50% chance pick top-ranked item, 50% random from the remaining list.
2. `enjoyScale = Lerp(0.66, 1.5, appeal)` — enthusiasm scalar.
3. `qty = Round(remaining × enjoyScale / price)`, clamped `1..prod.TotalPackages`.
4. Deduct `price × qty` from `remaining`, repeat.

---

## Known Problems

### Bug: Quantity formula can exceed budget

`qty = Round(remaining × enjoyScale / price)` with `enjoyScale` up to **1.5**
means a high-appeal product produces a quantity request worth up to **1.5×
remaining budget**. Example:

- Budget: $150, price: $5, enjoyScale: 1.5
- qty = Round(150 × 1.5 / 5) = **45 packages** ($225 cost, 50% over budget)

`remaining` goes negative after the deduction, stopping further purchases, but the
first product already drains the shelf. The only real cap is `prod.TotalPackages`
— so if you have 40 bags in storage, a first-visit NPC with $150 and a cheap
product will buy all 40.

**Fix needed:** cap qty at `Floor(remaining / price)` before applying the
enjoyScale multiplier, or change enjoyScale to a sub-1.0 fraction-of-budget
scalar.

### Problem: Generic fallback budget is unweighted

Walk-ins without `customerData` get a flat $50–$200 roll. Early-game customers
should skew toward $50–$80. Nothing gates a first-visit NPC from walking in with
a $200 budget.

### Problem: No tier differentiation on walk-ins

All generic walk-ins share the same `WeedAffinity = 0.8` and the same flat budget
distribution. There is currently no distinction between a curious low-spender and
a regular high-spender — the system has one "unknown customer" archetype.

---

## Planned: Reputation Tiers

The long-term design ties customer quality to store reputation (Phase 2+). Tiers
would gate which `customerData` archetypes can spawn and bias the fallback
distributions for generics.

| Tier | Reputation | Budget skew     | Quality expectation | Notes                        |
|------|------------|-----------------|---------------------|------------------------------|
| 0    | 0–10       | $30–$80         | 0.00–0.30           | Curious locals, one-timers   |
| 1    | 11–30      | $60–$140        | 0.10–0.50           | Regulars starting to form    |
| 2    | 31–60      | $100–$250       | 0.25–0.65           | Established clientele        |
| 3    | 61–100     | $200–$500       | 0.40–0.75           | High-spenders, effect-picky  |

Higher tiers also unlock `customerData`-backed NPCs with tighter preferences,
effect demands, and real weekly spend bands — replacing the flat random fallback.

---

## Rejection Reasons

| Reason        | Condition                                              |
|---------------|--------------------------------------------------------|
| `EmptyShelves`| Stock list was empty                                   |
| `TooExpensive`| Every item failed the price gate, none scored          |
| `LowAppeal`   | Items existed but all scored zero after filtering      |
