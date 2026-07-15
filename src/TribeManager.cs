using BepInEx.Logging;
using HarmonyLib;
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

    /* NOTE TO SELF: ASK MIDJIATE
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
        if  (__instance.action.Climate == GameManager.GameState.Map.GetTile(__instance.action.Coordinates).climate)
        {
            GameManager.DelayCall(1, onComplete);
            return false;
        }
        return true;
    }*/
}