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

        PolibUtils.SetVanillaCityRewardDefaults();
    }

    public static UnitEffect[] vanillaUnitEffects = new UnitEffect[] 
    { 
        UnitEffect.Boosted, 
        UnitEffect.Bubble, 
        UnitEffect.Frozen, 
        UnitEffect.Invisible, 
        UnitEffect.Petrified, 
        UnitEffect.Poisoned, 
        UnitEffect.Charmed, 
        UnitEffect.Swift, 
        UnitEffect.DoubleReady 
    };
    public static List<CityReward> rewardList = CityRewardData.cityRewards.ToList();
    public static List<PolibImprovementData> polibImprovementDatas = new();
    public static List<PolibUnitData> polibUnitDatas = new();
    public static List<PolibTribeData> polibTribeDatas = new();
    public static List<PolibCityRewardData> polibCityRewardDatas = new();
    public class PolibUnitEffectData //So I haveth a Laser Pointre...
    {
        public Dictionary<string, int> additives = new Dictionary<string, int>();
        public Dictionary<string, double> multiplicatives = new Dictionary<string, double>();
        public UnityEngine.Color? color = null;
        public List<string> removal { get; set; }
        public bool freezing { get; set; }
    }
    public static Dictionary<UnitEffect, PolibUnitEffectData> unitEffectDataDict = new();





    #region Parse

    static void HandleUnits(JObject token, bool onCreatedEnumCache)
    {
        static PolibUnitData f() => new(); // PolibUnitData Factory
        ParseUtils.ParseWithHandler<UnitData.Type, bool, PolibUnitData>(token, "hiddenItem", polibUnitDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibUnitData, UnitData.Type, TechData.Type>(token, "obsoleteBy", polibUnitDatas, f);
    }
    static void HandleImprovements(JObject token, bool onCreatedEnumCache)
    {
        static PolibImprovementData f() => new(); // PolibImprovementData Factory
        ParseUtils.ParseWithHandler<ImprovementData.Type, float, PolibImprovementData>(token, "aiScore", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, int, PolibImprovementData>(token, "defenceBoost", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, int, PolibImprovementData>(token, "defenceBoost_Neutral", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, string, PolibImprovementData>(token, "builtOnSpecific", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, string, PolibImprovementData>(token,"unblock", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, string, PolibImprovementData>(token, "infoOverride", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, bool, PolibImprovementData>(token,"canTrain", polibImprovementDatas, f);
        ParseUtils.ParseWithHandler<ImprovementData.Type, bool, PolibImprovementData>(token, "hiddenItem", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitAbility.Type>(token, "unitAbilityWhitelist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitAbility.Type>(token, "unitAbilityBlacklist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitData.Type>(token, "unitWhitelist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, UnitData.Type>(token, "unitBlacklist", polibImprovementDatas, f);
        ParseUtils.ParseWithHandlerIntoArray<PolibImprovementData, ImprovementData.Type, TechData.Type>(token, "obsoleteBy", polibImprovementDatas, f);
    }
    static void HandleTribes(JObject token, bool onCreatedEnumCache)
    {
        static PolibTribeData f() => new();
        ParseUtils.ParseWithHandler<TribeType, string, PolibTribeData>(token, "leaderName", polibTribeDatas, f);
        ParseUtils.ParseToDictWithHandler<TribeType, TerrainData.Type, TerrainData.Type, PolibTribeData>(token, "terrainOverrides", polibTribeDatas, f);
        ParseUtils.ParseToDictWithHandler<TribeType, ResourceData.Type, ResourceData.Type, PolibTribeData>(token, "resourceOverrides", polibTribeDatas, f);
        ParseUtils.ParseToDictWithHandler<TribeType, CityReward, CityReward, PolibTribeData>(token, "cityRewardOverrides", polibTribeDatas, f);
    }
    static void HandleCityRewards(JObject token, bool onCreatedEnumCache)
    {
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
        ParseUtils.ParseWithHandler<CityReward, string, PolibCityRewardData>(token, "persistence", polibCityRewardDatas, f);
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
    
    #endregion
    //thanks exploit
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    private static void GameLogicData_Parse(object[] __args, MethodBase __originalMethod, GameLogicData __instance/*, JObject rootObject*/)
    {
        JObject rootObject;
        try
        {
            rootObject = (JObject)__args[0];
        }
        catch (Exception ex)
        {
            Main.modLogger.LogError($"Update Fuckup: Params got changed? in: {__originalMethod.Name} \n{ex}");
            return;
        }

        #region UnitEffect

        foreach (JToken jtoken in rootObject.SelectTokens("$.unitEffect.*").ToList())
        {
            JObject token = jtoken.TryCast<JObject>();
            if (token != null)
            {
                if (EnumCache<UnitEffect>.TryGetType(token.Path.Split('.').Last(), out var unitEffect))
                {
                    PolibUnitEffectData unitEffectData = new PolibUnitEffectData();

                    if (token["add"] != null)
                    {
                        unitEffectData.additives = PolibUtils.ParseStringDict<int>(token["add"]);
                        token.Remove("add");
                    }
                    if (token["mult"] != null)
                    {
                        unitEffectData.multiplicatives = PolibUtils.ParseStringDict<double>(token["mult"]);
                        token.Remove("mult");
                    }

                    if (token["color"] != null)
                    {
                        string val = token["color"]!.ToObject<string>();
                        string[] vals = val.Split(',');

                        float r = 0;
                        float g = 0;
                        float b = 0;
                        float a = 1;

                        float.TryParse(vals[0], out r);
                        float.TryParse(vals[1], out g);
                        float.TryParse(vals[2], out b);
                        float.TryParse(vals[3], out a);

                        unitEffectData.color = new UnityEngine.Color(r, g, b, a);
                        token.Remove("color");
                    }

                    unitEffectDataDict[unitEffect] = unitEffectData;
                }
            }
        }

        #endregion
    }
}