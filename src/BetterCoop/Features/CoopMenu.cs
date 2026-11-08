using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Ui;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zorro.ControllerSupport;
using Zorro.UI;
using Object = UnityEngine.Object;

namespace BetterCoop.Features;

/// <summary>
/// The Coop Menu: one "Coop" button in the pause menu opening a page built
/// from the game's own Controls page and menu buttons, so it looks and
/// navigates like the game's menus.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class CoopMenu
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PauseMenuMainPage), "Start")]
    private static void AddMenu(PauseMenuMainPage __instance)
    {
        UIPageHandler handler = __instance.GetComponentInParent<UIPageHandler>(true);
        PauseMenuControlsPage? controls = handler != null ? handler.GetComponentInChildren<PauseMenuControlsPage>(true) : null;
        if (handler == null || controls == null)
        {
            Plugin.Log.LogWarning("The pause menu has no Controls page to build the Coop Menu from.");
            return;
        }

        CoopMenuPage page = CoopMenuPage.Build(handler, controls, __instance.m_settingsButton);
        AddButton(__instance, () => handler.TransistionToPage(page, new SetActivePageTransistion()));
    }

    /// <summary>A copy of a pause menu button with its own label and action, its text no longer localized by the game.</summary>
    internal static Button CloneButton(Button template, Transform parent, string name, Action onClick)
    {
        GameObject clone = Object.Instantiate(template.gameObject, parent);
        clone.name = name;
        foreach (LocalizedText text in clone.GetComponentsInChildren<LocalizedText>(true))
        {
            text.autoSet = false;
            text.enabled = false;
        }

        Button button = clone.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(() =>
        {
            try
            {
                onClick();
            }
            catch (Exception exception)
            {
                Guard.Report(name, exception);
            }

            // A mouse click would leave the button looking selected; a gamepad
            // keeps its selection to navigate.
            if (InputHandler.GetCurrentUsedInputScheme() != InputScheme.Gamepad && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        });
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        return button;
    }

    /// <summary>Adds the Coop button to the pause menu, after Resume.</summary>
    private static void AddButton(PauseMenuMainPage menu, Action open)
    {
        Button template = menu.m_settingsButton;
        Transform parent = template.transform.parent;
        Button button = CloneButton(template, parent, "BetterCoop.CoopMenuButton", open);
        if (button.GetComponentInChildren<TMP_Text>(true) is TMP_Text label)
        {
            label.text = Strings.CoopMenu;
        }

        GameObject clone = button.gameObject;
        if (parent.GetComponent<LayoutGroup>() != null)
        {
            clone.transform.SetSiblingIndex(menu.m_resumeButton.transform.GetSiblingIndex() + 1);
            Plugin.Verbose("Added the Coop Menu to the pause menu's layout.");
            return;
        }

        // Without a layout group, sit one step below the lowest menu button.
        var buttons = new[]
            {
                menu.m_resumeButton, menu.m_settingsButton, menu.m_accoladesButton,
                menu.m_controllsButton, menu.m_inviteButton, menu.m_quitButton,
            }
            .Where(b => b != null && b.transform.parent == parent)
            .Select(b => (RectTransform)b.transform)
            .OrderByDescending(r => r.anchoredPosition.y)
            .ToList();
        float step = buttons.Count > 1 ? buttons[0].anchoredPosition.y - buttons[1].anchoredPosition.y : 60f;
        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition = buttons.Count > 0
            ? buttons[^1].anchoredPosition - new Vector2(0, step)
            : rect.anchoredPosition - new Vector2(0, step);
        Plugin.Verbose($"Added the Coop Menu to the pause menu {step:F0} below its last button.");
    }
}

/// <summary>
/// The Coop Menu page. Its rows are rebuilt when the room's Pairs or the
/// Troop Leader change, and their texts refresh every frame while it is open.
/// </summary>
internal sealed class CoopMenuPage : UIPage, IHaveParentPage, INavigationPage
{
    private readonly List<MenuRow> rows = new();

    private UIPageHandler handler = null!;

    private Transform list = null!;

    private Button back = null!;

    private Button buttonTemplate = null!;

    private TMP_Text textTemplate = null!;

    private string shape = string.Empty;

    /// <summary>
    /// Copies the Controls page while it cannot wake: under an inactive
    /// holder, so the copy's Controls page never runs or registers, and is
    /// removed before it could.
    /// </summary>
    internal static CoopMenuPage Build(UIPageHandler handler, PauseMenuControlsPage original, Button buttonTemplate)
    {
        var holder = new GameObject("BetterCoop.MenuHolder");
        holder.SetActive(false);
        GameObject clone = Object.Instantiate(original.gameObject, holder.transform);
        clone.name = "BetterCoop.CoopMenu";

        PauseMenuControlsPage copy = clone.GetComponent<PauseMenuControlsPage>();
        Transform list = copy.controlsMenuButtonsParent;
        Button back = copy.backButton;
        GameObject? reset = copy.restoreAllButton != null ? copy.restoreAllButton.gameObject : null;
        GameObject[] schemeOnly = (copy.keyboardOnlyObjects ?? []).Concat(copy.controllerOnlyObjects ?? []).ToArray();
        Object.DestroyImmediate(copy);

        foreach (PauseMenuRebindButton rebind in clone.GetComponentsInChildren<PauseMenuRebindButton>(true))
        {
            Object.DestroyImmediate(rebind.gameObject);
        }

        foreach (Transform child in list.Cast<Transform>().ToList())
        {
            Object.DestroyImmediate(child.gameObject);
        }

        if (reset != null)
        {
            reset.SetActive(false);
        }

        // Key and gamepad hints belong to the Controls page; keep anything holding the list or Back.
        foreach (GameObject hint in schemeOnly)
        {
            if (hint != null && !list.IsChildOf(hint.transform) && !back.transform.IsChildOf(hint.transform))
            {
                hint.SetActive(false);
            }
        }

        if (list.GetComponent<LayoutGroup>() == null)
        {
            VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 8f;
        }

        TMP_Text? title = clone.GetComponentsInChildren<TMP_Text>(true)
            .Where(t => !t.transform.IsChildOf(list) && !t.transform.IsChildOf(back.transform))
            .OrderByDescending(t => t.fontSize)
            .FirstOrDefault();
        if (title != null)
        {
            foreach (LocalizedText text in title.GetComponents<LocalizedText>())
            {
                text.autoSet = false;
                text.enabled = false;
            }

            title.text = Strings.CoopMenu;
        }

        clone.transform.SetParent(original.transform.parent, false);
        clone.SetActive(false);
        clone.transform.SetSiblingIndex(original.transform.GetSiblingIndex() + 1);
        Object.Destroy(holder);

        CoopMenuPage page = clone.AddComponent<CoopMenuPage>();
        page.handler = handler;
        page.list = list;
        page.back = back;
        page.buttonTemplate = buttonTemplate;
        page.textTemplate = buttonTemplate.GetComponentInChildren<TMP_Text>(true);
        back.onClick = new Button.ButtonClickedEvent();
        back.onClick.AddListener(page.GoBack);
        Plugin.Verbose(
            "Built the Coop Menu: other texts "
            + string.Join(", ", clone.GetComponentsInChildren<TMP_Text>(true).Where(t => t != title).Select(t => t.name)));
        return page;
    }

    public (UIPage, PageTransistion) GetParentPage() =>
        (handler.GetPage<PauseMenuMainPage>(), new SetActivePageTransistion());

    public GameObject GetFirstSelectedGameObject() =>
        rows.Select(r => r.Button).FirstOrDefault(b => b != null && b.gameObject.activeInHierarchy)?.gameObject
        ?? back.gameObject;

    private void GoBack()
    {
        try
        {
            handler.TransistionToPage(handler.GetPage<PauseMenuMainPage>(), new SetActivePageTransistion());
        }
        catch (Exception exception)
        {
            Guard.Report("CoopMenuPage.GoBack", exception);
        }
    }

    private void LateUpdate()
    {
        try
        {
            string now = MenuShape.Of();
            if (now != shape)
            {
                shape = now;
                Rebuild();
            }

            foreach (MenuRow row in rows)
            {
                row.Refresh();
            }
        }
        catch (Exception exception)
        {
            Guard.Report("CoopMenuPage.LateUpdate", exception);
        }
    }

    private void Rebuild()
    {
        foreach (MenuRow row in rows)
        {
            Destroy(row.Root);
        }

        rows.Clear();
        PlayerRows.Add(this);
        if (PhotonNetwork.IsMasterClient)
        {
            TroopLeaderRows.Add(this);
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)list);
    }

    /// <summary>A heading over the rows below it.</summary>
    internal void AddHeader(Func<string> text, Func<bool>? visible = null) =>
        Add(new MenuRow(NewRow(), text, null, null, visible, null, header: true, textTemplate));

    /// <summary>A label with a value on the right.</summary>
    internal void AddInfo(Func<string> label, Func<string>? value = null, Func<bool>? visible = null)
    {
        GameObject root = NewRow();
        TMP_Text? valueText = value == null ? null : NewText(root.transform, "Value", TextAlignmentOptions.Right, flexible: false);
        Add(new MenuRow(root, label, value, null, visible, null, header: false, textTemplate, valueText));
    }

    /// <summary>A label with a button on the right; a button not available dims but still answers, to say why.</summary>
    internal void AddAction(Func<string> label, Func<string> button, Action onClick, Func<bool>? visible = null, Func<bool>? available = null)
    {
        GameObject root = NewRow();
        Button clone = CoopMenu.CloneButton(buttonTemplate, root.transform, "Action", onClick);
        var rect = (RectTransform)buttonTemplate.transform;
        LayoutElement? existing = clone.GetComponent<LayoutElement>();
        LayoutElement size = existing != null ? existing : clone.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = Mathf.Max(220f, rect.rect.width * 0.7f);
        size.preferredHeight = RowHeight;
        size.flexibleWidth = 0f;
        Add(new MenuRow(root, label, button, clone, visible, available, header: false, textTemplate));
    }

    private float RowHeight => Mathf.Max(44f, textTemplate.fontSize * 1.5f);

    private void Add(MenuRow row)
    {
        rows.Add(row);
        row.Refresh();
    }

    private GameObject NewRow()
    {
        var root = new GameObject("BetterCoop.Row", typeof(RectTransform));
        root.transform.SetParent(list, false);
        HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 16f;
        LayoutElement size = root.AddComponent<LayoutElement>();
        size.minHeight = RowHeight;
        size.preferredHeight = RowHeight;
        NewText(root.transform, "Label", TextAlignmentOptions.Left, flexible: true);
        return root;
    }

    private TMP_Text NewText(Transform parent, string name, TextAlignmentOptions alignment, bool flexible)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        TextMeshProUGUI text = holder.AddComponent<TextMeshProUGUI>();
        text.font = textTemplate.font;
        text.fontSharedMaterial = textTemplate.fontSharedMaterial;
        text.fontSize = textTemplate.fontSize * 0.8f;
        text.color = textTemplate.color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        LayoutElement size = holder.AddComponent<LayoutElement>();
        size.flexibleWidth = flexible ? 1f : 0f;
        size.minWidth = flexible ? 0f : 120f;
        return text;
    }
}

/// <summary>One row of the Coop Menu: a label, and a value or a button.</summary>
internal sealed class MenuRow
{
    private const float UnavailableAlpha = 0.5f;

    private readonly Func<string> label;

    private readonly Func<string>? right;

    private readonly Func<bool>? visible;

    private readonly Func<bool>? available;

    private readonly TMP_Text labelText;

    private readonly TMP_Text? rightText;

    private readonly Color rightColor;

    public MenuRow(
        GameObject root,
        Func<string> label,
        Func<string>? right,
        Button? button,
        Func<bool>? visible,
        Func<bool>? available,
        bool header,
        TMP_Text template,
        TMP_Text? valueText = null)
    {
        Root = root;
        Button = button;
        this.label = label;
        this.right = right;
        this.visible = visible;
        this.available = available;
        labelText = root.transform.Find("Label").GetComponent<TMP_Text>();
        if (header)
        {
            labelText.fontStyle = FontStyles.Bold;
            labelText.fontSize = template.fontSize * 0.9f;
        }

        rightText = button != null ? button.GetComponentInChildren<TMP_Text>(true) : valueText;
        rightColor = rightText != null ? rightText.color : Color.white;
    }

    public GameObject Root { get; }

    public Button? Button { get; }

    /// <summary>Runs after the button's own animation each frame, so the label's colour is the row's.</summary>
    public void Refresh()
    {
        bool shown = visible?.Invoke() ?? true;
        if (Root.activeSelf != shown)
        {
            Root.SetActive(shown);
        }

        if (!shown)
        {
            return;
        }

        Set(labelText, label());
        if (rightText != null && right != null)
        {
            Set(rightText, right());
            Color color = rightColor;
            if (available != null && !available())
            {
                color.a *= UnavailableAlpha;
            }

            if (rightText.color != color)
            {
                rightText.color = color;
            }
        }
    }

    private static void Set(TMP_Text text, string value)
    {
        if (text.text != value)
        {
            text.text = value;
        }
    }
}

/// <summary>What decides the Coop Menu's rows: the room, the Pairs, and who leads.</summary>
internal static class MenuShape
{
    public static string Of()
    {
        if (!PhotonNetwork.InRoom)
        {
            return "out";
        }

        string pairs = string.Join(",", PairRoster.Pairs.Select(p => $"{p.Carrier}>{p.Rider}"));
        string players = string.Join(",", PhotonNetwork.PlayerList.Select(p => p.ActorNumber));
        return $"{PhotonNetwork.IsMasterClient}|{PairRoster.Formed}|{pairs}|{players}";
    }
}

/// <summary>Every player's rows: their own Pair and Role Preference, and the Pairs of the room.</summary>
internal static class PlayerRows
{
    public static void Add(CoopMenuPage page)
    {
        page.AddAction(
            () => Strings.MenuRolePreference,
            () => Strings.RoleName(CoopConfig.RolePreference.Value),
            CycleRolePreference);
        page.AddAction(
            PartnerLine,
            () => RoleSwap.Pending ? Strings.CancelSwap : Strings.SwapPlaces,
            RoleSwap.Toggle,
            visible: () => PairRoster.TryGetPair(Character.localCharacter, out _),
            available: () => RoleSwap.Pending || RoleSwap.CanRequest(out _));
        page.AddInfo(() => Strings.MenuNoPartner, visible: () => PairRoster.Formed && !PairRoster.TryGetPair(Character.localCharacter, out _));
        page.AddInfo(() => Strings.MenuNextShuffle, NextShuffle, visible: () => PairRoster.Formed);
        page.AddInfo(() => Strings.MenuPairsForm, visible: () => !PairRoster.Formed);

        if (!PairRoster.Formed)
        {
            return;
        }

        page.AddHeader(() => Strings.MenuPairs);
        foreach (Pair pair in PairRoster.Pairs)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // The Troop Leader turns any Pair round without waiting for both to ask.
                page.AddAction(
                    () => Strings.MenuPair(NameOf(pair.Carrier), NameOf(pair.Rider)),
                    () => Strings.SwapPlaces,
                    () => TroopLeaderRows.Order(PairShuffle.Request(OperationKind.RoleSwap, pair.Carrier, pair.Rider)),
                    available: () => !PairShuffle.Busy);
            }
            else
            {
                page.AddInfo(() => Strings.MenuPair(NameOf(pair.Carrier), NameOf(pair.Rider)));
            }
        }

        page.AddInfo(
            () => Strings.MenuUnpaired,
            () => string.Join(", ", UnpairedActors().Select(NameOf)),
            visible: () => UnpairedActors().Any());
    }

    internal static IEnumerable<int> UnpairedActors() =>
        PhotonNetwork.PlayerList
            .Select(p => p.ActorNumber)
            .Where(a => !PairRoster.Pairs.Any(p => p.Contains(a)));

    internal static string NameOf(int actor) =>
        PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.NickName ?? $"#{actor}";

    private static string PartnerLine()
    {
        Character local = Character.localCharacter;
        if (!PairRoster.TryGetPair(local, out Pair pair))
        {
            return string.Empty;
        }

        int me = PairRoster.ActorOf(local);
        return Strings.MenuPartner(NameOf(pair.PartnerOf(me)), PairRoster.RoleOf(local) == PairRole.Carrier);
    }

    private static string NextShuffle()
    {
        if (PairShuffle.Busy)
        {
            return Strings.MenuShuffleNow;
        }

        return PairShuffle.SecondsToTimedShuffle() is double seconds
            ? Strings.Clock(seconds)
            : Strings.MenuShuffleOff;
    }

    private static void CycleRolePreference()
    {
        CoopConfig.RolePreference.Value = CoopConfig.RolePreference.Value switch
        {
            RolePreference.Random => RolePreference.Carrier,
            RolePreference.Carrier => RolePreference.Rider,
            _ => RolePreference.Random,
        };
    }
}

/// <summary>The Troop Leader's rows: Pair changes and the Run Rules.</summary>
internal static class TroopLeaderRows
{
    /// <summary>Timed Shuffle intervals the menu steps through, in minutes.</summary>
    private static readonly int[] Intervals = [1, 2, 3, 5, 10, 15, 20, 30, 45, 60];

    public static void Add(CoopMenuPage page)
    {
        page.AddHeader(() => Strings.MenuTroopLeader);
        page.AddAction(
            () => Strings.MenuShuffleNowLabel,
            () => Strings.MenuGo,
            () => Order(PairShuffle.Request(OperationKind.OrderedShuffle)),
            visible: () => PairRoster.Formed,
            available: () => !PairShuffle.Busy);
        page.AddAction(
            () => Strings.MenuRecover,
            () => Strings.MenuGo,
            CarryKeeper.RequestRecovery,
            visible: () => PairRoster.Formed);

        page.AddHeader(() => Strings.MenuRunRules);
        Toggle(page, () => Strings.RuleCampfireShuffle, CoopConfig.CampfireShuffle);
        Toggle(page, () => Strings.RuleTimedShuffle, CoopConfig.TimedShuffle);
        page.AddAction(
            () => Strings.RuleTimedShuffleInterval,
            () => Strings.Minutes(ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value)),
            NextInterval);
        Toggle(page, () => Strings.RuleHandOverPlace, CoopConfig.HandOverPlace);
        Toggle(page, () => Strings.RuleHandOverItems, CoopConfig.HandOverItems);
        Toggle(page, () => Strings.RuleCarryWeight, CoopConfig.CarryWeight);
        Toggle(page, () => Strings.RuleRiderPicksFromBackpack, CoopConfig.RiderPicksFromBackpack);
        Toggle(page, () => Strings.RuleAllowOddPlayerCount, CoopConfig.AllowOddPlayerCount);
    }

    /// <summary>Says why a Pair change the Troop Leader ordered did not start.</summary>
    internal static void Order(bool started)
    {
        if (!started)
        {
            CoopNotice.Info(Strings.MenuBusy);
        }
    }

    /// <summary>
    /// A Run Rule as a switch. It writes the Troop Leader's own setting, which
    /// is saved and published to the room as any change in the config editor is.
    /// </summary>
    private static void Toggle(CoopMenuPage page, Func<string> label, BepInEx.Configuration.ConfigEntry<bool> rule) =>
        page.AddAction(label, () => rule.Value ? Strings.On : Strings.Off, () => rule.Value = !rule.Value);

    private static void NextInterval()
    {
        int now = ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value);
        int next = Intervals.FirstOrDefault(m => m > now);
        CoopConfig.TimedShuffleInterval.Value = next == 0 ? Intervals[0] : next;
    }
}
