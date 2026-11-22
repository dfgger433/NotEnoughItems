using UnityEngine;

namespace NotEnoughItems;

/// <summary>Hides the stock tooltip through the same state GlobalDark itself uses.</summary>
internal static class NativeTooltipLayer
{
    /// <summary>
    /// GlobalDark.Update clears tooltipText immediately after HandleTooltip, so this
    /// mirrors the stock hidden branch for the skipped frame: an already visible root
    /// cannot survive as a ghost, and the next normal frame restores itself.
    /// </summary>
    internal static void Hide(GlobalDark ui)
    {
        if (!ui) return;
        ui.SetTooltip(("", ""));
        if (!ui.tooltipRect) return;
        ui.tooltipRect.gameObject.SetActive(false);
        ui.tooltipRect.localScale = new Vector3(.1f, .1f, 1f);
    }
}
