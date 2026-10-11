using Il2CppSystem.IO;
using Newtonsoft.Json.Linq;
using HarmonyLib;

namespace Polibrary.Json;

public static class JsonMerger
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(PolyMod.Json.JsonMerger), nameof(PolyMod.Json.JsonMerger.Merge))]
    private static void MergePatch(JObject original, ref JObject patch)
    {
        ExpandKeys(patch);
    }
    private static void ExpandKeys(JObject obj)
    {
        foreach (var property in obj.Properties().ToArray().ToList())
        {
            if (property == null || property.Name == null)
                continue;

            JToken value = property.Value;

            if (value.Type == JTokenType.Object)
            {
                ExpandKeys(value.Cast<JObject>());
            }
            else if (value.Type == JTokenType.Array)
            {
                foreach (var item in value.Cast<JArray>()._values.ToArray().ToList())
                    if (item.Type == JTokenType.Object)
                        ExpandKeys(item.Cast<JObject>());
            }

            if (!property.Name.Contains("/"))
                continue;

            string[] names = property.Name
                .Split('/')
                .Select(n => n.Trim())
                .Where(n => n.Length > 0)
                .ToArray();

            property.Remove();

            foreach (string name in names)
            {
                JToken copy = value.DeepClone();
                JToken existing = obj[name];

                if (existing != null && existing.Type == JTokenType.Object && copy.Type == JTokenType.Object)
                    existing.Cast<JObject>().Merge(copy.Cast<JObject>());
                else
                    obj[name] = copy;
            }
        }
    }
}