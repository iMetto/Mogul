using System;
using Mogul.Systems;
using UnityEngine;

namespace Mogul.UI;

public static class CheckoutUI
{
    private const float WindowWidth = 480f;
    private const float RowHeight = 36f;
    private const float MaxListHeight = 200f;
    private const float HeaderHeight = 52f;
    private const float FooterHeight = 44f;
    private const float Padding = 10f;

    private static Vector2 _scrollPos;
    private static CursorLockMode _savedLockMode;
    private static bool _savedCursorVisible;
    private static bool _cursorCaptured;

    // Cached styles — created once inside OnGUI so GUI.skin is available
    private static GUIStyle _rowStyle;
    private static GUIStyle _labelStyle;
    private static GUIStyle _closeStyle;
    private static GUIStyle _boxStyle;

    public static void Draw()
    {
        if (!CheckoutHandler.IsOpen)
        {
            if (_cursorCaptured) ReleaseCursor();
            return;
        }

        if (!_cursorCaptured) CaptureCursor();

        EnsureStyles();

        var products = CheckoutHandler.Products;

        // Keyboard shortcuts
        if (Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Q)
            {
                CheckoutHandler.Dismiss();
                Event.current.Use();
                return;
            }

            for (int i = 0; i < Math.Min(products.Count, 9); i++)
            {
                if (Event.current.keyCode == (KeyCode)((int)KeyCode.Alpha1 + i))
                {
                    CheckoutHandler.Sell(products[i]);
                    Event.current.Use();
                    return;
                }
            }
        }

        float listHeight = Mathf.Min(products.Count * RowHeight, MaxListHeight);
        float windowHeight = HeaderHeight + listHeight + FooterHeight;
        float x = (Screen.width - WindowWidth) / 2f;
        float y = (Screen.height - windowHeight) / 2f;

        // Background
        GUI.Box(new Rect(x, y, WindowWidth, windowHeight), "", _boxStyle);

        GUILayout.BeginArea(new Rect(x + Padding, y + Padding, WindowWidth - Padding * 2, windowHeight - Padding * 2));

        GUILayout.Label("  Customer is waiting — select a product to sell", _labelStyle);
        GUILayout.Space(4f);

        // Scrollable product list
        StorageProduct toSell = null;
        _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(listHeight));

        for (int i = 0; i < products.Count; i++)
        {
            var p = products[i];
            string shortcut = i < 9 ? $"[{i + 1}]" : "   ";
            string label = $"  {shortcut}  {p.DisplayName}   ·   {p.QualityName}   ·   ${p.Price:F0}   ·   {p.TotalPackages} in stock";

            if (GUILayout.Button(label, _rowStyle, GUILayout.Height(RowHeight)))
                toSell = p;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(4f);

        bool dismiss = GUILayout.Button("  [Q]  Close menu (customer keeps waiting)", _closeStyle, GUILayout.Height(30f));

        GUILayout.EndArea();

        // Act after layout pass to avoid IMGUI state corruption
        if (toSell != null) CheckoutHandler.Sell(toSell);
        else if (dismiss) CheckoutHandler.Dismiss();
    }

    private static void EnsureStyles()
    {
        if (_rowStyle != null) return;

        _rowStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleLeft,
        };
        _rowStyle.padding = new RectOffset(6, 6, 4, 4);

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
        };
        _labelStyle.normal.textColor = Color.white;

        _closeStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
        };

        _boxStyle = new GUIStyle(GUI.skin.box);
    }

    private static void CaptureCursor()
    {
        _savedLockMode = Cursor.lockState;
        _savedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _cursorCaptured = true;
    }

    private static void ReleaseCursor()
    {
        Cursor.lockState = _savedLockMode;
        Cursor.visible = _savedCursorVisible;
        _cursorCaptured = false;
    }
}
