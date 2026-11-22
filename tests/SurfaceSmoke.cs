using System.Reflection;
using NotEnoughItems;

internal static class SurfaceSmoke
{
    internal static void Run()
    {
        var surface = typeof(Catalog).Assembly.GetType("NotEnoughItems.GameSurface", throwOnError: true)!;
        var resolve = surface.GetMethod("ResolveKind", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool terminalReportedOpen = false;
        int terminalProbes = 0;
        bool TerminalOpen()
        {
            terminalProbes++;
            return terminalReportedOpen;
        }

        void Expect(string expected, string scenario, bool inventoryOpen, bool containerOpen = false,
            bool blockingScreen = false, bool terminalInstalled = true)
        {
            Func<bool>? terminal = terminalInstalled ? TerminalOpen : null;
            var actual = resolve.Invoke(null, [blockingScreen, containerOpen, terminal, inventoryOpen]);
            if (actual?.ToString() != expected)
                throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}.");
        }

        Expect("Closed", "Gameplay with no inventory open", inventoryOpen: false, terminalInstalled: false);
        Expect("Inventory", "Native backpack without the optional terminal", inventoryOpen: true, terminalInstalled: false);
        Expect("Inventory", "Native backpack with the terminal closed", inventoryOpen: true);
        Expect("Closed", "Container opened while the radial still reports open", inventoryOpen: true, containerOpen: true);

        terminalReportedOpen = true;
        Expect("Terminal", "Terminal with no native radial", inventoryOpen: false);
        Expect("Terminal", "Terminal using the native radial", inventoryOpen: true);
        Expect("Terminal", "Terminal hides the native container but leaves its state open", inventoryOpen: true, containerOpen: true);
        int probesBeforeBlock = terminalProbes;
        Expect("Closed", "Native secondary screen while both inventories still report open", inventoryOpen: true, blockingScreen: true);
        if (terminalProbes != probesBeforeBlock)
            throw new InvalidOperationException("A native blocker must win before querying the optional terminal.");
        Expect("Terminal", "Terminal resumes when the native blocker closes", inventoryOpen: true);
        terminalReportedOpen = false;
        Expect("Closed", "Native container becomes visible again when the terminal closes", inventoryOpen: true, containerOpen: true);
        Expect("Inventory", "Native backpack resumes after the terminal and container close", inventoryOpen: true);
        Expect("Closed", "Native secondary screen with no optional terminal", inventoryOpen: true, blockingScreen: true, terminalInstalled: false);
    }
}
