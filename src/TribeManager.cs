using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.UnityEngine;
using HarmonyLib;
using Polibrary.Parsing;
using Polibrary.PolyScript;
using PolyMod.Managers;
using Polytopia.Data;
using PolytopiaBackendBase.Common;


namespace Polibrary;

public static class TribeManager
{
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

            
            if (
                string.IsNullOrEmpty(playerState.GetNameInternal()) &&
                PolibData.TryGetValue<PolibTribeData, TribeType, string>(Parse.polibTribeDatas, tribeData.type, "leaderName", out var name)
                )
            {
                playerState.UserName = name;
            }
        }
    }
    #endregion
    
    #region Climate Overrides

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateInternal))]
    public static void OverrideMapPass(MapData __result, int seed, GameState gameState, MapGeneratorSettings settings)
    {
        foreach (TileData tile in __result.tiles)
        {
            if (PolibData.TryFindData(Parse.polibTribeDatas, tile.climate, out var data))
            {
                if (data.terrainOverrides != null)
                {
                    if (data.terrainOverrides.TryGetValue(tile.terrain, out var newTerrain))
                    {
                        tile.terrain = newTerrain;
                    }
                }
                if (data.resourceOverrides != null)
                {
                    if (data.resourceOverrides.TryGetValue(tile.resource.type, out var newResource))
                    {
                        tile.resource = new ResourceState()
                        {
                            type = newResource
                        };
                    }
                }
            }
        }

        __result.GenerateShoreLines();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartTurnReaction), nameof(StartTurnReaction.Execute))]
    public static void Thingy(StartTurnReaction __instance)
    {
        if (!GameManager.GameState.TryGetPlayer(__instance.action.PlayerId, out var playerState))
        {
            return;
        }

        if (playerState != null && playerState.UserName.ToLower().Contains("bananique"))
        {
            Random random = new();

            if (random.Next(0,20) != 1) return;

            InputManager.DisableAllInput();
            BasicPopup popup = PopupManager.GetBasicPopup();
            popup.Header = $"Congratulations {playerState.UserName}!!";
            popup.Description = $"By using Polibrary Lite, you just won yourself a 1-off voucher for a {random.Next(0, 100)}% discount on your next Polibrary purchase!";
            popup.buttonData = new PopupBase.PopupButtonData[1]
            {
                new PopupBase.PopupButtonData("UPGRADE NOW!", PopupBase.PopupButtonData.States.Selected, (Il2CppSystem.Action)delegate
                {
                    NotificationManager.Notify($"Current balance: -{random.Next(100, 50000)}$ Thank you for choosing Polibrary!", "Transaction successful!");
                    InputManager.EnableAllInput();
                })
            };
            popup.Show();
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