using BepInEx.Logging;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using Polytopia.Data;
using UnityEngine;


namespace Polibrary;

public static class Main
{


    public static ManualLogSource modLogger;
    public static void Load(ManualLogSource logger)
    {
        new Harmony("com.polibraryteam.polibrary").PatchAll(); //???
        modLogger = logger;
        logger.LogMessage("Polibrary.dll loaded.");
        modLogger.LogMessage("Version 2.1");
        PolyMod.Loader.AddPatchDataType("cityRewardData", typeof(CityReward)); //casual fapingvin carry
        PolyMod.Loader.AddPatchDataType("unitEffectData", typeof(UnitEffect)); //casual fapingvin carry... ...again
        PolyMod.Loader.AddPatchDataType("unitAbilityData", typeof(UnitAbility.Type)); //...casual...      ...fapingvin carry...       ...again
        PolyMod.Loader.AddPatchDataType("tileEffectData", typeof(TileData.EffectType));
        ClassInjector.RegisterTypeInIl2Cpp<CameraShake>();
        Directory.CreateDirectory(PolibSave.DATA_PATH);
    }
    public static PolibGameState polibGameState;

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
}

public class CameraShake : MonoBehaviour
{
    public CameraShake(System.IntPtr ptr) : base(ptr) { }

    public float shakeDuration = 0f;
    public float shakeAmount = 0.5f;
    public float decreaseFactor = 1.0f;
    private Vector3 originalPos;
    private bool isShaking = false;

    void LateUpdate()
    {

        if (shakeDuration > 0)
        {
            // If this is the first frame of the shake, capture the position
            if (!isShaking)
            {
                originalPos = transform.localPosition;
                isShaking = true;
            }

            transform.localPosition = Vector3.Lerp(transform.localPosition, originalPos + UnityEngine.Random.insideUnitSphere * shakeAmount, Time.deltaTime * 3);
            shakeDuration -= Time.deltaTime * decreaseFactor;
        }
        else if (isShaking)
        {
            shakeDuration = 0f;
            transform.localPosition = originalPos;
            isShaking = false;
        }
    }

    public void TriggerShake(float duration, float amount)
    {
        shakeDuration = duration;
        shakeAmount = amount;
    }
}
