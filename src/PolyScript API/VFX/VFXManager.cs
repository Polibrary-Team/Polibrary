using System.Data;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Polytopia.Data;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core;
using DG.Tweening.Plugins.Options;
using Unity.Collections.LowLevel.Unsafe;
using Il2CppMicrosoft.Win32;


namespace Polibrary;

public static class VFXManager
{
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(VFXManager));
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void Register(Newtonsoft.Json.Linq.JObject rootObject)
    {
        foreach (KeyValuePair<string, Sprite> kvp in PolyMod.Registry.sprites)
        {
            if (kvp.Key.StartsWith("vfx_"))
            {
                string formatted = kvp.Key.Remove(0,4).Trim('_');
                SpriteMappings[formatted] = kvp.Value;
            }
        }
    }

    public static Dictionary<string, Sprite> SpriteMappings = new();
    public static Dictionary<string, float> SizeMappings = new();
    public static Dictionary<string, Vector2> FadeInOutAnimOverrideMappings = new();
    public static List<string> RegisteredPuffs = new();

    public static void EnsureCustomPuffRegistered(string id, string originalName) // i <3 clanker
    {
        if (RegisteredPuffs.Contains(id)) return;

        GameObject original = ObjectPool.GetPooledObject(originalName);
        ObjectPool.ReturnObject(original);

        GameObject customPrefab = GameObject.Instantiate(original);
        customPrefab.name = id;

        GameObject instance = ObjectPool.GetPooledObject(customPrefab,
            ObjectPool.PooledObjectData.SpawnSettings.CreateIfNull);
        ObjectPool.ReturnObject(instance);

        RegisteredPuffs.Add(id);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Puff), nameof(Puff.StartAnimation), new System.Type[0])]
    private static bool Puff_StartAnimation(Puff __instance)
    {
        string idInPool = __instance.gameObject.name;
        if (SpriteMappings.TryGetValue(idInPool.ToLower(), out var sprite))
        {
            __instance.spriteRenderer.sprite = sprite;
        }
        if (SizeMappings.TryGetValue(idInPool.ToLower(), out var size))
        {
            __instance.spriteRenderer.transform.localScale *= size;
        }

        if (!FadeInOutAnimOverrideMappings.TryGetValue(idInPool.ToLower(), out var fadeData)) return true;

        __instance.IsUsed = true;
        ((Component)__instance).gameObject.SetActive(true);
        ((Component)__instance).transform.localScale = Vector3.one * __instance.startScale;
        __instance.spriteRenderer.color = new Color(__instance.spriteRenderer.color.r, __instance.spriteRenderer.color.g, __instance.spriteRenderer.color.b, 0f);

        __instance.puffSequence = DOTween.Sequence();

        TweenSettingsExtensions.Append(__instance.puffSequence, (Tween)(object)TweenSettingsExtensions.SetEase<TweenerCore<Vector3, Vector3, VectorOptions>>(ShortcutExtensions.DOScale(((Component)__instance).transform, __instance.targetScale, 0f), (Ease)27));
        TweenSettingsExtensions.Append(__instance.puffSequence, (Tween)(object)__instance.spriteRenderer.DOFade(1f, fadeData.x));
        TweenSettingsExtensions.Append(__instance.puffSequence, (Tween)(object)__instance.spriteRenderer.DOFade(0f, fadeData.y));

        TweenSettingsExtensions.AppendCallback(__instance.puffSequence, (TweenCallback)__instance.AnimComplete);

        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Puff), nameof(Puff.AnimComplete))]
    private static void Puff_EndAnimation(Puff __instance)
    {
        string idInPool = __instance.gameObject.name;
        if (SizeMappings.TryGetValue(idInPool.ToLower(), out var size))
        {
            __instance.spriteRenderer.transform.localScale /= size;
        }
    }
}

    