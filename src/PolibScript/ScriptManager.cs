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
    //
    //condition / expression grammar (lowest to highest precedence):
    //  orExpr   := andExpr ('/' andExpr)*
    //  andExpr  := notExpr ('&' notExpr)*
    //  notExpr  := '!' notExpr | compareExpr
    //  compareExpr := operand (('>=' | '<=' | '!=' | '>' | '<' | '=') operand)?
    //  operand  := variable | literal (int, bool, string, wcoords, area)
    //
    // '&' binds tighter than '/', matching standard boolean algebra (AND before OR).
    // e.g. "a/b&c" parses as "a OR (b AND c)".

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
                object condition = (parsedCondition is bool || parsedCondition is Variable || parsedCondition is Comparison) ? parsedCondition : true;

                MethodCall methodCall = new MethodCall(methodInfo, parameters, condition);

                list.Add(methodCall);
            }
            catch (System.Exception ex)
            {
                Main.modLogger.LogError($"WOAH [Mr./Mrs./O. 1000thCustomer], WAS THAT A [Big Fat F#&@up On Your End]?? \n'{line}' \n{ex}");
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

YO'URE'S TRULY,
[Every Buddy's Favorite Number 1 Rated Salesman1997]"); //yes this is necessary
                return null;
            }
        }

        return list;
    }

    public static Variable FindFirstVariableByName(string name, List<Variable> variables)
    {
        foreach (Variable v in variables)
        {
            if (v.Name == name)
            {
                return v;
            }
        }
        return null;
    }

    public static object ParseParam(string param)
    {
        return ParseOr(param.Trim());
    }

    // orExpr := andExpr ('/' andExpr)*
    private static object ParseOr(string expr)
    {
        List<string> parts = SplitTopLevel(expr, OrSymbol);

        if (parts.Count == 1)
        {
            return ParseAnd(parts[0]);
        }

        object result = ParseAnd(parts[0]);
        for (int i = 1; i < parts.Count; i++)
        {
            object right = ParseAnd(parts[i]);
            result = new Comparison(result, BoolOp.Or, right);
        }
        return result;
    }

    // andExpr := notExpr ('&' notExpr)*
    private static object ParseAnd(string expr)
    {
        List<string> parts = SplitTopLevel(expr, AndSymbol);

        if (parts.Count == 1)
        {
            return ParseNot(parts[0]);
        }

        object result = ParseNot(parts[0]);
        for (int i = 1; i < parts.Count; i++)
        {
            object right = ParseNot(parts[i]);
            result = new Comparison(result, BoolOp.And, right);
        }
        return result;
    }

    // notExpr := '!' notExpr | compareExpr
    private static object ParseNot(string expr)
    {
        string trimmed = expr.Trim();

        if (trimmed.Length > 0 && trimmed[0] == NotSymbol)
        {
            object inner = ParseNot(trimmed.Substring(1));
            return new Comparison(inner, BoolOp.Not, null);
        }

        return ParseCompare(trimmed);
    }

    // compareExpr := operand (compareOp operand)?
    private static object ParseCompare(string expr)
    {
        // Check two-character operators first so '>=' isn't mistaken for '>' + '='.
        foreach (var op in new[]
        {
            (NotEqualSymbol, BoolOp.NotEqual),
            (GreaterEqualSymbol, BoolOp.GreaterEqual),
            (LesserEqualSymbol, BoolOp.LesserEqual),
        })
        {
            int idx = FindTopLevel(expr, op.Item1);
            if (idx >= 0)
            {
                object left = ParseOperand(expr.Substring(0, idx).Trim());
                object right = ParseOperand(expr.Substring(idx + op.Item1.Length).Trim());
                return new Comparison(left, op.Item2, right);
            }
        }

        foreach (var op in new[]
        {
            (GreaterSymbol, BoolOp.Greater),
            (LesserSymbol, BoolOp.Lesser),
            (EqualSymbol, BoolOp.Equal),
        })
        {
            int idx = FindTopLevel(expr, op.Item1);
            if (idx >= 0)
            {
                object left = ParseOperand(expr.Substring(0, idx).Trim());
                object right = ParseOperand(expr.Substring(idx + 1).Trim());
                return new Comparison(left, op.Item2, right);
            }
        }

        // No comparison operator found - it's a plain operand.
        return ParseOperand(expr);
    }

    // Finds index of a single-char operator at the top level (not inside quotes).
    // Skips matches that are actually part of a wider 2-char operator (handled separately).
    private static int FindTopLevel(string expr, char target)
    {
        bool inString = false;
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == StringSymbol) inString = !inString;
            if (inString) continue;

            if (c == target)
            {
                // Avoid splitting on '=' or '>'/'<' that are actually part of '!=' / '>=' / '<='
                if (target == EqualSymbol && i > 0 && (expr[i - 1] == NotSymbol || expr[i - 1] == GreaterSymbol || expr[i - 1] == LesserSymbol))
                    continue;
                return i;
            }
        }
        return -1;
    }

    private static int FindTopLevel(string expr, string target)
    {
        bool inString = false;
        for (int i = 0; i <= expr.Length - target.Length; i++)
        {
            char c = expr[i];
            if (c == StringSymbol) inString = !inString;
            if (inString) continue;

            if (expr.Substring(i, target.Length) == target)
                return i;
        }
        return -1;
    }

    // Splits on a top-level boolean operator character, ignoring occurrences inside quoted strings.
    private static List<string> SplitTopLevel(string expr, char separator)
    {
        List<string> result = new();
        bool inString = false;
        int start = 0;

        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];

            if (c == StringSymbol) inString = !inString;

            if (!inString && c == separator)
            {
                result.Add(expr.Substring(start, i - start));
                start = i + 1;
            }
        }

        result.Add(expr.Substring(start));
        return result;
    }

    // operand := variable | literal
    private static object ParseOperand(string param)
    {
        if (string.IsNullOrEmpty(param))
        {
            return param;
        }

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

public enum BoolOp
{
    Greater,
    Lesser,
    GreaterEqual,
    LesserEqual,
    Equal,
    NotEqual,
    Not,
    And,
    Or
}

// Represents a comparison or boolean combination, to be evaluated at runtime
// once Variable values are resolved (e.g. Left/Right may be Variable instances
// whose .Value hasn't been populated yet at parse time).
public struct Comparison
{
    public object Left;
    public BoolOp Op;
    public object Right; // null when Op == BoolOp.Not (unary)

    public Comparison(object left, BoolOp op, object right)
    {
        Left = left;
        Op = op;
        Right = right;
    }
}

public class Variable
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