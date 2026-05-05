using Il2CppScheduleOne.PlayerScripts;
using Mogul.Systems;
using UnityEngine;

namespace Mogul.UI;

public static class CheckoutUI
{
    private const float WindowWidth = 480f;
    private const float RowHeight   = 44f;
    private const float MaxRows     = 6f;
    private const float HeaderH     = 44f;
    private const float FooterH     = 40f;
    private const float Pad         = 16f;

    private static Vector2 _scrollPos;
    private static CursorLockMode _savedLock;
    private static bool _savedVisible;
    private static bool _cursorCaptured;

    private static GUIStyle _nameStyle;
    private static GUIStyle _subStyle;
    private static GUIStyle _headerStyle;
    private static GUIStyle _hintStyle;
    private static GUIStyle _totalStyle;

    private static Texture2D _bgTex;
    private static Texture2D _divTex;

    public static void Draw()
    {
        if (!CheckoutHandler.IsOpen)
        {
            if (_cursorCaptured) ReleaseCursor();
            return;
        }

        if (!_cursorCaptured) CaptureCursor();
        FreezeCamera();
        EnsureStyles();

        var order = CheckoutHandler.CustomerOrder;

        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Q)
            {
                CheckoutHandler.Dismiss();
                Event.current.Use();
                return;
            }
            if (Event.current.keyCode == KeyCode.E)
            {
                CheckoutHandler.FulfillOrder();
                Event.current.Use();
                return;
            }
        }

        float listH = order.Count > 0
            ? Mathf.Min(order.Count * RowHeight, MaxRows * RowHeight)
            : RowHeight;
        float winH = HeaderH + listH + FooterH;
        float wx   = (Screen.width  - WindowWidth) * 0.5f;
        float wy   = (Screen.height - winH) * 0.5f;

        GUI.DrawTexture(new Rect(wx, wy, WindowWidth, winH), _bgTex, ScaleMode.ScaleAndCrop);

        // Header
        GUI.Label(new Rect(wx + Pad, wy + 12f, WindowWidth * 0.65f, 22f),
            "Customer order", _headerStyle);
        GUI.Label(new Rect(wx + WindowWidth - 104f, wy + 14f, 92f, 18f),
            "[Q]  dismiss", _hintStyle);

        // Divider below header
        GUI.DrawTexture(new Rect(wx, wy + HeaderH - 1f, WindowWidth, 1f), _divTex);

        // Order rows
        bool hasScroll  = order.Count * RowHeight > listH;
        float innerW    = WindowWidth - (hasScroll ? 14f : 0f);
        float contentH  = Mathf.Max(order.Count, 1) * RowHeight;

        _scrollPos = GUI.BeginScrollView(
            new Rect(wx, wy + HeaderH, WindowWidth, listH),
            _scrollPos,
            new Rect(0, 0, innerW, contentH),
            false, false
        );

        if (order.Count == 0)
        {
            GUI.Label(new Rect(Pad, (RowHeight - 16f) * 0.5f, innerW - Pad, 16f),
                "No items", _subStyle);
        }
        else
        {
            var savedColor = GUI.color;
            for (int i = 0; i < order.Count; i++)
            {
                var   item = order[i];
                float ry   = i * RowHeight;
                string sub = $"{item.QualityName}  ·  ${item.Price:F0}/pkg  ·  {item.Quantity} pkg  =  ${item.Total:F0}";

                if (i > 0)
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.05f);
                    GUI.DrawTexture(new Rect(Pad, ry, innerW - Pad, 1f), Texture2D.whiteTexture);
                    GUI.color = savedColor;
                }

                GUI.Label(new Rect(Pad,       ry + 8f,  innerW - Pad, 17f), item.DisplayName, _nameStyle);
                GUI.Label(new Rect(Pad,       ry + 25f, innerW - Pad, 14f), sub,              _subStyle);
            }
        }

        GUI.EndScrollView();

        // Footer: total + confirm hint
        float footerY = wy + HeaderH + listH;
        GUI.DrawTexture(new Rect(wx, footerY, WindowWidth, 1f), _divTex);

        float orderTotal = 0f;
        foreach (var item in order) orderTotal += item.Total;

        GUI.Label(new Rect(wx + Pad, footerY + 11f, WindowWidth * 0.5f, 18f),
            $"Total  ${orderTotal:F0}", _totalStyle);
        GUI.Label(new Rect(wx + WindowWidth - 104f, footerY + 12f, 92f, 18f),
            "[E]  confirm", _hintStyle);

        // Click anywhere on the footer to confirm
        if (GUI.Button(new Rect(wx, footerY, WindowWidth, FooterH), GUIContent.none, GUIStyle.none))
            CheckoutHandler.FulfillOrder();
    }

    private static void FreezeCamera()
    {
        try
        {
            var cam = PlayerCamera.Instance;
            if (cam == null) return;
            cam.mouseX = 0f;
            cam.mouseY = 0f;
        }
        catch { }
    }

    private static void EnsureStyles()
    {
        if (_nameStyle != null) return;

        _bgTex  = BuildGrainTex(64);
        _divTex = BuildDivTex();

        _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        _nameStyle.normal.textColor = new Color(0.90f, 0.90f, 0.88f);

        _subStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
        _subStyle.normal.textColor = new Color(0.46f, 0.46f, 0.44f);

        _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13 };
        _headerStyle.normal.textColor = new Color(0.88f, 0.88f, 0.85f);

        _totalStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
        };
        _totalStyle.normal.textColor = new Color(0.88f, 0.88f, 0.85f);

        _hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.MiddleRight,
        };
        _hintStyle.normal.textColor = new Color(0.36f, 0.36f, 0.34f);
    }

    private static Texture2D BuildGrainTex(int size)
    {
        var rng = new System.Random(7);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode   = TextureWrapMode.Repeat,
        };
        for (int py = 0; py < size; py++)
            for (int px = 0; px < size; px++)
            {
                float g = (float)rng.NextDouble() * 0.06f;
                tex.SetPixel(px, py, new Color(0.04f + g, 0.04f + g, 0.05f + g, 0.91f));
            }
        tex.Apply();
        return tex;
    }

    private static Texture2D BuildDivTex()
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.08f));
        tex.Apply();
        return tex;
    }

    private static void CaptureCursor()
    {
        _savedLock    = Cursor.lockState;
        _savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
        _cursorCaptured  = true;
    }

    private static void ReleaseCursor()
    {
        Cursor.lockState = _savedLock;
        Cursor.visible   = _savedVisible;
        _cursorCaptured  = false;
    }
}
