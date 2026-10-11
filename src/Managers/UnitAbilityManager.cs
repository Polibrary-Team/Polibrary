using System.Drawing;
using BepInEx.Logging;
using HarmonyLib;
using Polibrary.Parsing;
using Polibrary.PolyScript;
using Polytopia.Data;
using UnityEngine.Video;


namespace Polibrary.Managers;

public static class UnitAbilityManager
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(UnitPopup), nameof(UnitPopup.AddAbilityButtons))]
    private static void AbilityButtonColoring(UnitPopup __instance, Il2CppSystem.Collections.Generic.List<Polytopia.Data.UnitAbility.Type> abilities)
    {
        foreach (UITextButton button in __instance.abilityButtons)
        {
            UnitAbility.Type type = abilities[button.id];

            if (PolibData.TryFindData(Parse.polibUnitAbilityDatas, type, out var data))
            {
                if (data.bgColorStates != null)
                {
                    if (data.bgColorStates.defaultColor.a != 2)
                    {
                        button.BgColorStates.defaultColor = data.bgColorStates.defaultColor;
                    }
                    if (data.bgColorStates.hoverColor.a != 2)
                    {
                        button.BgColorStates.hoverColor = data.bgColorStates.hoverColor;
                    }
                    if (data.bgColorStates.highlightedColor.a != 2)
                    {
                        button.BgColorStates.highlightedColor = data.bgColorStates.highlightedColor;
                    }
                    if (data.bgColorStates.highlightedHoverColor.a != 2)
                    {
                        button.BgColorStates.highlightedHoverColor = data.bgColorStates.highlightedHoverColor;
                    }
                    if (data.bgColorStates.disabledColor.a != 2)
                    {
                        button.BgColorStates.disabledColor = data.bgColorStates.disabledColor;
                    }
                }
                if (data.labelColorStates != null)
                {
                    if (data.labelColorStates.defaultColor.a != 2)
                    {
                        button.LabelColorStates.defaultColor = data.labelColorStates.defaultColor;
                    }
                    if (data.labelColorStates.hoverColor.a != 2)
                    {
                        button.LabelColorStates.hoverColor = data.labelColorStates.hoverColor;
                    }
                    if (data.labelColorStates.highlightedColor.a != 2)
                    {
                        button.LabelColorStates.highlightedColor = data.labelColorStates.highlightedColor;
                    }
                    if (data.labelColorStates.highlightedHoverColor.a != 2)
                    {
                        button.LabelColorStates.highlightedHoverColor = data.labelColorStates.highlightedHoverColor;
                    }
                    if (data.labelColorStates.disabledColor.a != 2)
                    {
                        button.LabelColorStates.disabledColor = data.labelColorStates.disabledColor;
                    }
                }
            }
        }
    }
}