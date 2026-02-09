using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EnumsNET;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
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
using Polibrary.Parsing;

namespace Polibrary;

public static class UI
{
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(UI));
        modLogger = logger;
    }

    #region BuildingUI

    [HarmonyPostfix]
    [HarmonyPatch(typeof(InteractionBar), nameof(InteractionBar.AddUnitActionButtons))]
    private static void TryShowCost(InteractionBar __instance, Il2Gen.List<CommandBase> availableActions)
    {
        if (__instance == null) return;
        if (GameManager.LocalPlayer == null || GameManager.LocalPlayer.AutoPlay) return;
        if (availableActions == null || availableActions.Count == 0) return;

        var commands = availableActions.ToArray();
        foreach (var command in commands)
        {
            BuildCommand buildCommand = command.TryCast<BuildCommand>();
            if (buildCommand == null) continue;
            if (!GameManager.GameState.GameLogicData.TryGetData(buildCommand.Type, out ImprovementData improvementData)) continue;

            var refLoc = LocalizationUtils.CapitalizeString(Localization.Get(improvementData.displayName));
            var buttons = __instance.buttons.ToArray();
            foreach (var button in buttons)
            {
                if (button.text == refLoc)
                {

                    if (buildCommand != null && improvementData.cost > 0)
                    {
                        button.Cost = improvementData.cost;
                        button.ShowLabel = true;
                        button.m_showLabelBackground = true;
                        button.UpdateLabelVisibility();
                    }
                }
            }
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(BuildingUtils), nameof(BuildingUtils.GetInfo))]
    private static void InfoOverride(ref string __result, PolytopiaBackendBase.Common.SkinType skinOfCurrentLocalPlayer, ImprovementData improvementData, ImprovementState improvementState = null, PlayerState owner = null, TileData tileData = null)
    {
        if(PolibData.TryGetValue(Parse.polibImprovementDatas, improvementData.type, nameof(PolibImprovementData.infoOverride), out string result))
            __result = Localization.Get(result);
    }
    #endregion
}