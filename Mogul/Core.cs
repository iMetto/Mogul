using MelonLoader;
using Mogul.Systems;
using Mogul.UI;
using UnityEngine.SceneManagement;
using S1API.Entities;
using UnityEngine;

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
            LocationSpawner.SyncSpawns();
            SellDesk.SyncDesks();
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

        if (Input.GetKeyDown(KeyCode.F7))
        {
            // Cycle reach to the next tier floor so you can test budget/behaviour per tier.
            // Each press jumps to the next tier. Wraps back to 0 after Legend.
            int[] tierFloors = { 0, 10_001, 250_001, 1_000_001, 10_000_001, 50_000_001, 100_000_001, 500_000_001 };
            int current = MogulNetwork.Data.Reach;
            int next = tierFloors[0];
            for (int i = 0; i < tierFloors.Length; i++)
                if (current < tierFloors[i]) { next = tierFloors[i]; break; }

            MogulNetwork.RequestAction(MogulActions.AddReach, (next - current).ToString());

            var tier = ReachSystem.GetTier(next);
            var rng  = new System.Random(42);
            var (min, max) = ReachSystem.GetBudgetRange(tier, rng);
            LoggerInstance.Msg($"[Reach] Set to {ReachSystem.FormatReach(next)} → {ReachSystem.GetTierName(tier)}  normal ${min:F0}–${max:F0}  outlier (20%) up to ${max * 2f:F0}");
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            if (BuildingCustomizerUI.IsVisible)
            {
                BuildingCustomizerUI.Hide();
            }
            else
            {
                var pos = Player.Local?.Position;
                if (pos.HasValue && LocationGeometry.TryFindNearestLocation(pos.Value, out var loc))
                    BuildingCustomizerUI.ShowForLocation(loc.Id, loc.Name);
                else
                    LoggerInstance.Msg("[Mogul] No owned+spawned location nearby for customiser");
            }
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            var pos = Player.Local?.Position;
            if (pos.HasValue)
                LoggerInstance.Msg($"[POS] X={pos.Value.x:F2}  Y={pos.Value.y:F2}  Z={pos.Value.z:F2}");
            MogulNetwork.DumpStoragePrefabs();
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            var pos = Player.Local?.Position;
            if (pos.HasValue)
                CustomerManager.SpawnForNearestLocation(pos.Value);
        }
    }
}
