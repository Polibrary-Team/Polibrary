using BepInEx.Logging;
using HarmonyLib;
using Polibrary.PolyScript;
using Polytopia.Data;


namespace Polibrary;

public static class TribeManager
{
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(TribeManager));
    }

    #region AI Leader Name
    // Simpler than it seems, and it seems very simple (Fapingvin, 2025)
    [HarmonyPrefix] //na azt jól megmondtad
    [HarmonyPatch(typeof(GameStateUtils), nameof(GameStateUtils.SetPlayerNames))]
    public static void OverridePlayerNames(GameState gameState)
    {
        foreach (PlayerState playerState in gameState.PlayerStates)
        {
            TribeData tribeData;
            gameState.GameLogicData.TryGetData(playerState.tribe, out tribeData);
            if (string.IsNullOrEmpty(playerState.GetNameInternal()) && Parsing.Parse.leaderNameDict.TryGetValue(tribeData.type, out string name))
            {
                playerState.UserName = name;
            }
        }
    }
    #endregion
    /* would need PreActionGameState
    [HarmonyPrefix] //fix for custom tribe spread and alienclimate waits so it works like polaris
    [HarmonyPatch(typeof(ClimateChangeAction), nameof(ClimateChangeAction.Execute))]
    private static bool ClimateChangeActionFix(ClimateChangeAction __instance, GameState gameState)
    {
        return __instance.Climate != gameState.Map.GetTile(__instance.Coordinates).climate;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(ClimateChangeReaction), nameof(ClimateChangeReaction.Execute))]
    private static bool ClimateChangeReactionFix(ClimateChangeReaction __instance, Il2CppSystem.Action onComplete)
    {
        if  (__instance.action.Climate == PolibActionManager.PreActionGameState.Map.GetTile(__instance.action.Coordinates).climate)
        {
            Main.modLogger.LogInfo("did");
            onComplete.Invoke();
            return false;
        }
        Main.modLogger.LogInfo("didnt");
        return true;
    }*/
}