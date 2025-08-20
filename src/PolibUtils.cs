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


namespace Polibrary;

public static class PolibUtils
{
    private static ManualLogSource utilGuy;
    public static void Load(ManualLogSource logger)
    {
        utilGuy = logger;
        utilGuy.LogInfo("I ran out of ideas");
    }
    public static Il2CppSystem.Collections.Generic.List<T> ToIl2CppList<T>(System.Collections.Generic.List<T> sysList)
    {
        var il2cppList = new Il2CppSystem.Collections.Generic.List<T>();
        for (int i = 0; i < sysList.Count; i++)
        {
            il2cppList.Add(sysList[i]);
        }
        return il2cppList;
    }
    public static List<T> ToSystemList<T>(Il2Gen.List<T> il2cppList)
    {
        var sysList = new List<T>(il2cppList.Count);
        for (int i = 0; i < il2cppList.Count; i++)
        {
            sysList.Add(il2cppList[i]);
        }
        return sysList;
    }
    public static T[] ArrayFromListIl2Cpp<T>(Il2Gen.List<T> il2cppList)
    {
        T[] array = new T[il2cppList.Count];
        for (int i = 0; i < il2cppList.Count; i++)
        {
            array[i] = il2cppList[i];
        }
        return array;
    }
    public static T[] ArrayFromListSystem<T>(List<T> sysList)
    {
        T[] array = new T[sysList.Count];
        for (int i = 0; i < sysList.Count; i++)
        {
            array[i] = sysList[i];
        }
        return array;
    }
    public static T[] MakeSystemArray<T>(T value)
    {
        return new T[] { value };
    }
    public static List<T> MakeSystemList<T>(T[] array)
    {
        return new List<T>(array);
    }
    public static int GetRewardCountForPlayer(byte playerId, CityReward[] targetRewards)
    {
        GameManager.GameState.TryGetPlayer(playerId, out var playerState);
        Il2Gen.List<TileData> tiles = playerState.GetCityTiles(GameManager.GameState);
        int num = 0;
        foreach (TileData tile in tiles)
        {
            Il2Gen.List<CityReward> rewards = tile.improvement.rewards;
            foreach (CityReward checkedReward in rewards)
            {
                if (targetRewards != null)
                {
                    foreach (CityReward compareToThisReward in targetRewards)
                    {
                        if (checkedReward == compareToThisReward)
                        {
                            num++;
                        }
                    }
                }
            }
        }
        return num;
    }
    public static int GetRewardCountForPlayer(byte playerId, CityReward targetReward)
    {
        return GetRewardCountForPlayer(playerId, MakeSystemArray(targetReward));
    }
    public static CityReward[] GetSpawningRewardsForUnit(UnitData.Type unit)
    {
        List<CityReward> list = new List<CityReward>();
        foreach (CityReward reward in Main.rewardList)
        {
            if (Main.cityRewardDict.TryGetValue(reward, out var cityRewardData))
            {
                if (cityRewardData.unitType == unit)
                {
                    list.Add(reward);
                }
            }
        }
        return ArrayFromListSystem(list);
    }
    public static Main.PolibCityRewardData GetRewardData(CityReward reward)
    {
        Main.cityRewardDict.TryGetValue(reward, out var data);
        return data;
    }
}