using BepInEx.Logging;
using HarmonyLib;
using Polytopia.Data;
using Newtonsoft.Json.Linq;
using Il2CppSystem.Linq;

using pbb = PolytopiaBackendBase.Common;
using PolyMod;
using System.Reflection;
using PolytopiaBackendBase.Common;


namespace Polibrary.Parsing;

public static class Parse
{
    private static ManualLogSource LogMan1997;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(Parse));
        LogMan1997 = logger;
        PolyMod.Loader.AddPatchDataType("cityReward", typeof(CityReward));
        PolyMod.Loader.AddPatchDataType("unitEffect", typeof(UnitEffect));
        PolyMod.Loader.AddPatchDataType("tileEffect", typeof(TileData.EffectType));
        Loader.AddTypeHandler(typeof(ImprovementData.Type), HandleImprovements);
        Loader.AddTypeHandler(typeof(UnitData.Type), HandleUnits);
        Loader.AddTypeHandler(typeof(CityReward), HandleCityRewards);
        Loader.AddTypeHandler(typeof(TribeType), HandleTribes);
        Loader.AddTypeHandler(typeof(UnitEffect), HandleUnitEffects);

        PolibUtils.SetVanillaCityRewardDefaults();
    }

    public static List<CityReward> rewardList = CityRewardData.cityRewards.ToList();
    public static List<PolibImprovementData> polibImprovementDatas = new();
    public static List<PolibUnitData> polibUnitDatas = new();
    public static List<PolibTribeData> polibTribeDatas = new();
    public static List<PolibCityRewardData> polibCityRewardDatas = new();
    public static List<PolibUnitEffectData> polibUnitEffectDatas = new();
    
    //public static Dictionary<UnitEffect, PolibUnitEffectData> unitEffectDataDict = new();





    #region HANDLERS

    static void HandleUnits(JObject token, bool onCreatedEnumCache)
    {
        if (onCreatedEnumCache) return;

        static PolibUnitData f() => new(); // PolibUnitData Factory
        ParseUtils.ParseWithHandler<UnitData.Type, bool, PolibUnitData>(token, "hiddenItem", polibUnitDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibUnitData, UnitData.Type, TechData.Type>(token, "obsoleteBy", polibUnitDatas, f);
    }
    static void HandleImprovements(JObject token, bool onCreatedEnumCache)
    {
        if (onCreatedEnumCache) return;
        static PolibImprovementData f() => new(); // PolibImprovementData Factory
        ParseUtils.ParseWithHandler<ImprovementData.Type, float, PolibImprovementData>(token, "aiScore", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, int, PolibImprovementData>(token, "defenceBoost", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, int, PolibImprovementData>(token, "defenceBoost_Neutral", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler_<ImprovementData.Type, string, PolibImprovementData>(token, "builtOnSpecific", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler_<ImprovementData.Type, string, PolibImprovementData>(token,"unblock", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler_<ImprovementData.Type, string, PolibImprovementData>(token, "infoOverride", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, bool, PolibImprovementData>(token,"canTrain", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, bool, PolibImprovementData>(token, "hiddenItem", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitAbility.Type>(token, "unitAbilityWhitelist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitAbility.Type>(token, "unitAbilityBlacklist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitData.Type>(token, "unitWhitelist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitData.Type>(token, "unitBlacklist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, TechData.Type>(token, "obsoleteBy", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitEffect>(token, "appliesOnBuild", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitEffect>(token, "appliesOnStep", polibImprovementDatas, f);
    }
    static void HandleTribes(JObject token, bool onCreatedEnumCache)
    {
        if (onCreatedEnumCache) return;

        static PolibTribeData f() => new();
        ParseUtils.ParseWithHandler_<TribeType, string, PolibTribeData>(token, "leaderName", polibTribeDatas, f);
        ParseUtils.ParseToDictWithHandler<TribeType, TerrainData.Type, TerrainData.Type, PolibTribeData>(token, "terrainOverrides", polibTribeDatas, f);
        ParseUtils.ParseToDictWithHandler<TribeType, ResourceData.Type, ResourceData.Type, PolibTribeData>(token, "resourceOverrides", polibTribeDatas, f);
        ParseUtils.ParseToDictWithHandler<TribeType, CityReward, CityReward, PolibTribeData>(token, "cityRewardOverrides", polibTribeDatas, f);
    }
    static void HandleCityRewards(JObject token, bool onCreatedEnumCache)
    {
        if (onCreatedEnumCache) return;

        static PolibCityRewardData f() => new();
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "addProduction", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "currencyReward", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "populationReward", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "scoreReward", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "defenceBoost", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "scoutSpawnAmount", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "scoutMoveAmount", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "borderGrowthAmount", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, UnitData.Type, PolibCityRewardData>(token, "unitType", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "level", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler_<CityReward, string, PolibCityRewardData>(token, "persistence", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, int, PolibCityRewardData>(token, "order", polibCityRewardDatas, f);
        ParseUtils.ParseWithHandler<CityReward, bool, PolibCityRewardData>(token, "hidden", polibCityRewardDatas, f);

        if (EnumCache<CityReward>.TryGetType(token.Path.Split('.').Last(), out var type))
        {
            if (!rewardList.Contains(type))
            {
                rewardList.Add(type);
            }
        }
    }

    static void HandleUnitEffects(JObject token, bool onCreatedEnumCache)
    {
        if(onCreatedEnumCache) return;
        static PolibUnitEffectData f() => new();

        ParseUtils.ParseWithHandler<UnitEffect, UnityEngine.Color, PolibUnitEffectData>(token, "color", polibUnitEffectDatas, f);
        ParseUtils.ParseWithHandler<UnitEffect, bool, PolibUnitEffectData>(token, "hidden", polibUnitEffectDatas, f);
        ParseUtils.ParseToDictWithHandlerKeyString<UnitEffect, string, int, PolibUnitEffectData>(token, "add", polibUnitEffectDatas, f);
        ParseUtils.ParseToDictWithHandlerKeyString<UnitEffect, string, double, PolibUnitEffectData>(token, "multiply", polibUnitEffectDatas, f);
        
    }
    
    #endregion

    // Finally could erase the God-Parsing method
}