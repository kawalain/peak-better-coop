using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Ui;
using BetterCoop.Ui.Paper;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zorro.UI;

namespace BetterCoop.Features;

/// <summary>
/// The Coop Menu: one "Coop" button in the pause menu opening a paper page
/// that keeps the game's own Back button and menu button style.
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

        Button template = __instance.m_settingsButton;
        CoopMenuPage page = PaperPage.Create<CoopMenuPage>(handler, controls, template, __instance, () => Strings.CoopMenu);
        AddButton(__instance, Strings.CoopMenu, page.Open, after: __instance.m_resumeButton);
    }

    /// <summary>Adds a button to the pause menu, after <paramref name="after"/>.</summary>
    private static Button AddButton(PauseMenuMainPage menu, string label, Action open, Button after)
    {
        Button template = menu.m_settingsButton;
        Transform parent = template.transform.parent;
        Button button = PaperList.CloneButton(template, parent, "BetterCoop.MenuButton." + label, open);
        if (button.GetComponentInChildren<TMP_Text>(true) is TMP_Text text)
        {
            text.text = label;
        }

        GameObject clone = button.gameObject;
        if (parent.GetComponent<LayoutGroup>() != null)
        {
            clone.transform.SetSiblingIndex(after.transform.GetSiblingIndex() + 1);
            Plugin.Verbose($"Added {label} to the pause menu's layout.");
            return button;
        }

        // Without a layout group, sit one step below the lowest menu button.
        var buttons = new[]
            {
                menu.m_resumeButton, menu.m_settingsButton, menu.m_accoladesButton,
                menu.m_controllsButton, menu.m_inviteButton, menu.m_quitButton, after,
            }
            .Where(b => b != null && b != button && b.transform.parent == parent)
            .Select(b => (RectTransform)b.transform)
            .Distinct()
            .OrderByDescending(r => r.anchoredPosition.y)
            .ToList();
        float step = buttons.Count > 1 ? buttons[0].anchoredPosition.y - buttons[1].anchoredPosition.y : 60f;
        var rect = (RectTransform)clone.transform;
        rect.anchoredPosition = buttons.Count > 0
            ? buttons[^1].anchoredPosition - new Vector2(0, step)
            : rect.anchoredPosition - new Vector2(0, step);
        Plugin.Verbose($"Added {label} to the pause menu {step:F0} below its last button.");
        return button;
    }
}

/// <summary>
/// The Coop Menu page: the room's Pairs and the local player's own Pair and
/// Role Preference, and for the Troop Leader the Pair changes and the Run
/// Rules. It shows whatever room it is given: the game's own, or a made-up
/// one in the Zoo.
/// </summary>
internal sealed class CoopMenuPage : PaperPage
{
    private ICoopRoom room = new LiveRoom();

    /// <summary>The players picked for a Pair Assignment.</summary>
    private AssignPicks picks = new();

    /// <summary>Shows another room, its rows filled anew at once.</summary>
    public void Show(ICoopRoom shown)
    {
        room = shown;
        picks = new AssignPicks();
        Refill();
    }

    protected override string Shape() => room.Shape();

    protected override void Fill(PaperList list)
    {
        PlayerRows.Add(list, room);
        if (room.InRoom && room.IsTroopLeader)
        {
            TroopLeaderRows.Add(list, room, picks);
        }
    }

    protected override void Tick() => room.Tick(Time.unscaledDeltaTime);
}

/// <summary>The players the Troop Leader has picked for a Pair Assignment, by actor; -1 for none yet.</summary>
internal sealed class AssignPicks
{
    public int Carrier { get; set; } = -1;

    public int Rider { get; set; } = -1;
}

/// <summary>Every player's rows: their own Pair and Role Preference, and the Pairs of the room.</summary>
internal static class PlayerRows
{
    private static readonly RolePreference[] Preferences = [RolePreference.Random, RolePreference.Carrier, RolePreference.Rider];

    public static void Add(PaperList list, ICoopRoom room)
    {
        list.Cycle(
            () => Strings.MenuRolePreference,
            () => room.Preference,
            value => room.Preference = value,
            () => Preferences,
            Strings.RoleName,
            about: () => Strings.AboutRolePreference);
        list.Action(
            () => PartnerLine(room),
            () => room.SwapPending ? Strings.CancelSwap : Strings.SwapPlaces,
            room.ToggleSwap,
            visible: () => room.PairOf(room.Me) != null,
            available: () => room.SwapPending || room.CanAskSwap,
            about: () => Strings.AboutSwapPlaces);
        list.Info(
            () => Strings.MenuNoPartner,
            visible: () => room.Formed && room.PairOf(room.Me) == null,
            about: () => Strings.AboutNoPartner);
        list.Info(
            () => Strings.MenuNextShuffle,
            () => NextShuffle(room),
            visible: () => room.Formed,
            about: () => Strings.AboutNextShuffle);
        list.Info(() => Strings.MenuPairsForm, visible: () => !room.Formed);

        if (!room.Formed)
        {
            return;
        }

        list.Header(() => Strings.MenuPairs);
        foreach (Pair pair in room.Pairs)
        {
            if (room.IsTroopLeader)
            {
                // The Troop Leader turns any Pair round without waiting for both to ask.
                list.Action(
                    () => Strings.MenuPair(room.NameOf(pair.Carrier), room.NameOf(pair.Rider)),
                    () => Strings.SwapPlaces,
                    () => TroopLeaderRows.Order(room.SwapRoles(pair)),
                    available: () => !room.Busy);
            }
            else
            {
                list.Info(() => Strings.MenuPair(room.NameOf(pair.Carrier), room.NameOf(pair.Rider)));
            }
        }

        list.Info(
            () => Strings.MenuUnpaired,
            () => string.Join(", ", room.Unpaired().Select(room.NameOf)),
            visible: () => room.Unpaired().Any());
    }

    private static string PartnerLine(ICoopRoom room)
    {
        if (room.PairOf(room.Me) is not Pair pair)
        {
            return string.Empty;
        }

        return Strings.MenuPartner(room.NameOf(pair.PartnerOf(room.Me)), pair.Carrier == room.Me);
    }

    private static string NextShuffle(ICoopRoom room)
    {
        if (room.Busy)
        {
            return Strings.MenuShuffleNow;
        }

        return room.SecondsToTimedShuffle is double seconds
            ? Strings.Clock(seconds)
            : Strings.MenuShuffleOff;
    }
}

/// <summary>The Troop Leader's rows: Pair changes and the Run Rules.</summary>
internal static class TroopLeaderRows
{
    /// <summary>Timed Shuffle intervals the menu steps through, in minutes.</summary>
    private static readonly int[] Intervals = [1, 2, 3, 5, 10, 15, 20, 30, 45, 60];

    public static void Add(PaperList list, ICoopRoom room, AssignPicks picks)
    {
        list.Header(() => Strings.MenuTroopLeader);
        list.Action(
            () => Strings.MenuShuffleNowLabel,
            () => Strings.MenuGo,
            () => Order(room.Shuffle()),
            visible: () => room.Formed,
            available: () => !room.Busy,
            about: () => Strings.AboutShuffleNow);
        list.Action(
            () => Strings.MenuRecover,
            () => Strings.MenuGo,
            room.Recover,
            visible: () => room.Formed,
            about: () => Strings.AboutRecover);
        list.Cycle(
            () => Strings.MenuAssignCarrier,
            () => picks.Carrier,
            actor => picks.Carrier = actor,
            () => room.Pickable,
            actor => PickName(room, actor),
            about: () => Strings.AboutAssignCarrier,
            visible: () => room.Formed);
        list.Cycle(
            () => Strings.MenuAssignRider,
            () => picks.Rider,
            actor => picks.Rider = actor,
            () => room.Pickable,
            actor => PickName(room, actor),
            about: () => Strings.AboutAssignRider,
            visible: () => room.Formed);
        list.Action(
            () => Strings.MenuAssign,
            () => Strings.MenuGo,
            () => Assign(room, picks),
            visible: () => room.Formed,
            available: () => !room.Busy && room.CanAssign(picks.Carrier, picks.Rider),
            about: () => Strings.AboutAssign);

        list.Header(() => Strings.MenuRunRules);
        Rule(list, room, RunRule.CampfireShuffle, () => Strings.RuleCampfireShuffle, () => Strings.AboutCampfireShuffle);
        Rule(list, room, RunRule.TimedShuffle, () => Strings.RuleTimedShuffle, () => Strings.AboutTimedShuffle);
        list.Cycle(
            () => Strings.RuleTimedShuffleInterval,
            () => room.ShuffleMinutes,
            minutes => room.ShuffleMinutes = minutes,
            () => Intervals,
            Strings.Minutes,
            about: () => Strings.AboutTimedShuffleInterval);
        Rule(list, room, RunRule.HandOverPlace, () => Strings.RuleHandOverPlace, () => Strings.AboutHandOverPlace);
        Rule(list, room, RunRule.HandOverItems, () => Strings.RuleHandOverItems, () => Strings.AboutHandOverItems);
        Rule(list, room, RunRule.CarryWeight, () => Strings.RuleCarryWeight, () => Strings.AboutCarryWeight);
        Rule(list, room, RunRule.RiderPicksFromBackpack, () => Strings.RuleRiderPicksFromBackpack, () => Strings.AboutRiderPicksFromBackpack);
        Rule(list, room, RunRule.AllowOddPlayerCount, () => Strings.RuleAllowOddPlayerCount, () => Strings.AboutAllowOddPlayerCount);
    }

    /// <summary>Says why a Pair change the Troop Leader ordered did not start.</summary>
    internal static void Order(bool started)
    {
        if (!started)
        {
            CoopNotice.Info(Strings.MenuBusy);
        }
    }

    private static void Rule(PaperList list, ICoopRoom room, RunRule rule, Func<string> label, Func<string> about) =>
        list.Toggle(label, () => room.Rule(rule), on => room.SetRule(rule, on), about);

    private static void Assign(ICoopRoom room, AssignPicks picks)
    {
        if (!room.CanAssign(picks.Carrier, picks.Rider))
        {
            CoopNotice.Info(Strings.MenuAssignUnavailable);
            return;
        }

        Order(room.Assign(picks.Carrier, picks.Rider));
    }

    private static string PickName(ICoopRoom room, int actor) =>
        actor >= 0 && room.Pickable.Contains(actor) ? room.NameOf(actor) : "-";
}

/// <summary>
/// The game's own room as the Coop Menu sees it. A Run Rule is the Troop
/// Leader's own setting, saved and published to the room as any change in
/// the config editor is.
/// </summary>
internal sealed class LiveRoom : ICoopRoom
{
    public bool InRoom => PhotonNetwork.InRoom;

    public bool IsTroopLeader => PhotonNetwork.IsMasterClient;

    public bool Formed => PairRoster.Formed;

    public int Me => Character.localCharacter is Character local
        ? PairRoster.ActorOf(local)
        : PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;

    public IReadOnlyList<int> Players => PhotonNetwork.PlayerList.Select(p => p.ActorNumber).ToList();

    public IReadOnlyList<Pair> Pairs => PairRoster.Pairs;

    public RolePreference Preference
    {
        get => CoopConfig.RolePreference.Value;
        set => CoopConfig.RolePreference.Value = value;
    }

    public bool SwapPending => RoleSwap.Pending;

    public bool CanAskSwap => RoleSwap.CanRequest(out _);

    public bool Busy => PairShuffle.Busy;

    public double? SecondsToTimedShuffle => PairShuffle.SecondsToTimedShuffle();

    /// <summary>Players in the room with this version.</summary>
    public IReadOnlyList<int> Pickable =>
        PhotonNetwork.PlayerList
            .Where(p => !p.IsInactive && Net.ModPresence.VersionOf(p) == MyPluginInfo.PLUGIN_VERSION)
            .Select(p => p.ActorNumber)
            .OrderBy(a => a)
            .ToList();

    public int ShuffleMinutes
    {
        get => ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value);
        set => CoopConfig.TimedShuffleInterval.Value = value;
    }

    public string NameOf(int actor) =>
        PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.NickName ?? $"#{actor}";

    public void ToggleSwap() => RoleSwap.Toggle();

    public bool Shuffle() => PairShuffle.Request(OperationKind.OrderedShuffle);

    public bool SwapRoles(Pair pair) => PairShuffle.Request(OperationKind.RoleSwap, pair.Carrier, pair.Rider);

    public void Recover() => CarryKeeper.RequestRecovery();

    public bool CanAssign(int carrier, int rider) => PairShuffle.CanAssign(carrier, rider);

    public bool Assign(int carrier, int rider) => PairShuffle.Request(OperationKind.PairAssignment, carrier, rider);

    public bool Rule(RunRule rule) => Entry(rule).Value;

    public void SetRule(RunRule rule, bool on) => Entry(rule).Value = on;

    public void Tick(float seconds)
    {
    }

    private static BepInEx.Configuration.ConfigEntry<bool> Entry(RunRule rule) => rule switch
    {
        RunRule.CampfireShuffle => CoopConfig.CampfireShuffle,
        RunRule.TimedShuffle => CoopConfig.TimedShuffle,
        RunRule.HandOverPlace => CoopConfig.HandOverPlace,
        RunRule.HandOverItems => CoopConfig.HandOverItems,
        RunRule.CarryWeight => CoopConfig.CarryWeight,
        RunRule.RiderPicksFromBackpack => CoopConfig.RiderPicksFromBackpack,
        RunRule.AllowOddPlayerCount => CoopConfig.AllowOddPlayerCount,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };
}
