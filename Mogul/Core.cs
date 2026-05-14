using System;
using MelonLoader;
using Mogul.Systems;
using Mogul.UI;
using UnityEngine.SceneManagement;
using S1API.Entities;
using UnityEngine;
using Il2CppScheduleOne;

[assembly: MelonInfo(typeof(Mogul.Core), "Mogul", "0.1.0", "imetto")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace Mogul;

public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        LoggerInstance.Msg("Mogul loaded.");
        MogulPersistence.Initialize();
        LocationSpawner.Initialize();
        SellDesk.Initialize();
        CustomerManager.Initialize();
        EmployeeSystem.Initialize();
        BuildingPreview.RegisterConsoleCommands();
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        LoggerInstance.Msg($"[Scene] buildIndex={buildIndex} sceneName={sceneName}");
        if (sceneName == "Menu")
        {
            MogulPersistence.ResetData();
            MogulNetwork.InitializeSteam();
        }
        if (sceneName == "Main")
        {
            LocationSpawner.ClearSpawned();
            EmployeeSystem.ClearSpawned();
            LocationSpawner.SyncSpawns();
            SellDesk.SyncDesks();
            EmployeeSystem.SyncAll();
        }
    }

    public override void OnGUI()
    {
        CheckoutUI.Draw();
    }

    public override void OnUpdate()
    {
        MogulNetwork.Tick();

        var playerPos = Player.Local?.Position;
        if (playerPos.HasValue)
            CustomerManager.Tick(playerPos.Value);
        EmployeeSystem.Tick();

        if (Input.GetKeyDown(KeyCode.F4))
        {
            MogulNetwork.RequestAction(MogulActions.PurchaseWithDesign, "loc_westville_01:industrial");
            Il2CppScheduleOne.Console.SubmitCommand("teleport bungalow");
            LoggerInstance.Msg("[Mogul] Debug: purchased loc_westville_01, teleporting to bungalow");
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            var player = Player.Local;
            var pos = player?.Position;
            if (pos.HasValue)
            {
                LoggerInstance.Msg($"[POS] X={pos.Value.x:F2}  Y={pos.Value.y:F2}  Z={pos.Value.z:F2}");
                var yaw = player.Transform != null ? player.Transform.eulerAngles.y : 0f;
                LoggerInstance.Msg($"[POS] Yaw={yaw:F1}");
                if (LocationGeometry.TryFindNearestLocation(pos.Value, out var loc))
                {
                    if (LocationSpawner.TryGetSpawnedBuilding(loc.Id, out var root) && root != null)
                    {
                        var local = root.transform.InverseTransformPoint(pos.Value);
                        var localForward = root.transform.InverseTransformDirection(player.Transform.forward);
                        var localYaw = Mathf.Atan2(localForward.x, localForward.z) * Mathf.Rad2Deg;
                        LoggerInstance.Msg($"[POS] {loc.Id} local=({local.x:F2}, {local.y:F2}, {local.z:F2}) localYaw={localYaw:F1}");
                    }
                    else
                    {
                        var local = pos.Value - loc.WorldPosition;
                        LoggerInstance.Msg($"[POS] {loc.Id} approxLocal=({local.x:F2}, {local.y:F2}, {local.z:F2})");
                    }
                }
            }
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            var pos = Player.Local?.Position;
            if (pos.HasValue)
                CustomerManager.SpawnForNearestLocation(pos.Value);
        }
    }
}
