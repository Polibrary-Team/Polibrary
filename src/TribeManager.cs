using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EnumsNET;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Linq.Expressions.Interpreter;
using JetBrains.Annotations;
using Polytopia.Data;
using PolytopiaBackendBase.Auth;
using PolytopiaBackendBase.Game;
using SevenZip.Compression.LZMA;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements.UIR;
using System.Reflection;
using UnityEngine.EventSystems;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;

using Une = UnityEngine;
using Il2Gen = Il2CppSystem.Collections.Generic;
using pbb = PolytopiaBackendBase.Common;


namespace Polibrary;

public static class TribeManager
{
    private static ManualLogSource wingLogster;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(TribeManager));
        wingLogster = logger;
        //wingLogster.LogInfo("I'm wing gaster");
        //wingLogster.LogInfo("UHH I MEAN");
        //wingLogster.LogInfo("✋︎ ✂︎♌︎♏︎♐︎❒︎♓︎♏︎■︎♎︎♏︎♎︎✂︎ ⍓︎□︎◆︎❒︎ ❍︎□︎⧫︎♒︎♏︎❒︎ ●︎♋︎⬧︎⧫︎ ■︎♓︎♑︎♒︎⧫︎");
    }

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