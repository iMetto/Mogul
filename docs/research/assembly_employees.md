# Assembly Employee And Growing Reference

Use this for employees, botanists, grow containers, and future budtender grow
tent work.

## Current Entry Points

- `assembly/Assembly-CSharp/Il2CppScheduleOne/Employees/EEmployeeType.cs`
- `assembly/Assembly-CSharp/Il2CppScheduleOne/Employees/Employee.cs`
- `assembly/Assembly-CSharp/Il2CppScheduleOne/Employees/Botanist.cs`
- `assembly/Assembly-CSharp/Il2CppScheduleOne/Employees/Packager.cs`
- `assembly/Assembly-CSharp/Il2CppScheduleOne/NPCs/Behaviour/GrowContainerBehaviour.cs`
- `assembly/Assembly-CSharp/Il2CppScheduleOne/NPCs/Behaviour/SowSeedInPotBehaviour.cs`
- `assembly/Assembly-CSharp/Il2CppScheduleOne/Building/BuildUpdate_GrowContainer.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Growing/PlantInstance.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Growing/SeedDefinition.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Growing/SeedCreator.cs`

## Known Facts

Vanilla employee types:

- `Botanist`
- `Handler`
- `Chemist`
- `Cleaner`

Useful vanilla/S1API growing concepts:

- `GrowContainer`
- `GrowContainerBehaviour`
- `SowSeedInPotBehaviour`
- `PlantInstance.NormalizedGrowth`
- `PlantInstance.IsFullyGrown`
- `SeedDefinition`

`Botanist` has substantial configuration and work-behavior fields:

- assigned pots/stations
- configuration replicator
- worldspace UI
- sow/water/harvest behaviors
- missing-material/no-work dialogues

This is too much to depend on for Mogul's first employee milestone.

## Mogul Decision

Mogul currently uses lightweight worker NPCs, not vanilla `Employee` subclasses.
Reason: visible NPCs plus role logic can ship quickly, while vanilla employee
setup likely requires property/locker/configuration plumbing and can fail in
places unrelated to Mogul's custom locations.

Current roles:

- Cashier: auto-completes checkout and deposits to register.
- Budtender: produces virtual `ogkush` test stock at `20/day`.
- Runner: spawns as NPC; behavior intentionally deferred.

## Search Commands

```bash
rg -n "enum EEmployeeType|class Employee|class Botanist|class Packager" assembly/Assembly-CSharp/Il2CppScheduleOne/Employees
rg -n "GrowContainer|SowSeed|Harvest|WaterPot|Plant|SeedDefinition" assembly/Assembly-CSharp/Il2CppScheduleOne assembly/S1API.Il2Cpp.MelonLoader-1
rg -n "EmployeeIdlePoints|EmployeeCapacity|Employees" assembly/Assembly-CSharp/Il2CppScheduleOne/Property assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Property
```

## Next Budtender Slice

The next useful implementation is not vanilla botanist AI. It should be:

1. Spawn a local grow-tent visual in the property.
2. Show a simple plant visual/state tied to the budtender's selected strain.
3. Keep production virtual until real item creation/storage insertion is proven.
4. Later, replace virtual production with real product stacks.

