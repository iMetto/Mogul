using System;
using System.Collections.Generic;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Mogul.Data;
using Mogul.Systems;
using S1API.PhoneApp;
using UnityEngine;
using UnityEngine.UI;

namespace Mogul.Apps;

public partial class MogulApp
{
    private void BuildQuestPanel(GameObject container)
    {
        _questPanel = new GameObject("QuestPanel");
        _questPanel.transform.SetParent(container.transform, false);
        var pr = _questPanel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(0f, 0f);
        pr.anchorMax = new Vector2(1f, 0.82f);
        pr.sizeDelta = Vector2.zero;
        _questPanel.AddComponent<Image>().color = new Color(ColorPurple.r * 0.18f, ColorPurple.g * 0.18f, ColorPurple.b * 0.18f, 1f);

        var texQuests = LoadModTexture("questsLandscape.png");
        if (texQuests != null)
        {
            var bgImg = new GameObject("BgImage");
            bgImg.transform.SetParent(_questPanel.transform, false);
            var ri = bgImg.AddComponent<RawImage>();
            ri.texture = texQuests;
            ri.color = new Color(1f, 1f, 1f, 0.28f);
            var bir = EnsureRect(bgImg);
            bir.anchorMin = Vector2.zero;
            bir.anchorMax = Vector2.one;
            bir.sizeDelta = Vector2.zero;
        }

        _questPanel.SetActive(false);

        // Left panel — scrollable pill list (36% width)
        var leftPanel = new GameObject("LeftPanel");
        leftPanel.transform.SetParent(_questPanel.transform, false);
        leftPanel.AddComponent<Image>().color = new Color(0.04f, 0.048f, 0.062f, 0.88f);
        var lpr = EnsureRect(leftPanel);
        lpr.anchorMin = new Vector2(0f, 0f);
        lpr.anchorMax = new Vector2(0.36f, 1f);
        lpr.sizeDelta = Vector2.zero;

        // Vertical separator
        var sep = new GameObject("Separator");
        sep.transform.SetParent(_questPanel.transform, false);
        sep.AddComponent<Image>().color = new Color(ColorPurple.r, ColorPurple.g, ColorPurple.b, 0.30f);
        var sepr = EnsureRect(sep);
        sepr.anchorMin = new Vector2(0.36f, 0.02f);
        sepr.anchorMax = new Vector2(0.362f, 0.98f);
        sepr.sizeDelta = Vector2.zero;

        // Right panel — detail view
        _questDetailPanel = new GameObject("DetailPanel");
        _questDetailPanel.transform.SetParent(_questPanel.transform, false);
        _questDetailPanel.AddComponent<Image>().color = new Color(0.04f, 0.048f, 0.062f, 0.75f);
        var dpr = EnsureRect(_questDetailPanel);
        dpr.anchorMin = new Vector2(0.363f, 0f);
        dpr.anchorMax = new Vector2(1f, 1f);
        dpr.sizeDelta = Vector2.zero;

        // ScrollRect inside left panel
        var scrollGo = new GameObject("QuestScroll");
        scrollGo.transform.SetParent(leftPanel.transform, false);
        var srRect = scrollGo.AddComponent<RectTransform>();
        srRect.anchorMin = Vector2.zero;
        srRect.anchorMax = Vector2.one;
        srRect.sizeDelta = Vector2.zero;

        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollGo.transform, false);
        var vpRect = viewport.AddComponent<RectTransform>();
        vpRect.anchorMin = Vector2.zero;
        vpRect.anchorMax = Vector2.one;
        vpRect.sizeDelta = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        scrollRect.viewport = vpRect;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        _questContent = content.AddComponent<RectTransform>();
        _questContent.anchorMin = new Vector2(0f, 1f);
        _questContent.anchorMax = new Vector2(1f, 1f);
        _questContent.pivot = new Vector2(0.5f, 1f);
        _questContent.sizeDelta = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.spacing = 2f;
        vlg.padding = new RectOffset(4, 4, 6, 6);

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = _questContent;

        RefreshQuestPanel();
    }

    private void RefreshQuestPanel()
    {
        if (_questContent == null) return;
        for (int i = _questContent.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_questContent.GetChild(i).gameObject);

        var quests = MogulQuestSystem.GetAvailable(MogulObjectiveType.Quest, MogulNetwork.Data);
        var tasks = MogulQuestSystem.GetAvailable(MogulObjectiveType.Task, MogulNetwork.Data);
        if (quests.Count > 0 || tasks.Count > 0)
        {
            BuildQuestSectionHeader("QUESTS");
            foreach (var q in quests)
                BuildQuestPill(q);
            foreach (var t in tasks)
                BuildQuestPill(t);
        }

        RefreshQuestDetail();
    }

    private void BuildQuestSectionHeader(string label)
    {
        var row = new GameObject("Header_" + label);
        row.transform.SetParent(_questContent, false);
        var le = row.AddComponent<LayoutElement>();
        le.preferredHeight = 20f;
        le.minHeight = 20f;

        var textObj = MakeText(row, "Label", label);
        var text = textObj.GetComponent<Text>();
        text.fontSize = 9;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(ColorPurple.r, ColorPurple.g, ColorPurple.b, 0.85f);
        text.alignment = TextAnchor.MiddleLeft;
        var tr = EnsureRect(textObj);
        tr.anchorMin = new Vector2(0.05f, 0f);
        tr.anchorMax = new Vector2(0.98f, 1f);
        tr.sizeDelta = Vector2.zero;
    }

    private void BuildQuestPill(MogulQuestDefinition quest)
    {
        var data     = MogulNetwork.Data;
        bool active   = data.ActiveQuestId == quest.Id;
        bool complete = MogulQuestSystem.IsComplete(quest, data);
        bool claimed  = MogulQuestSystem.IsClaimed(quest, data);
        bool accepted = MogulQuestSystem.IsAccepted(quest, data);
        bool selected = _selectedQuestId == quest.Id;

        var pill = new GameObject("Pill_" + quest.Id);
        pill.transform.SetParent(_questContent, false);
        var le = pill.AddComponent<LayoutElement>();
        le.preferredHeight = 30f;
        le.minHeight = 30f;

        pill.AddComponent<Image>().color = selected
            ? new Color(ColorPurple.r * 0.38f, ColorPurple.g * 0.38f, ColorPurple.b * 0.38f, 1f)
            : new Color(0.08f, 0.095f, 0.12f, 0.90f);

        // Left status strip (3px)
        var strip = new GameObject("Strip");
        strip.transform.SetParent(pill.transform, false);
        Color stripColor = claimed   ? ColorMuted
                         : complete  ? ColorGold
                         : active    ? ColorPurple
                         : accepted  ? new Color(ColorPurple.r * 0.55f, ColorPurple.g * 0.55f, ColorPurple.b * 0.55f, 1f)
                         : new Color(0.28f, 0.30f, 0.34f, 1f);
        strip.AddComponent<Image>().color = stripColor;
        var sr = EnsureRect(strip);
        sr.anchorMin = new Vector2(0f, 0.12f);
        sr.anchorMax = new Vector2(0f, 0.88f);
        sr.pivot = new Vector2(0f, 0.5f);
        sr.sizeDelta = new Vector2(3f, 0f);

        var nameObj = MakeText(pill, "Name", quest.Title);
        var nameText = nameObj.GetComponent<Text>();
        nameText.fontSize = 11;
        nameText.fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
        nameText.color = claimed ? ColorMuted : complete ? ColorGold : Color.white;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var nr = EnsureRect(nameObj);
        nr.anchorMin = new Vector2(0.07f, 0f);
        nr.anchorMax = new Vector2(0.97f, 1f);
        nr.sizeDelta = Vector2.zero;

        var btn = pill.AddComponent<Button>();
        var btnColors = btn.colors;
        btnColors.normalColor = Color.white;
        btnColors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
        btnColors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
        btnColors.fadeDuration = 0.08f;
        btn.colors = btnColors;

        string questId = quest.Id;
        btn.onClick.AddListener(new Action(() =>
        {
            _selectedQuestId = questId;
            RefreshQuestPanel();
        }));
    }

    private void RefreshQuestDetail()
    {
        if (_questDetailPanel == null) return;

        for (int i = _questDetailPanel.transform.childCount - 1; i >= 0; i--)
            GameObject.Destroy(_questDetailPanel.transform.GetChild(i).gameObject);

        if (string.IsNullOrEmpty(_selectedQuestId))
        {
            var hint = MakeText(_questDetailPanel, "Hint", "Select a quest to view details");
            var hintText = hint.GetComponent<Text>();
            hintText.fontSize = 12;
            hintText.color = new Color(ColorMuted.r, ColorMuted.g, ColorMuted.b, 0.5f);
            hintText.alignment = TextAnchor.MiddleCenter;
            var hr = EnsureRect(hint);
            hr.anchorMin = new Vector2(0.05f, 0.4f);
            hr.anchorMax = new Vector2(0.95f, 0.6f);
            hr.sizeDelta = Vector2.zero;
            return;
        }

        var quest = MogulQuestSystem.Find(_selectedQuestId);
        if (quest == null)
        {
            _selectedQuestId = null;
            RefreshQuestDetail();
            return;
        }

        var data     = MogulNetwork.Data;
        int progress = MogulQuestSystem.GetProgress(quest, data);
        bool active   = data.ActiveQuestId == quest.Id;
        bool complete = MogulQuestSystem.IsComplete(quest, data);
        bool claimed  = MogulQuestSystem.IsClaimed(quest, data);
        bool accepted = MogulQuestSystem.IsAccepted(quest, data);

        Color accent = claimed ? ColorMuted : complete ? ColorGold : ColorPurple;

        // Accent strip along top edge
        var topStrip = new GameObject("TopStrip");
        topStrip.transform.SetParent(_questDetailPanel.transform, false);
        topStrip.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.65f);
        var tsr = EnsureRect(topStrip);
        tsr.anchorMin = new Vector2(0f, 1f);
        tsr.anchorMax = new Vector2(1f, 1f);
        tsr.pivot = new Vector2(0.5f, 1f);
        tsr.sizeDelta = new Vector2(0f, 3f);

        // Title
        var titleObj = MakeText(_questDetailPanel, "Title", quest.Title.ToUpper());
        var titleText = titleObj.GetComponent<Text>();
        titleText.fontSize = 15;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = accent;
        titleText.alignment = TextAnchor.UpperLeft;
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        var titleR = EnsureRect(titleObj);
        titleR.anchorMin = new Vector2(0.05f, 0.84f);
        titleR.anchorMax = new Vector2(0.95f, 0.97f);
        titleR.sizeDelta = Vector2.zero;

        // Divider below title
        var div = new GameObject("Divider");
        div.transform.SetParent(_questDetailPanel.transform, false);
        div.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.30f);
        var divr = EnsureRect(div);
        divr.anchorMin = new Vector2(0.05f, 0.825f);
        divr.anchorMax = new Vector2(0.95f, 0.831f);
        divr.sizeDelta = Vector2.zero;

        // DESCRIPTION
        BuildDetailLabel("DESCRIPTION", new Vector2(0.05f, 0.76f), new Vector2(0.95f, 0.82f));

        var descObj = MakeText(_questDetailPanel, "Description", quest.Description);
        var descText = descObj.GetComponent<Text>();
        descText.fontSize = 11;
        descText.color = new Color(0.84f, 0.84f, 0.84f, 1f);
        descText.alignment = TextAnchor.UpperLeft;
        descText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descText.verticalOverflow = VerticalWrapMode.Truncate;
        var descR = EnsureRect(descObj);
        descR.anchorMin = new Vector2(0.05f, 0.60f);
        descR.anchorMax = new Vector2(0.95f, 0.76f);
        descR.sizeDelta = Vector2.zero;

        // OBJECTIVE
        BuildDetailLabel("OBJECTIVE", new Vector2(0.05f, 0.52f), new Vector2(0.95f, 0.58f));

        string progressStr = $"{quest.Objective}: {Math.Min(progress, quest.Target)} / {quest.Target}";
        var progObj = MakeText(_questDetailPanel, "Progress", progressStr);
        var progText = progObj.GetComponent<Text>();
        progText.fontSize = 12;
        progText.color = complete ? ColorGold : Color.white;
        progText.alignment = TextAnchor.UpperLeft;
        var progR = EnsureRect(progObj);
        progR.anchorMin = new Vector2(0.05f, 0.44f);
        progR.anchorMax = new Vector2(0.95f, 0.52f);
        progR.sizeDelta = Vector2.zero;

        // REWARDS
        BuildDetailLabel("REWARDS", new Vector2(0.05f, 0.36f), new Vector2(0.95f, 0.42f));

        var rewObj = MakeText(_questDetailPanel, "Reward", $"+{quest.ReachReward} Reach");
        var rewText = rewObj.GetComponent<Text>();
        rewText.fontSize = 14;
        rewText.fontStyle = FontStyle.Bold;
        rewText.color = ColorGold;
        rewText.alignment = TextAnchor.UpperLeft;
        var rewR = EnsureRect(rewObj);
        rewR.anchorMin = new Vector2(0.05f, 0.28f);
        rewR.anchorMax = new Vector2(0.95f, 0.36f);
        rewR.sizeDelta = Vector2.zero;

        // Action button
        string btnLabel = claimed   ? "CLAIMED"
                        : complete  ? "CLAIM REWARD"
                        : active    ? "UNTRACK"
                        : accepted  ? "TRACK QUEST"
                        : "ACCEPT";
        Color btnBg = claimed   ? ColorRowOwned
                    : complete  ? ColorGold
                    : active    ? ColorMuted
                    : accepted  ? ColorRow
                    : ColorPurple;
        Color btnFg = claimed  ? ColorMuted
                    : complete ? ColorDark
                    : Color.white;

        BuildButton(_questDetailPanel, "QuestAction", btnLabel,
            new Vector2(0.05f, 0.05f), new Vector2(0.60f, 0.16f),
            btnBg, btnFg,
            () =>
            {
                if (claimed) return;
                if (complete)
                    MogulQuestSystem.RequestClaim(quest.Id);
                else if (!accepted)
                    MogulQuestSystem.RequestAccept(quest.Id);
                else if (active)
                    MogulQuestSystem.RequestUntrack();
                else
                    MogulQuestSystem.RequestTrack(quest.Id);
                RefreshQuestPanel();
            });
    }

    private void BuildDetailLabel(string text, Vector2 ancMin, Vector2 ancMax)
    {
        var obj = MakeText(_questDetailPanel, "Lbl_" + text, text);
        var t = obj.GetComponent<Text>();
        t.fontSize = 9;
        t.fontStyle = FontStyle.Bold;
        t.color = ColorMuted;
        t.alignment = TextAnchor.UpperLeft;
        var r = EnsureRect(obj);
        r.anchorMin = ancMin;
        r.anchorMax = ancMax;
        r.sizeDelta = Vector2.zero;
    }
}
