using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using MonoMod.RuntimeDetour;
using System.Reflection;

namespace Polibrary;

static class NativeHooks
{
    delegate int GetCommandTypeDelegate(IntPtr instance, IntPtr methodInfo);
    static GetCommandTypeDelegate? original;
    static GetCommandTypeDelegate? hookDelegate; // keep alive!
    static NativeDetour? detour;
    
    public static void Apply()
    {
        Main.modLogger.LogInfo("Applying native hook...");
        var classPtr = Il2CppClassPointerStore<CommandBase>.NativeClassPtr;
        var methodPtr = IL2CPP.il2cpp_class_get_method_from_name(classPtr, "GetCommandType", 0);
        var nativeFuncPtr = Marshal.ReadIntPtr(methodPtr);
        
        Main.modLogger.LogInfo($"Method ptr: {methodPtr}, Native func ptr: {nativeFuncPtr}");
        
        hookDelegate = Hook; // store reference before passing to NativeDetour
        detour = new NativeDetour(nativeFuncPtr, Marshal.GetFunctionPointerForDelegate(hookDelegate));
        original = detour.GenerateTrampoline<GetCommandTypeDelegate>();
        Main.modLogger.LogInfo("Native hook applied!");
    }

    static int Hook(IntPtr instance, IntPtr methodInfo)
    {
        Main.modLogger.LogInfo("GetCommandType hook called!");
        if (instance != IntPtr.Zero)
        {
            try
            {
                var obj = new CommandBase(instance);
                var polibCmd = obj.TryCast<PolibCommandBase>();
                if (polibCmd != null)
                {
                    Main.modLogger.LogInfo("Found PolibCommandBase!");
                    return (int)polibCmd.GetCommandTypeNew();
                }
            }
            catch { }
        }
        return original!(instance, methodInfo);
    }
}