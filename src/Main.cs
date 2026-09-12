using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Polytopia.Data;
using Polibrary.PolyScript;


namespace Polibrary;

public static class Main
{
    //public static PolibGameState polibGameState;
    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        Harmony.CreateAndPatchAll(typeof(Main));
        Harmony.CreateAndPatchAll(typeof(VFXManager));
        Harmony.CreateAndPatchAll(typeof(CityRewardManager));
        Harmony.CreateAndPatchAll(typeof(ImprovementManager));
        Harmony.CreateAndPatchAll(typeof(PolibUtils));
        Harmony.CreateAndPatchAll(typeof(TribeManager));
        Harmony.CreateAndPatchAll(typeof(UI));
        Harmony.CreateAndPatchAll(typeof(UnitManager));

        Harmony.CreateAndPatchAll(typeof(PolibReactionManager));
        ClassInjector.RegisterTypeInIl2Cpp<PolibActionBase>();
        Harmony.CreateAndPatchAll(typeof(PolibCommandManager));
        ClassInjector.RegisterTypeInIl2Cpp<PolibCommandBase>();
        Harmony.CreateAndPatchAll(typeof(PolibActionManager));
        ClassInjector.RegisterTypeInIl2Cpp<PolibActionBase>();

        modLogger = logger;
        logger.LogMessage("Polibrary.dll loaded.");
        modLogger.LogMessage("Version 2.1.5");
        PolyMod.Loader.AddPatchDataType("cityReward", typeof(CityReward)); //casual fapingvin carry
        PolyMod.Loader.AddPatchDataType("unitEffect", typeof(UnitEffect)); //casual fapingvin carry... ...again
        PolyMod.Loader.AddPatchDataType("tileEffect", typeof(TileData.EffectType));
        ClassInjector.RegisterTypeInIl2Cpp<CameraShake>();
        //Directory.CreateDirectory(PolibSave.DATA_PATH);
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
