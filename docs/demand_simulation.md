# Demand Simulation

The demand simulator runs the pure `CustomerDemand` purchase logic without
launching the game. Use it to test pricing, reach tiers, inventory quality,
effect matching, and whether budtender upgrades create a sensible sales path.

It does not simulate NPC movement, checkout UI, real time, vanilla customers, or
storage scanning. You give it reach plus an inventory list, and it rolls many
synthetic customers deterministically from a seed.

## Run It

From the repo root:

```bash
dotnet run --project Mogul.Tests/Mogul.Tests.csproj --no-restore -- --simulate-demand
```

Useful flags:

```bash
dotnet run --project Mogul.Tests/Mogul.Tests.csproj --no-restore -- --simulate-demand --scenario starter --reach 750 --customers 10000
dotnet run --project Mogul.Tests/Mogul.Tests.csproj --no-restore -- --simulate-demand --scenario mixed --reach 3500 --customers 50000 --seed 42
dotnet run --project Mogul.Tests/Mogul.Tests.csproj --no-restore -- --simulate-demand --scenario mixed --reach 10000 --customers 50000 --no-deplete
```

Flags:

- `--scenario starter` uses basic base strains.
- `--scenario mixed` uses base strains plus example mixed strains.
- `--reach <number>` controls the customer budget tier.
- `--customers <number>` controls how many customer rolls to run.
- `--seed <number>` makes runs repeatable.
- `--no-deplete` keeps inventory reusable, which is useful for demand-share and
  pricing tests where stockouts would hide what customers wanted.

## Custom Inventory

Pass one or more `--stock` rows to replace the preset inventory.

Format:

```text
id|display name|price|packages|quality|effect,effect
```

Example:

```bash
dotnet run --project Mogul.Tests/Mogul.Tests.csproj --no-restore -- --simulate-demand \
  --reach 3500 \
  --customers 25000 \
  --stock "ogkush|OG Kush|35|500|2|calming" \
  --stock "ogkush_cuke|OG Kush + Cuke|55|500|3|calming,refreshing" \
  --stock "sourdiesel_energy|Sour Diesel + Energy|70|500|3|energizing,focused"
```

Quality uses the same scale as scanned storage:

- `0` Trash
- `1` Poor
- `2` Standard
- `3` Premium
- `4` Heavenly

Effects are lowercased IDs like `calming`, `energizing`, `focused`, `sedating`,
or `refreshing`. The simulator compares these to the demand effect pool in
`CustomerDemand`.

## Reading The Report

The report shows:

- `Fulfilled customers`: how many customers bought anything.
- `Orders`: number of product lines selected.
- `Packages sold`: total units sold.
- `Revenue`: total simulated income.
- `Avg revenue/customer`: expected value per walk-in roll.
- `Avg revenue/buyer`: expected value once a customer decides to buy.
- `Rejections`: why customers walked away.
- `Products`: sales, revenue, buyers, and remaining stock per product.

Use depleting inventory when testing a real day or stock plan. Use
`--no-deplete` when testing whether a price/effect mix is attractive over a large
sample without stockouts dominating the result.

## What To Test

Good balancing passes:

- Starter path: at `0`, `750`, and `3500` reach, base strains should sell without
  pricing players into constant `TooExpensive` rejections.
- First budtender upgrade: compare base weed against `base + 1 ingredient` at
  the same reach. The mixed strain should raise revenue without making base
  products useless.
- Slot unlocks: test `750`, `3500`, `10000`, and `20000` reach with 1/2/3/4
  ingredient-style products. Higher slot products should become viable as budgets
  rise.
- Pricing pressure: increase one product price by 10-20% per run and watch when
  `TooExpensive` overtakes extra revenue.
- Effect value: keep price and quality fixed, then swap effect lists to see how
  much demand comes from matching effects instead of raw quality.
- Inventory planning: run with depletion and realistic package counts to see
  whether a location sells out too early or carries too much low-value stock.

## Current Limits

The simulator only knows the current pure demand rules. It does not yet model:

- reach gained from sales
- employee wages or property costs
- cashier throughput
- online orders
- per-location reputation
- actual vanilla mixed product prices/effects from runtime

Those can be layered in later as separate inputs once the core demand loop feels
right.
