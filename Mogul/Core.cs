using MelonLoader;
using Mogul.Systems;
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
        MogulNetwork.Initialize();
        LocationSpawner.Initialize();
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        LoggerInstance.Msg($"[Scene] buildIndex={buildIndex} sceneName={sceneName}");
        if (sceneName == "Menu")
            MogulNetwork.InitializeSteam();
        if (sceneName == "Main")
        {
            LocationSpawner.ClearSpawned();
            LocationSpawner.SyncSpawns();
        }
    }

    public override void OnUpdate()
    {
        MogulNetwork.Tick();

        if (Input.GetKeyDown(KeyCode.F5))
        {
            var pos = Player.Local?.Position;
            if (pos.HasValue)
                LoggerInstance.Msg($"[POS] X={pos.Value.x:F2}  Y={pos.Value.y:F2}  Z={pos.Value.z:F2}");
        }

        if (Input.GetKeyDown(KeyCode.F6))
        {
            var pos = Player.Local?.Position;
            if (pos.HasValue)
                LoggerInstance.Msg($"[CORNER] X={pos.Value.x:F2}  Y={pos.Value.y:F2}  Z={pos.Value.z:F2}");
        }
    }
}
