using System.ComponentModel;
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
using Il2Gen = Il2CppSystem.Collections.Generic;
using System.Reflection.Metadata;
using UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler;
using AsmResolver.PE.DotNet.ReadyToRun;

namespace Polibrary;

public static class ScriptManager
{
    //syntax:
    //method?condition:param,param,param
    //method ? condition: param, param, param

    private static readonly char MethodSymbol = ':';
    private static readonly char ConditionSymbol = '?';
    private static readonly char ParamSeparatorSymbol = ',';
    private static readonly char StringSymbol = '\'';
    private static readonly char CoordinatePairSymbol = ';';
    private static readonly char AreaSeparatorSymbol = '|';
    private static readonly char VariableSymbol = '@';
    private static readonly char GreaterSymbol = '>';
    private static readonly char LesserSymbol = '<';
    private static readonly string GreaterEqualSymbol = ">=";
    private static readonly string LesserEqualSymbol = "<=";
    private static readonly char EqualSymbol = '=';
    private static readonly string NotEqualSymbol = "!=";
    private static readonly char NotSymbol = '!';
    private static readonly char OrSymbol = '/';
    private static readonly char AndSymbol = '&';

    public static List<MethodCall> Read(string[] lines, System.Type type)
    {
        List<MethodCall> list = new();

        foreach (string line in lines)
        {
            try
            {
                string frontString = line.Split(MethodSymbol, 2)[0];
                string parametersStringUnsplit = line.Split(MethodSymbol, 2)[1];

                string methodName = frontString.Split(ConditionSymbol, 2)[0].Trim();
                string conditionString = frontString.Split(ConditionSymbol, 2).Length != 1 ? frontString.Split(ConditionSymbol, 2)[1].Trim() : "b"; //b (its important)

                string[] parametersString = parametersStringUnsplit.Split(ParamSeparatorSymbol);

                List<object> parameters = new();

                foreach (string rawparam in parametersString)
                {
                    object param = ParseParam(rawparam.Trim());

                    parameters.Add(param);
                }

                MethodInfo methodInfo = type.GetMethod(methodName);

                if (methodInfo == null)
                {
                    Main.modLogger.LogError($"METHOD DOESN'T F#&@ING [Exist] DU$&#SS! DID YOU MAKE A [Typo or other f*cky-wucky]??: '{line}'");
                    return null;
                }

                object parsedCondition = ParseParam(conditionString);
                object condition = (parsedCondition is bool || parsedCondition is Variable) ? parsedCondition : true;

                MethodCall methodCall = new MethodCall(methodInfo, parameters, condition);
                
                list.Add(methodCall);
            }
            catch (System.Exception ex)
            {
                Main.modLogger.LogError($"WOAH [Mr./Mrs./O. 1000thCustomer], WAS THAT A [Big Fat F#&@up On Your End]?? I'M SO PROUD OF YOU, MY HEART(s) IS [Hyperlink Allowed]: '{line}'");
                Main.modLogger.LogError($"ALSO I [Found And Definitely Not Stole] THIS FOR YOU: {ex}");
                Main.modLogger.LogInfo(
@"⣿⣿⣿⣿⣿⣿⡟⠛⠃⠀⠀⠀⠀⠀⠀⠘⠛⠛⠛⠛⠛⠃⠀⠀
⣿⣿⣿⣿⡟⠁⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠀⠉
⣿⣿⣿⣿⣧⣄⠀⠀⠀⣤⣤⣤⣤⣤⣤⡤⠀⠀⠀⠀⠀⠀⣀⣴
⣿⣿⣿⣿⣿⡿⠇⠀⠈⠉⠩⠍⠉⠉⠉⠹⢿⣤⠀⠀⠀⠀⣉⣿
⣿⣿⣿⣿⣿⡇⠀⣿⣿⣿⡀⢸⣿⣿⣇⠀⢈⣉⠀⠀⣶⣶⣿⣿
⣿⣿⣿⣿⣿⡷⠆⠉⠉⢁⣠⣎⣉⣉⠉⠰⢾⣿⣴⠀⣿⠿⠿⣿
⣿⠿⠿⠏⣉⣁⣶⣶⣶⣶⣿⣿⣿⠟⠀⢀⣸⣿⠿⠀⠉⠀⠀⠉
⣉⣈⣀⣀⣉⡁⢈⣉⡀⢁⣈⣁⣉⠀⢰⣾⣿⠈⠀⠀⠀⣀⣶⣶
⣿⣿⣿⣿⣿⣷⡆⠈⠳⠄⢀⣀⡀⢰⣾⣿⠀⠀⣰⣤⣶⣿⣿⣿
⣿⣿⣿⣿⣿⠃⠀⠀⠐⢄⣀⣀⣀⣼⣿⠀⠀⠀⡄⣿⣿⣿⣿⣿
⣿⣿⣿⣿⠛⠀⠀⠀⠀⠀⢠⣀⣀⠀⠀⠀⠀⠀⠀⠀⣿⣿⣿⣿
⣿⡿⠛⠀⠀⠀⠀⠀⠀⠀⠀⢘⠃⠀⠀⠀⠀⠠⠀⠀⠄⣿⣿⣿
⠀⢠⡟⠀⠀⣼⡇⠀⠀⠀⠀⠀⠀⠀⠀⠀⢤⣿⠀⠀⠀⡄⠛⣿
⣿⣤⣤⣤⣿⣿⣿⡇⠀⠀⠀⠀⠀⠀⠀⠀⢸⣿⣿⣤⠀⠛⠃⠀
⣿⣿⣿⣿⣿⣿⣿⣿⡇⠀⠀⠀⠀⠀⠀⢹⣿⣿⣿⣿⣿⣿⣿⣿
⣿⣿⣿⣿⣿⣿⣿⣿⣧⡄⢸⡇⢹⠿⠀⢸⣿⣿⣿⣿⣿⣿⣿⣿
⣿⣿⣿⣿⣿⣿⣿⣿⣿⣧⣌⣁⣈⣀⣸⣿⣿⣿⣿⣿⣿⣿⣿⣿

        YOURE'S TRULY,
        [Every Buddy's Favorite Number 1 Rated Salesman1997]"); //yes this is necessary
                return null;
            }
        }

        return list;
    }

    public static object ParseParam(string param)
    {
        if (param[0] == VariableSymbol)
        {
            return new Variable(param.Trim('@'));
        }

        if (param[0] == StringSymbol && param[param.Length - 1] == StringSymbol)
        {
            return param.Trim(StringSymbol);
        }

        if (int.TryParse(param, out var intParam))
        {
            return intParam;
        }

        if (bool.TryParse(param, out var boolParam))
        {
            return boolParam;
        }

        if (param.Contains(CoordinatePairSymbol))
        {
            if (param.Contains(AreaSeparatorSymbol))
            {
                List<WorldCoordinates> area = new();
                foreach (string subParam in param.Split(AreaSeparatorSymbol))
                {
                    if (int.TryParse(subParam.Split(CoordinatePairSymbol, 2)[0].Trim(), out var x) && int.TryParse(subParam.Split(CoordinatePairSymbol, 2)[1].Trim(), out var y))
                    {
                        area.Add(new WorldCoordinates(x, y));
                    }
                }
                return area;
            }
            else
            {
                if (int.TryParse(param.Split(CoordinatePairSymbol, 2)[0].Trim(), out var x) && int.TryParse(param.Split(CoordinatePairSymbol, 2)[1].Trim(), out var y))
                {
                    return new WorldCoordinates(x, y);
                }
            }
        }

        return param;
    }
}

public struct Variable
{
    public string Name;
    public object Value;

    public Variable(string name, object value = null)
    {
        Name = name;
        Value = value;
    }
}

public struct MethodCall
{
    public MethodInfo MethodInfo;
    public List<object> Params;
    public object Condition;

    public MethodCall(MethodInfo method, List<object> param, object condition = null)
    {
        MethodInfo = method;
        Params = param;

        if (condition == null)
        {
            Condition = true;
        }
        else
        {
            Condition = condition;
        }
    }
}