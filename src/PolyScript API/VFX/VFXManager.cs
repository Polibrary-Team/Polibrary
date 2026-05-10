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
    public static List<string> RegisteredPuffs = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
    private static void Debug_Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            GameState state = GameManager.GameState;
            state.TryGetPlayer(state.CurrentPlayer, out var player);
            Tile tile = state.Map.GetTile(player.GetCurrentCapitalCoordinates(state)).GetInstance();
            



            string puffId = "Puff";
            Transform parentTransform = tile.transform;
            Vector3 localPosition = tile.VisualCenterObject.localPosition;

            tile.DoPuff(puffId, parentTransform, localPosition);
        }
        if (Input.GetKeyDown(KeyCode.O))
        {
            GameState state = GameManager.GameState;
            state.TryGetPlayer(state.CurrentPlayer, out var player);
            Tile tile = state.Map.GetTile(player.GetCurrentCapitalCoordinates(state)).GetInstance();
            



            string puffId = "CustomPuff";
            Transform parentTransform = tile.transform;
            Vector3 localPosition = tile.VisualCenterObject.localPosition;

            EnsureCustomPuffRegistered(puffId, "Puff");
            tile.DoPuff(puffId, parentTransform, localPosition);
        }
    }

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
    [HarmonyPatch(typeof(Puff), nameof(Puff.StartAnimation), typeof(Transform), typeof(Vector3) )]
    private static void Puff_StartAnimation(Puff __instance, Transform parentTransform, Vector3 localPosition)
    {
        string idInPool = __instance.gameObject.name;
        if (SpriteMappings.TryGetValue(idInPool.ToLower(), out var sprite))
        {
            __instance.spriteRenderer.sprite = sprite;
        }
        if (SizeMappings.TryGetValue(idInPool.ToLower(), out var size))
        {
            Main.modLogger.LogInfo($"size for {idInPool}: {size}");
            __instance.spriteRenderer.transform.localScale *= size;
        }
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

    