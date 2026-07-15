using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Polytopia.Data;
using Polibrary.PolyScript;


namespace Polibrary;

public static class Main
{
    //public static PolibGameState polibGameState;
    public static Dictionary<IntPtr, pAction> waitList = new Dictionary<IntPtr, pAction>();
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony harmony = new Harmony("com.polibraryteam.polibrary"); //???
        harmony.PatchAll();
        Harmony.CreateAndPatchAll(typeof(Main));
        modLogger = logger;
        logger.LogMessage("Polibrary.dll loaded.");
        modLogger.LogMessage("Version 2.1.4");
        PolyMod.Loader.AddPatchDataType("cityRewardData", typeof(CityReward)); //casual fapingvin carry
        PolyMod.Loader.AddPatchDataType("unitEffectData", typeof(UnitEffect)); //casual fapingvin carry... ...again
        PolyMod.Loader.AddPatchDataType("tileEffectData", typeof(TileData.EffectType));
        ClassInjector.RegisterTypeInIl2Cpp<CameraShake>();
        //Directory.CreateDirectory(PolibSave.DATA_PATH);


        var myPostfix = new HarmonyMethod(typeof(Main).GetMethod(nameof(Main.MethodThing))); //a brief thanks to our sponsor, chatGPT!

        HashSet<string> targetActions = new HashSet<string> 
        {
            "IncreaseCurrencyAction",
            "BuildAction",
            "DestroyImprovementAction",
            "TrainAction",
            "AttackAction",
            "ConvertAction",
            "ExploreAction",
            "PromoteAction",
            "UpgradeAction",
            "RevealAction",
            "KillUnitAction",
            "RecoverAction",
            "ResearchAction",
            "RuleAreaAction",
            "CityRewardAction",
            "IncreaseScoreAction",
            "DecreaseScoreAction"
        };

        var actionTypes = typeof(ActionBase).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ActionBase)) && !t.IsAbstract);

        int count = 0;
        foreach (var actionType in actionTypes)
        {
            if (!targetActions.Contains(actionType.Name)) continue;

            var executeMethod = actionType.GetMethod("Execute", new Type[] { typeof(GameState) });
            if (executeMethod != null)
            {
                try 
                {
                    harmony.Patch(executeMethod, postfix: myPostfix);
                    count++;
                }
                catch (Exception e)
                {
                    modLogger.LogWarning($"Failed {actionType.Name}: {e.Message}");
                }
            }
        }
        modLogger.LogInfo($"Dynamically patched {count} Action types!");
    }

    public static void MethodThing(ActionBase __instance)
    {
        if (waitList.TryGetValue(__instance.Pointer, out var action))
        {
            waitList.Remove(__instance.Pointer);
            action.Execute();
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(GameLogicData), nameof(GameLogicData.AddGameLogicPlaceholders))]
    public static void LateLoad()
    {
        PolibActionManager.RegisterAction<PolibAction>("polibaction");
    }
}
    // Good for quick reference getting:
    /*using System.ComponentModel;
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
    using Il2Gen = Il2CppSystem.Collections.Generic;*/
































    //I snuck this in, and he didn't even realize! I'm so sneaky! He'll never figure it out!
    //he figured it out 
    /*



                                                                                ███████        ████████                                                                                       
                                                                           ████                   ██████                                                                                      
                                                                       ████                   █████                                                                                           
                                                                     ███                   ███                                                                                                
                                                                   ███                  ████                                                                                                  
                                                                 ███                  ███                                                                                                     
                                                               ███                 ████                                                                                                       
                                                              ███                 ██                                                                                                          
                                                             ███                 ██                                                                                                           
                                                            ██                  ██                                                                                                            
                                                           ██                   ██               █████                                                                                        
                                                          ██                      ███   ████████████████████                                                                                  
                                                         ███                        █████████████████████████████                                                                             
                                                         ██                     ███████████      █████████████████████                              ██                                        
                                                         ██                █████████████                ████████████████████                       ███                                        
                                                         ██            █████████████                        ████████████████████████  ██████     ███ ██                                       
                                                          ██        ██████████████                              ████████████████████████████   ███   ██                                       
                                                        ████    █████████████████                    ██            ██████████████████████   ████     ██                                       
                                                       ███████████████████████                         ██            ███████████████████████         ██                                       
                                                       ██████████████████████        ███████             ██           █████████████████              ██                                       
                                                       █████████████████████            ██████            ██            ████████████████             ██                                       
                                                       ████████████████████                ████            ██            ███████████████            ██                                        
                                                       ██████████████████                    ███            ████          ██████████████           ██                                         
                                                     ███████████████████              ███████████                          █████████████          ██                                          
                                                  █████████████████████            ██ █  ██████████                        ██████████████       ██                                            
                                                 █████████████████████                  █████    ███                        █████████████      ██                                             
                                               ██████████████████████                          █   ██       █                ███████████     ███                                              
                                               █████████████████████                                     ███        ███████  ███████████   ███                                                
                                              █████████████████████         ██                              ███████████      ██████████████                                                   
                                             █████████████████████          ███                                  ████ ██     ████████████                                                     
                                             ████████████████████          █████                                         █  ████████████                                                      
                                            █████████████████████          ███████                                          █████████                                                         
                                            ████████████████████          ███████████          ███    ██                   █████████                                                          
                                            ███████████████████        ███████████████████               ██                ████████                                                           
                                    ███████████████████████████       █████████████████████                  ██           ████████                                                            
                      █████████████████     ██████████████████        ████████████████████████                            ████████                                                            
              ████████████                 ██████████████████        █████████    ████████████                            ████████                                                            
        █████████                         ███████████████████       █████████    ██       ████   ██                       ████████                                                            
                                        ██  ████████████████        █████████     ██         ███  ██              ███     █████████                                                           
                                      ███   ███   ██████████       ██████████      █           ███                 ████   █████████                                                           
                                     ██             ████████      █████████         █           █████      █        ████  █████████                                                           
                                    ██               ███████      ████████           ███          ██████          █████████████████   ██                                                      
                                  ███                ████████    ███████                ███           ███████████       ████████████████                                                      
                                 ██                  █████████   ██████     █              ██                              ███████████                                                        
                                ██                  ███████████████████ █████          █      ████                          ██████████                                                        
                               ██                ██████████████████████████             ███       █████                   █  ███████████                                                      
                             ███             █████████████████████████████                 ███           █████        █████   ███████████                                                     
                             ██               ███████████████████████████                      █                               ██████████                                                     
                           ███                 ██████████████████████████                                                       █████████                                                     
                          ██                     ████████████████████████                                                       ███   ██                                                      
                          ██                       ████████████████████████                                         ██         ███     ██                                                     
                         ██                          ██████████████████████                                      ███           ██      ██                                                     
                        ██                             █████████████████████                                  ███              ██       ██                                                    
                        ██                               ███████████████████                             █████                ██        ██                                                    
                       ██                                  ██████████████████                                                ██          █                                                    
                       ██                                   █████████████████                                               ██           ██                                                   
                      ██                                  ██ ██████████████████                                            ██             ██                                                  
                     ██                                  ███  ████████████████████            ██                          █               ███                                                 
                     ██                                 ██     █████████████████████         ██                          ██               █████                                               
                     ██                                ██       ███████████████████████    █████     ██                 ██                ██  ███                                             
                     ██                               ██         ████████████████████████████████   ████     ███       ██                 █     ███                                           
                     █                              ██           ████████████████████████████████████████████████    ███                 ██       ██                                          
                    ██                            ███             █████████████████████████████████████████████████████                  █         ███                                        
                    ██                           ███               ███████████████████████████████████████████████████                  ██          ███                                       
                     █                          ██                  ████████████████████████████████████████████████                   ██             ██                                      
                     ██                        ██                   ███████████████████████████████████████████████                  ██                ███                                    
                     ██                      ██                      ████████████████████████████████████████████████             ███                   ███                                   
                      ██                   ███                        █████████████████████████████████████████      ███████  █████                      ███                                  
                       ██                 ██                           ███████████████████████████████████████                                            ███                                 
        */
