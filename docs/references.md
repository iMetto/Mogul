# Reference Map

Use this file before scanning `assembly/` or OTC. The goal is to keep sessions
small: search narrow, summarize findings here, and avoid rereading huge generated
files.

## External Reference Repos

- OTC: `/home/imetto/projects/mods/OTC-S1-Mod`
  - Use as a behavior reference, not as code to copy wholesale.
  - Keep OTC outside this repo. Copying it into Mogul makes search/build/graph
    context noisier.

## Research Memory

Search `docs/research/` before scanning generated assembly files or OTC. These
files are the durable notes for reusable findings from assembly, OTC, game
internals, and manual testing.

- `docs/research/README.md` - research notes index.
- `docs/research/npc.md` - CivilianNPC spawn sequence, appearance, voice, routing.
- `docs/research/otc_budtender_reference.md` - OTC budtender-specific NPC appearance and
  staffing lessons.
- `docs/research/assembly_items.md` - item categories, ingredients, item definitions,
  and runner research entry points.
- `docs/research/assembly_employees.md` - vanilla employee classes, botanist/growing
  APIs, and why Mogul currently uses lightweight worker NPCs.
- `docs/research/placement_research.md` - player placement/repositioning research for
  storage racks, grow tents, vanilla buildables, and OTC's custom-grid approach.
- `docs/research/rebuild_lifecycle.md` - customization rebuild notes, rack preservation,
  direct employee placement, cashier anchor math, and grow tent respawn flow.
- `docs/research/quests.md` - Mogul quest layer and S1API/vanilla quest integration notes.
- `docs/research/map_pins.md` - Mogul/OTC map POI pin implementation notes.
- `docs/research/quest_task_hooks.md` - new quest/task backend, unlock flags, and
  NPC/sale/body hook leads.
- `docs/research/strain_mixing.md` - vanilla ProductManager mixing/product
  creation APIs and Mogul budtender strain-builder plan.
- `docs/research/otc_customer_research.md` - OTC customer/product-selection model.
- `docs/research/handover_cashregister.md` - cash register visual state and known issue.

## Local Working Docs

- `docs/whatToTest.md` - current manual test checklist for playable slices.
- `docs/CustomerDemand.md` - pure customer demand/order logic.
- `docs/demand_simulation.md` - headless demand simulator usage for pricing,
  reach, inventory, and upgrade balancing.
- `docs/howto.md` - chat usage and handoff notes.

## Search First

Prefer `rg` over opening generated files:

```bash
rg -n "EItemCategory|Ingredient|MixingIngredient" assembly/Assembly-CSharp assembly/S1API.Il2Cpp.MelonLoader-1
rg -n "class .*Employee|EEmployeeType|EmployeeManager|Botanist|Handler|Chemist|Cleaner" assembly/Assembly-CSharp/Il2CppScheduleOne/Employees
rg -n "GrowContainer|SeedDefinition|Plant|Harvest|SowSeed|WaterPot" assembly/Assembly-CSharp/Il2CppScheduleOne assembly/S1API.Il2Cpp.MelonLoader-1
rg -n "GetDefaultInstance|ItemRegistry|ItemDefinition|StorableItemDefinition" assembly/Assembly-CSharp/Il2CppScheduleOne assembly/S1API.Il2Cpp.MelonLoader-1
rg -n "BudtenderSpawner|BudtenderStorageSearch|IsStaffed|CheckoutArrivalTime" /home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter -g '*.cs'
```

## Assembly Reading Rule

Generated IL2CPP files are huge. Read exact line windows around search hits, not
whole files. After a useful discovery, update the matching file in
`docs/research/` with:

- exact file path
- class/method/field names
- what is safe to call
- known gotchas
