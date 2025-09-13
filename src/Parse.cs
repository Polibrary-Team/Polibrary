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
using MS.Internal.Xml.XPath;


namespace Polibrary;

public static class Parse
{
    private static ManualLogSource LogMan1997;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(Parse));
        LogMan1997 = logger;
        //LogMan1997.LogInfo("KRIS DON'T [Get The Heebie Jeebies] I'M STILL HERE");
        //LogMan1997.LogInfo("BUT NOW, I'M NOT [Calling] THE [[BIG SHOTS]] ANYMORE!!");
        //LogMan1997.LogInfo("[They] HAVE [Demoted] ME TO PERFORM [Medium] SHOTS??? I'M NO LONGER [XXL Family Size with 20% more value]!!!");
    }

    public static Dictionary<ImprovementData.Type, string> BuildersDict = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<ImprovementData.Type, string> NoBuildersDict = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<ImprovementData.Type, string> ImpBuildersDict = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<ImprovementData.Type, string> ImpCustomLocKey = new Dictionary<ImprovementData.Type, string>();
    public static Dictionary<TribeData.Type, string> leaderNameDict = new Dictionary<TribeData.Type, string>();
    public static Dictionary<ImprovementData.Type, int> defenceBoostDict = new Dictionary<ImprovementData.Type, int>();
    public static Dictionary<TribeData.Type, List<(ResourceData.Type, int)>> startingResources = new Dictionary<TribeData.Type, List<(ResourceData.Type, int)>>();
    public class PolibCityRewardData //oh boy its time to bake some lights, except its not lights and we're not baking anything and flowey undertale
    {
        public int productionModifier { get; set; }
        public int currencyReward { get; set; }
        public int populationReward { get; set; }
        public int scoreReward { get; set; }
        public int defenceBoostReward { get; set; }
        public int scoutSpawnAmount { get; set; }
        public int scoutMoveAmount { get; set; } = 15;
        public int borderGrowthAmount { get; set; } //yay now its useful
        public UnitData.Type unitType { get; set; }
        public int level { get; set; } = -1;
        public string persistence { get; set; } = "none";
        public int order { get; set; } = 0;
        public bool hidden { get; set; } = false;
        public int boostAttackOverSpawn { get; set; }
        public int boostDefenceOverSpawn { get; set; }
        public int boostMaxHpOverSpawn { get; set; }
        public int boostMovementOverSpawn { get; set; }
        public bool healUnitOverSpawn { get; set; } = false;
    }
    public class CityRewardOverrideClass
    {
        public CityReward og { get; set; }
        public CityReward neu { get; set; }
    }
    public class PolibUnitEffectData //So I haveth a Laser Pointre...
    {
        public int defenceMult { get; set; }
        public int attackMult { get; set; }
        public int attackAdd { get; set; }
        public int movementMult { get; set; }
        public int movementAdd { get; set; }
        public string color { get; set; }
        public List<string> removal { get; set; }
        public bool freezing { get; set; }
    }
    public static Dictionary<CityReward, PolibCityRewardData> cityRewardDict = new Dictionary<CityReward, PolibCityRewardData>();
    public static Dictionary<TribeData.Type, List<CityRewardOverrideClass>> cityRewardOverrideDict = new Dictionary<TribeData.Type, List<CityRewardOverrideClass>>();
    public static List<CityReward> rewardList = PolibUtils.MakeSystemList<CityReward>(CityRewardData.cityRewards);
    public static Dictionary<UnitEffect, PolibUnitEffectData> unitEffectDataDict = new Dictionary<UnitEffect, PolibUnitEffectData>();
    public class PolibUnitAbilityData
    {
        public int visionRadius { get; set; }
        public bool allowsFly { get; set; }
        public UnitEffect effect { get; set; }
        public string effectApplication { get; set; }
        public string effectApplicationActionTarget { get; set; }
    }
    public static Dictionary<UnitAbility.Type, PolibUnitAbilityData> unitAbilityDataDict = new Dictionary<UnitAbility.Type, PolibUnitAbilityData>();
    public static UnitEffect[] vanillaUnitEffects = new UnitEffect[] { UnitEffect.Boosted, UnitEffect.Bubble, UnitEffect.Frozen, UnitEffect.Invisible, UnitEffect.Petrified, UnitEffect.Poisoned };

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))] //dude why tf do you have 176387126 different patches for ts??
    private static void GameLogicData_Parse(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();

            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["BuiltBySpecific"] != null ? "BuiltBySpecific" : "builtBySpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        BuildersDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in BuildersDict");
                    }
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse5(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["BuiltOnSpecific"] != null ? "BuiltOnSpecific" : "builtOnSpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        ImpBuildersDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in ImpBuildersDict");
                    }
                }
            }
        }
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse4(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["NotBuiltBySpecific"] != null ? "NotBuiltBySpecific" : "notBuiltBySpecific";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        NoBuildersDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in NoBuildersDict");
                    }
                }
            }
        }
    }

    public static Dictionary<ImprovementData.Type, string> UnblockDict = new Dictionary<ImprovementData.Type, string>();

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse3(GameLogicData __instance, JObject rootObject)
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var impType))
                {
                    string key = token["Unblock"] != null ? "Unblock" : "unblock";
                    if (token[key] != null)
                    {
                        string ability = token[key]!.ToObject<string>();
                        UnblockDict[impType] = ability;
                        token.Remove(key);
                        LogMan1997.LogInfo($"Added {ability} ability to {impType} in UnblockDict");
                    }
                }
            }
        }
    }

    //thanks exploit
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse6(GameLogicData __instance, JObject rootObject) //in this world, its analfuck, or be analfucked
    {
        foreach (JToken jtoken in rootObject.SelectTokens("$.tribeData.*").ToList()) // "// tribeData!" -exploit, 2025
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<TribeData.Type>.TryGetType(token.Path.Split('.').Last(), out var tribeType))
                {
                    if (token["leaderName"] != null)
                    {
                        string leaderName = token["leaderName"]!.ToObject<string>();
                        leaderNameDict[tribeType] = leaderName;
                        token.Remove("leaderName");
                    }

                    /*
                    List<(ResourceData.Type, int)> startingResourcesList = new List<(ResourceData.Type, int)>();
                    if (token["startingResources"] != null)
                    {
                        JArray resarray = token["startingResources"].TryCast<JArray>();
                        if (resarray != null)
                        {
                            for (int i = 0; i < resarray.Count; i++)
                            {
                                JToken restoken = resarray[i];
                                ResourceData.Type restype = ResourceData.Type.Fruit;
                                int amount = -1;

                                JObject resobject = restoken.TryCast<JObject>(); //hey, I wrote this shit without using gpt! I'm proud of myself, and if you think otherwise, touch grass nerd!
                                if (resobject != null)
                                {
                                    if (resobject["resource"] != null)
                                    {
                                        EnumCache<ResourceData.Type>.TryGetType(resobject["resource"]!.ToObject<string>(), out restype);
                                    }
                                    if (resobject["amount"] != null)
                                    {
                                        amount = resobject["amount"]!.ToObject<int>();
                                    }
                                }
                                startingResourcesList.Add((restype, amount));
                            }
                        }
                        token.Remove("startingResources");
                        startingResources[tribeType] = startingResourcesList;
                    }
                    else
                    {
                        startingResources[tribeType] = new List<(ResourceData.Type, int)> { (ResourceData.Type.Fruit, -1) };
                    }
                    */

                    List<CityRewardOverrideClass> overlist = new List<CityRewardOverrideClass>();
                    foreach (JToken overtoken in token.SelectTokens("$.cityRewardOverrides.*").ToList())
                    {
                        if (EnumCache<CityReward>.TryGetType(overtoken.Path.Split('.').Last(), out var reward))
                        {
                            if (EnumCache<CityReward>.TryGetType(overtoken!.ToObject<string>(), out var overreward))
                            {
                                CityRewardOverrideClass overrideClass = new CityRewardOverrideClass
                                {
                                    og = reward,
                                    neu = overreward
                                };
                                overlist.Add(overrideClass);
                            }
                        }
                    }
                    cityRewardOverrideDict[tribeType] = overlist;
                }
            }
        }
        foreach (JToken jtoken in rootObject.SelectTokens("$.improvementData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<ImprovementData.Type>.TryGetType(token.Path.Split('.').Last(), out var improvementType))
                {
                    if (token["defenceBoost"] != null)
                    {
                        int amount = token["defenceBoost"]!.ToObject<int>();
                        defenceBoostDict[improvementType] = amount;
                        token.Remove("defenceBoost");
                    }
                }
            }
        }

        foreach (CityReward reward in CityRewardData.cityRewards) //default for vanilla cityRewards
        {
            cityRewardDict[reward] = PolibUtils.SetVanillaCityRewardDefaults(reward);
        }

        foreach (JToken jtoken in rootObject.SelectTokens("$.cityRewardData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<CityReward>.TryGetType(token.Path.Split('.').Last(), out var cityReward))
                {
                    PolibCityRewardData cityRewardData = new PolibCityRewardData();

                    if (token["addProduction"] != null)
                    {
                        int addProduction = token["addProduction"]!.ToObject<int>();
                        token.Remove("addProduction");
                        cityRewardData.productionModifier = addProduction;

                    }
                    if (token["currencyReward"] != null)
                    {
                        int currencyReward = token["currencyReward"]!.ToObject<int>();
                        token.Remove("currencyReward");
                        cityRewardData.currencyReward = currencyReward;

                    }
                    if (token["populationReward"] != null)
                    {
                        int populationReward = token["populationReward"]!.ToObject<int>();
                        token.Remove("populationReward");
                        cityRewardData.populationReward = populationReward;

                    }
                    if (token["scoreReward"] != null)
                    {
                        int scoreReward = token["scoreReward"]!.ToObject<int>();
                        token.Remove("scoreReward");
                        cityRewardData.scoreReward = scoreReward;

                    }
                    if (token["defenceBoost"] != null)
                    {
                        int defenceBoost = token["defenceBoost"]!.ToObject<int>();
                        token.Remove("defenceBoost");
                        cityRewardData.defenceBoostReward = defenceBoost;

                    }
                    if (token["scoutSpawnAmount"] != null)
                    {
                        int scoutSpawnAmount = token["scoutSpawnAmount"]!.ToObject<int>();
                        token.Remove("scoutSpawnAmount");
                        cityRewardData.scoutSpawnAmount = scoutSpawnAmount;

                    }
                    if (token["scoutMoveAmount"] != null)
                    {
                        int scoutMoveAmount = token["scoutMoveAmount"]!.ToObject<int>();
                        token.Remove("scoutMoveAmount");
                        cityRewardData.scoutMoveAmount = scoutMoveAmount;

                    }
                    if (token["borderGrowthAmount"] != null)
                    {
                        int borderGrowthAmount = token["borderGrowthAmount"]!.ToObject<int>();
                        token.Remove("borderGrowthAmount");
                        cityRewardData.borderGrowthAmount = borderGrowthAmount;

                    }
                    if (token["spawnUnit"] != null)
                    {
                        if (EnumCache<UnitData.Type>.TryGetType(token["spawnUnit"]!.ToObject<string>(), out var type))
                        {
                            cityRewardData.unitType = type;

                        }
                        token.Remove("spawnUnit");
                    }
                    if (token["level"] != null)
                    {
                        int level = token["level"]!.ToObject<int>();
                        token.Remove("level");
                        cityRewardData.level = level;

                    }
                    if (token["persistence"] != null)
                    {
                        string persistence = token["persistence"]!.ToObject<string>();
                        token.Remove("persistence");
                        cityRewardData.persistence = persistence;

                    }
                    if (token["order"] != null)
                    {
                        int order = token["order"]!.ToObject<int>();
                        token.Remove("order");
                        cityRewardData.order = order;

                    }
                    if (token["hidden"] != null)
                    {
                        bool hidden = token["hidden"]!.ToObject<bool>();
                        token.Remove("hidden");
                        cityRewardData.hidden = hidden;

                    }
                    if (token["boostAttackOverSpawn"] != null)
                    {
                        int boostAttackOverSpawn = token["boostAttackOverSpawn"]!.ToObject<int>();
                        token.Remove("boostAttackOverSpawn");
                        cityRewardData.boostAttackOverSpawn = boostAttackOverSpawn;

                    }
                    if (token["boostDefenceOverSpawn"] != null)
                    {
                        int boostDefenceOverSpawn = token["boostDefenceOverSpawn"]!.ToObject<int>();
                        token.Remove("boostDefenceOverSpawn");
                        cityRewardData.boostDefenceOverSpawn = boostDefenceOverSpawn;

                    }
                    if (token["boostMaxHpOverSpawn"] != null)
                    {
                        int boostMaxHpOverSpawn = token["boostMaxHpOverSpawn"]!.ToObject<int>();
                        token.Remove("boostMaxHpOverSpawn");
                        cityRewardData.boostMaxHpOverSpawn = boostMaxHpOverSpawn;

                    }
                    if (token["boostMovementOverSpawn"] != null)
                    {
                        int boostMovementOverSpawn = token["boostMovementOverSpawn"]!.ToObject<int>();
                        token.Remove("boostMovementOverSpawn");
                        cityRewardData.boostMovementOverSpawn = boostMovementOverSpawn;

                    }
                    if (token["healUnitOverSpawn"] != null)
                    {
                        bool healUnitOverSpawn = token["healUnitOverSpawn"]!.ToObject<bool>();
                        token.Remove("healUnitOverSpawn");
                        cityRewardData.healUnitOverSpawn = healUnitOverSpawn;

                    }
                    if (!rewardList.Contains(cityReward))
                    {
                        rewardList.Add(cityReward);
                    }
                    cityRewardDict[cityReward] = cityRewardData;
                }
            }
        }

        foreach (UnitEffect effect in vanillaUnitEffects)
        {
            unitEffectDataDict[effect] = PolibUtils.SetVanillaUnitEffectDefaults(effect);
        }

        foreach (JToken jtoken in rootObject.SelectTokens("$.unitEffectData.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<UnitEffect>.TryGetType(token.Path.Split('.').Last(), out var unitEffect))
                {
                    PolibUnitEffectData unitEffectData = new PolibUnitEffectData();

                    if (token["defenceMult"] != null)
                    {
                        int defenceMult = token["defenceMult"]!.ToObject<int>();
                        token.Remove("defenceMult");
                        unitEffectData.defenceMult = defenceMult;

                    }
                    if (token["attackAdd"] != null)
                    {
                        int attackAdd = token["attackAdd"]!.ToObject<int>();
                        token.Remove("attackAdd");
                        unitEffectData.attackAdd = attackAdd;

                    }
                    if (token["attackMult"] != null)
                    {
                        int attackMult = token["attackMult"]!.ToObject<int>();
                        token.Remove("attackMult");
                        unitEffectData.attackMult = attackMult;

                    }
                    if (token["movementAdd"] != null)
                    {
                        int movementAdd = token["movementAdd"]!.ToObject<int>();
                        token.Remove("movementAdd");
                        unitEffectData.movementAdd = movementAdd;

                    }
                    if (token["movementMult"] != null)
                    {
                        int movementMult = token["movementMult"]!.ToObject<int>();
                        token.Remove("movementMult");
                        unitEffectData.movementMult = movementMult;

                    }
                    if (token["color"] != null)
                    {
                        string color = token["color"]!.ToObject<string>();
                        token.Remove("color");
                        unitEffectData.color = color;
                    }
                    if (token["freezing"] != null)
                    {
                        bool freezing = token["freezing"]!.ToObject<bool>();
                        token.Remove("freezing");
                        unitEffectData.freezing = freezing;
                    }

                    unitEffectDataDict[unitEffect] = unitEffectData;
                }
            }
        }
    }
}