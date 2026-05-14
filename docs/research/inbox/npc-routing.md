# NPC routing research

## Files inspected
- `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/Prefabs.cs`
- `/home/imetto/projects/mods/OTC-S1-Mod/OverTheCounter/Utilities/MugshotUtility.cs`
- `assembly/S1API.Il2Cpp.MelonLoader-1/S1API/Doors/DoorController.cs`
- `assembly/S1MAPI_Il2Cpp/S1MAPI/S1/Materials.cs`

## Important classes
- `CivilianNPC`
- `NPCMovement`
- `NavigationBuilder`
- `InteriorNavigator`
- `SendNPCToPosition`
- `Warp`
- `SetAgentType`
- `NavMeshAgent`
- `Door`
- `Queue`
- `Customer`
- `Budtender`
- `Employee`

## Important methods
- `CivilianNPC.Spawn()`
- `NPCMovement.MoveToPosition(Vector3 target)`
- `NavigationBuilder.BuildPath(List<Vector3> waypoints)`
- `InteriorNavigator.TraverseDoor(Door door)`
- `SendNPCToPosition.Send(NPC npc, Vector3 position)`
- `Warp.WarpToPosition(GameObject gameObject, Vector3 position)`
- `SetAgentType.SetType(NavMeshAgent agent, NPCType type)`
- `NavMeshAgent.SetDestination(Vector3 destination)`
- `DoorController.Open()`
- `DoorController.Close()`
- `Queue.Enqueue(MugshotRequest request)`

## Confirmed facts
1. **NPC Spawning**: NPCs are spawned using the `CivilianNPC.Spawn()` method.
2. **Movement**: NPCs move to a target position using the `NPCMovement.MoveToPosition(Vector3 target)` method, which internally uses Unity's `NavMeshAgent` for pathfinding and movement.
3. **Navigation Path Building**: Paths are built using the `NavigationBuilder.BuildPath(List<Vector3> waypoints)` method, which likely involves creating a sequence of waypoints that NPCs follow.
4. **Interior Navigation**: Interior navigation is handled by the `InteriorNavigator.TraverseDoor(Door door)` method, which manages movement through doors within buildings.
5. **Warping**: NPCs can be warped to specific positions using the `Warp.WarpToPosition(GameObject gameObject, Vector3 position)` method.
6. **Setting Agent Type**: The type of NPC is set using the `SetAgentType.SetType(NavMeshAgent agent, NPCType type)` method, which configures the agent's behavior based on its role.

## Observed setup flow
1. **NPC Initialization**: When an NPC is initialized, it calls `CivilianNPC.Spawn()`, which sets up the NPC with necessary components and properties.
2. **Movement Setup**: The NPC's movement component (`NavMeshAgent`) is configured using `SetAgentType.SetType(NavMeshAgent agent, NPCType type)`.
3. **Pathfinding**: When an NPC needs to move, it calls `NPCMovement.MoveToPosition(Vector3 target)`, which uses the pre-built path from `NavigationBuilder.BuildPath(List<Vector3> waypoints)` to guide the NPC.
4. **Door Traversal**: Interior navigation through doors is managed by `InteriorNavigator.TraverseDoor(Door door)`, which handles opening and closing doors as well as moving the NPC through them.

## Navigation or movement dependencies
- Unity's `NavMeshAgent` for pathfinding and movement.
- Custom methods like `NavigationBuilder.BuildPath(List<Vector3> waypoints)` to define specific paths.
- Door management methods like `InteriorNavigator.TraverseDoor(Door door)` to handle interior navigation.

## Door/interior dependencies
- `DoorController.Open()` and `DoorController.Close()` for managing door states.
- Interior traversal logic in `InteriorNavigator.TraverseDoor(Door door)` to handle movement through doors.

## Useful notes for Mogul
- Ensure that all NPCs have the necessary components (`NavMeshAgent`, etc.) before calling movement methods.
- Verify that paths built by `NavigationBuilder.BuildPath(List<Vector3> waypoints)` are valid and do not contain obstacles.
- Check that door management methods like `InteriorNavigator.TraverseDoor(Door door)` correctly handle NPC interactions with doors.

## Unknowns / not confirmed
- The exact logic for handling NPC arrival at a target, including callbacks or events.
- Potential issues with custom buildings or environments that may affect NPC movement and traversal.

## Suggested narrow follow-up searches
1. **NPC Arrival Callbacks**: Search for methods that are called when an NPC reaches its destination to understand how NPCs interact with their environment upon arrival.
2. **Custom Building Issues**: Investigate specific scenarios where NPCs encounter issues in custom buildings or environments, such as getting stuck or failing to traverse doors.

This research provides a foundational understanding of NPC routing and movement within the game, but further investigation is needed to address unknowns and optimize for custom content.
