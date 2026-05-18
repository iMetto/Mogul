# Research Index

This folder is the reusable research memory for game, mod, assembly, and OTC
findings.

Before scanning `assembly/` or `/home/imetto/projects/mods/OTC-S1-Mod`, search
this folder for relevant terms first. Examples:

```bash
rg -n "npc|spawn|route|NavigationBuilder" docs/research
rg -n "storage|rack|inventory|TakeOne" docs/research
rg -n "budtender|grow|employee" docs/research
```

When new reusable information is learned from assembly, OTC, or in-game testing,
update the most specific file here. Create a new research file only when no
existing topic fits. Keep current checklists, hand-written how-to steps, and
active test reports in `docs/` root.

Local Ollama research output goes in `docs/research/inbox/`. Treat inbox notes
as untrusted drafts until their claims are verified against source snippets, then
promote confirmed facts into the matching file in this folder.

## Current Files

- `assembly_employees.md` - vanilla employee classes, botanist/growing APIs, and
  Mogul's lightweight worker decision.
- `assembly_items.md` - item categories, ingredients, item definitions, and
  runner research entry points.
- `handover_cashregister.md` - cash register visual state and known issue.
- `npc.md` - CivilianNPC spawn sequence, appearance, voice, routing, queue, and
  storage notes.
- `otc_budtender_reference.md` - OTC budtender-specific NPC appearance and
  staffing lessons.
- `otc_customer_research.md` - OTC customer/product-selection model.
- `placement_research.md` - storage rack, grow tent, vanilla buildable, and OTC
  custom-grid placement research.
- `quests.md` - Mogul quest layer and S1API/vanilla quest integration notes.
- `quest_task_hooks.md` - quest/task event backend, unlock flags, NPC/sale/body hook leads.
- `dropoff_quests.md` - drop-off quest tech: dumpster spawning via PrefabRef,
  why vanilla storage doesn't apply, player inventory removal, MogulDropZoneSpawner
  design, and gotchas (grid init, server-only spawn, no persistence).
- `rebuild_lifecycle.md` - customization rebuild, rack preservation, employee
  respawn, cashier anchor math, grow tent respawn, and storage-removal guard.
