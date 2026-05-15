# Quest and task hook research

Date: 2026-05-14

## Implemented backend

- `MogulQuestSystem` now models infrequent `Quest` objectives and repeatable-style
  `Task` objectives with shared progress, availability, claim, reward, target,
  location, world position, and radius fields.
- Progress is durable in `MogulSaveData.ObjectiveProgress`.
- Unlock gates are durable in `MogulSaveData.UnlockedFeatureIds`.
- Host action `record_objective_event` records objective progress and maps event
  keys such as `KnockoutNpc:westville_mark_01` into any matching objective.
- First quest availability is triggered by `cash + online balance >= 3000` via
  `S1API.Money.Money.GetCashBalance()` and `GetOnlineBalance()`.
- The Properties tab is hidden until `properties_tab` unlocks.
- Westville purchase is locked until `westville_purchase`.
- Downtown is hidden until `downtown_revealed`, then visible but locked until
  `downtown_purchase`.

## Current hook points

Use `MogulQuestSystem.RequestRecordEvent(eventType, targetId, amount)` from
runtime hooks.

Suggested event keys:

- `KnockoutNpc:westville_mark_01`
- `KnockoutNpc:<task npc id>`
- `PickpocketNpc:<task npc id>`
- `SellDrug:<product id>`
- `SellPawnItem:<stolen item id>`
- `DumpBody:police_water_drop_01`
- `HandOutDrug:ogkush`

## NPC interaction leads

Existing project NPC reference: `docs/research/npc.md`.

Relevant decompiled symbols found:

- `ScheduleOne.AvatarFramework.Avatar.Ragdolled`
- `Avatar.onRagdollChange`
- `Avatar.SetRagdollPhysicsEnabled(bool ragdollEnabled, bool playStandUpAnim)`
- `CartelDealer.DiedOrKnockedOut()`
- `ScheduleOne.Combat.IDamageable`
- `Impact.ImpactDamage`
- weapon hit paths through `AvatarMeleeWeapon.ApplyHitToDamageable` and
  `AvatarRangedWeapon.ApplyHitToDamageable`

Practical next step:

- Spawn quest/task NPCs ourselves using the known `CivilianNPC` spawn sequence.
- Assign deterministic `NPC.ID` values matching objective `TargetId`.
- Attach a small Mogul component to the spawned NPC that subscribes to
  `npc.Avatar.onRagdollChange` and records `KnockoutNpc:<id>` when ragdoll
  flips on.
- For kill-specific objectives, inspect whether the target NPC has a health or
  death state beyond ragdoll in live objects; current search surfaced generic
  combat/damage interfaces but not a clean vanilla `OnDeath` event.

## Body dumping lead

For the police body quest, use our spawned target and track its transform after
ragdoll. The objective definition already carries `WorldPosition` and `Radius`.

Practical next step:

- Mark the spawned police NPC as the current body target after knockout.
- During host tick, if `npc.Avatar.Ragdolled` and distance to
  `police_water_drop_01.WorldPosition` is within radius, record
  `DumpBody:police_water_drop_01`.
- Water detection can start as a coordinate/radius quest volume; live water
  surface or trigger lookup can be researched later if needed.

## Sales and pawn shop leads

Mogul synthetic sales are already centralized in `CheckoutHandler.FulfillOrderDirect`,
then `CashRegister.AddSale`. Hooking `SelectedProduct.ProductId` there can record
`SellDrug:<product id>` for Mogul sales.

Vanilla pawn shop sales need more research. Assembly search surfaced shop and
money APIs, but not a confirmed pawn sale event yet. The likely paths are:

- search `assembly/Assembly-CSharp/Il2CppScheduleOne` narrowly for pawn shop UI
  and transaction methods once exact class names are known from a live object dump;
- hook the method that removes the item from player inventory and calls
  `MoneyManager.ChangeCashBalance`;
- only count items with Mogul-created IDs, so normal player pawn sales do not
  accidentally progress tasks.

## Pickpocket lead

No confirmed pickpocket event surfaced in the quick assembly search. The safer
implementation path is to use spawned Mogul NPCs with a Mogul-controlled
interactable or inventory token, then record `PickpocketNpc:<id>` when that token
is taken. Vanilla NPC pickpocketing can be researched later if the game exposes a
clean event.
