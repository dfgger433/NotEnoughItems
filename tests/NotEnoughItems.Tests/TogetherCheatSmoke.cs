using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using HarmonyLib;
using NotEnoughItems;

internal static class TogetherCheatSmoke
{
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run()
    {
        var nei = typeof(Catalog).Assembly;
        var bridge = nei.GetType("NotEnoughItems.TogetherCheats", throwOnError: true)!;
        var dispatch = typeof(ConsoleScript).GetMethod("TryExecuteCommand", Instance, [typeof(string[]), typeof(bool)])!;
        var transpiler = bridge.GetMethod("WrapConsoleInput", Static)!;
        // The game's compile-time reference DLL may not have method bodies.
        // Verify the exact call replacement and that an unexpected call layout fails closed.
        CodeInstruction[] call = [new(OpCodes.Call, dispatch)];
        var wrapped = (IEnumerable<CodeInstruction>)transpiler.Invoke(null, [call])!;
        if (wrapped.Single().operand as MethodInfo != bridge.GetMethod("RunLocalConsoleInput", Static))
            throw new InvalidOperationException("NEI must replace the direct console input call.");
        try
        {
            transpiler.Invoke(null, [Array.Empty<CodeInstruction>()]);
            throw new InvalidOperationException("An unidentified console input call must not enable grant management.");
        }
        catch (TargetInvocationException e) when (e.InnerException is InvalidOperationException) { }

        var pluginDir = Path.Combine(AppContext.BaseDirectory, "Together");
        var togetherPath = Path.Combine(pluginDir, "CasualtiesMP.dll");
        if (!File.Exists(togetherPath)) return; // Optional integration; no Unity or network startup.
        AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(pluginDir, "LiteNetLib.dll"));
        var together = AssemblyLoadContext.Default.LoadFromAssemblyPath(togetherPath);
        var apiType = nei.GetType("NotEnoughItems.TogetherCheatApi", throwOnError: true)!;
        var api = Activator.CreateInstance(apiType, Instance, binder: null, [together], culture: null)!;
        var callbacks = (Delegate[])apiType.GetMethod("BindCallbacks", Instance)!.Invoke(api,
            [bridge.GetMethod("ReceiveRequest", Static), bridge.GetMethod("ReceivePermission", Static), bridge.GetMethod("ForgetPlayer", Static)])!;
        if (callbacks.Length != 3) throw new InvalidOperationException("Together callback signatures did not bind.");
    }
}
