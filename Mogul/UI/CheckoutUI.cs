using Il2CppScheduleOne.PlayerScripts;
using Mogul.Systems;
using UnityEngine;

namespace Mogul.UI;

public static class CheckoutUI
{
    private const float WindowWidth = 520f;
    private const float Pad = 18f;

    private static CursorLockMode _savedLock;
    private static bool _savedVisible;
    private static bool _cursorCaptured;

    private static GUIStyle _headerStyle;
    private static GUIStyle _bodyStyle;
    private static GUIStyle _mutedStyle;
    private static GUIStyle _totalStyle;
    private static GUIStyle _keyStyle;
    private static Texture2D _panelTex;
    private static Texture2D _lineTex;

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

        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.E)
            {
                CheckoutHandler.FulfillOrder();
                Event.current.Use();
                return;
            }
            if (Event.current.keyCode == KeyCode.Q)
            {
                CheckoutHandler.Deny();
                Event.current.Use();
                return;
            }
            if (Event.current.keyCode == KeyCode.W)
            {
                CheckoutHandler.Dismiss();
                Event.current.Use();
                return;
            }
        }

        var order = CheckoutHandler.CustomerOrder;
        float total = 0f;
        foreach (var item in order) total += item.Total;

        float contentRows = Mathf.Max(1, order.Count);
        float winH = 148f + contentRows * 34f;
        float wx = (Screen.width - WindowWidth) * 0.5f;
        float wy = (Screen.height - winH) * 0.5f;

        GUI.DrawTexture(new Rect(wx, wy, WindowWidth, winH), _panelTex, ScaleMode.StretchToFill);
        GUI.Label(new Rect(wx + Pad, wy + 14f, WindowWidth - Pad * 2f, 24f), "COUNTER ORDER", _headerStyle);
        GUI.DrawTexture(new Rect(wx + Pad, wy + 46f, WindowWidth - Pad * 2f, 1f), _lineTex);

        float y = wy + 58f;
        if (order.Count == 0)
        {
            GUI.Label(new Rect(wx + Pad, y, WindowWidth - Pad * 2f, 24f), "No items requested.", _mutedStyle);
            y += 34f;
        }
        else
        {
            foreach (var item in order)
            {
                GUI.Label(new Rect(wx + Pad, y, WindowWidth * 0.47f, 24f), item.DisplayName, _bodyStyle);
                GUI.Label(new Rect(wx + WindowWidth * 0.52f, y, WindowWidth * 0.18f, 24f), item.Quantity + " pkg", _mutedStyle);
                GUI.Label(new Rect(wx + WindowWidth * 0.70f, y, WindowWidth * 0.26f - Pad, 24f), "$" + item.Total.ToString("F0"), _bodyStyle);
                y += 34f;
            }
        }

        GUI.DrawTexture(new Rect(wx + Pad, y + 4f, WindowWidth - Pad * 2f, 1f), _lineTex);
        y += 16f;

        GUI.Label(new Rect(wx + Pad, y, WindowWidth * 0.45f, 30f), "TOTAL", _mutedStyle);
        GUI.Label(new Rect(wx + WindowWidth * 0.52f, y, WindowWidth * 0.42f, 30f), "$" + total.ToString("F0"), _totalStyle);
        y += 40f;

        GUI.Label(new Rect(wx + Pad, y, WindowWidth - Pad * 2f, 24f),
            "[E] Accept     [Q] Deny     [W] Cancel", _keyStyle);
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
        if (_headerStyle != null) return;

        _panelTex = BuildTex(new Color(0.12f, 0.12f, 0.12f, 0.94f));
        _lineTex = BuildTex(new Color(1f, 1f, 1f, 0.10f));

        _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
        _headerStyle.normal.textColor = new Color(0.90f, 0.90f, 0.88f);

        _bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        _bodyStyle.normal.textColor = new Color(0.86f, 0.86f, 0.84f);

        _mutedStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
        _mutedStyle.normal.textColor = new Color(0.58f, 0.58f, 0.56f);

        _totalStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        _totalStyle.normal.textColor = new Color(0.90f, 0.76f, 0.20f);

        _keyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        _keyStyle.normal.textColor = new Color(0.74f, 0.74f, 0.72f);
    }

    private static Texture2D BuildTex(Color color)
    {
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, color);
        tex.Apply();
        return tex;
    }

    private static void CaptureCursor()
    {
        _savedLock = Cursor.lockState;
        _savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _cursorCaptured = true;
    }

    private static void ReleaseCursor()
    {
        Cursor.lockState = _savedLock;
        Cursor.visible = _savedVisible;
        _cursorCaptured = false;
    }
}
