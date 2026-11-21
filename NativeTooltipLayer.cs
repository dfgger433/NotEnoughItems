using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NotEnoughItems;

internal static class NativeTooltipLayer
{
    private static Camera? _captureCamera;
    private static RenderTexture? _captureTexture;
    private static readonly List<(GameObject Object, bool Active)> SiblingState = new();
    private static bool _capturing;

    // Canvas sorting cannot order a ScreenSpaceOverlay canvas against IMGUI. The
    // tooltip is captured after NEI paints and then composited as the last texture.
    internal static void Raise(GlobalDark ui) { }

    internal static void RenderAfterGui(GlobalDark? ui)
    {
        if (_capturing || Event.current?.type != EventType.Repaint || !ui ||
            !ui!.tooltipRect || !ui.tooltipRect.gameObject.activeInHierarchy || !ui.canvas)
            return;
        Canvas canvas = ui.canvas;
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay || Screen.width < 1 || Screen.height < 1)
            return;

        _capturing = true;
        try
        {
            EnsureResources(canvas, ui.tooltipRect);
            if (!_captureCamera || !_captureTexture) return;
            CaptureTooltip(canvas, ui.tooltipRect);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _captureTexture,
                ScaleMode.StretchToFill, true);
        }
        catch (Exception)
        {
            // Rendering a duplicate must never interrupt the stock tooltip update.
        }
        finally
        {
            _capturing = false;
        }
    }

    private static void CaptureTooltip(Canvas canvas, RectTransform tooltip)
    {
        var canvasTransform = (RectTransform)canvas.transform;
        var rect = canvasTransform.rect;
        Vector3 center = canvasTransform.TransformPoint(rect.center);
        Vector3 normal = canvasTransform.forward;
        float planeDistance = Mathf.Max(10f, rect.height);
        _captureCamera!.transform.position = center - normal * planeDistance;
        _captureCamera.transform.rotation = canvasTransform.rotation;
        _captureCamera.orthographicSize = Mathf.Max(1f, rect.height * .5f);
        _captureCamera.aspect = Mathf.Max(.01f, rect.width / Mathf.Max(1f, rect.height));
        _captureCamera.cullingMask = TooltipLayers(tooltip);
        _captureCamera.targetTexture = _captureTexture;
        _captureCamera.clearFlags = CameraClearFlags.SolidColor;
        _captureCamera.backgroundColor = new Color(0, 0, 0, 0);

        RenderMode oldMode = canvas.renderMode;
        Camera? oldCamera = canvas.worldCamera;
        float oldDistance = canvas.planeDistance;
        try
        {
            SetSiblings(canvas.transform, tooltip);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _captureCamera;
            canvas.planeDistance = planeDistance;
            Canvas.ForceUpdateCanvases();
            _captureTexture!.DiscardContents();
            _captureCamera!.Render();
        }
        finally
        {
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            canvas.planeDistance = oldDistance;
            RestoreSiblings();
            _captureCamera.targetTexture = null;
        }
    }

    private static void SetSiblings(Transform canvasRoot, Transform tooltip)
    {
        SiblingState.Clear();
        for (int i = 0; i < canvasRoot.childCount; i++)
        {
            var child = canvasRoot.GetChild(i).gameObject;
            bool keep = tooltip == child.transform || tooltip.IsChildOf(child.transform);
            SiblingState.Add((child, child.activeSelf));
            if (!keep) child.SetActive(false);
        }
    }

    private static void RestoreSiblings()
    {
        foreach (var (obj, active) in SiblingState)
            if (obj) obj.SetActive(active);
        SiblingState.Clear();
    }

    private static int TooltipLayers(Transform tooltip)
    {
        int mask = 0;
        foreach (var component in tooltip.GetComponentsInChildren<Transform>(true))
            mask |= 1 << component.gameObject.layer;
        return mask == 0 ? ~0 : mask;
    }

    private static void EnsureResources(Canvas canvas, RectTransform tooltip)
    {
        var rect = (RectTransform)canvas.transform;
        int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
        if (!_captureTexture || _captureTexture!.width != width || _captureTexture.height != height)
        {
            if (_captureTexture) UnityEngine.Object.Destroy(_captureTexture);
            _captureTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "NEI.NativeTooltipCapture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _captureTexture.Create();
        }
        if (!_captureCamera)
        {
            var go = new GameObject("NEI.NativeTooltipCaptureCamera");
            go.hideFlags = HideFlags.HideAndDontSave;
            _captureCamera = go.AddComponent<Camera>();
            _captureCamera.enabled = false;
            _captureCamera.nearClipPlane = .01f;
            _captureCamera.farClipPlane = 5000f;
            _captureCamera.orthographic = true;
        }
        _captureCamera!.pixelRect = new Rect(0, 0, width, height);
        _captureCamera.targetTexture = _captureTexture;
    }

    internal static void Restore() { }

    internal static void Release()
    {
        RestoreSiblings();
        if (_captureTexture) UnityEngine.Object.Destroy(_captureTexture);
        if (_captureCamera) UnityEngine.Object.Destroy(_captureCamera!.gameObject);
        _captureTexture = null;
        _captureCamera = null;
    }
}
