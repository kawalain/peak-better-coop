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
/// The Roster: one button in the pause menu opening a paper page
/// that keeps the game's own Back button and menu button style. Debug builds
/// also get the Zoo next to it.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class Roster
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(PauseMenuMainPage), "Start")]
    private static void AddMenu(PauseMenuMainPage __instance)
    {
        UIPageHandler handler = __instance.GetComponentInParent<UIPageHandler>(true);
        PauseMenuControlsPage? controls = handler != null ? handler.GetComponentInChildren<PauseMenuControlsPage>(true) : null;
        if (handler == null || controls == null)
        {
            Plugin.Log.LogWarning("The pause menu has no Controls page to build the Roster from.");
            return;
        }

        // Pages are built on first use, after the pause menu has started: its
        // handler registers the pages under it by type, one each, and would
        // fail on two of a type (the Roster and the Zoo's example of it).
        Button template = __instance.m_settingsButton;
        RosterPage? page = null;
        Button roster = AddButton(
            __instance,
            Strings.Roster,
            () => (page ??= PaperPage.Create<RosterPage>(handler, controls, template, __instance, () => Strings.Roster)).Open(),
            after: __instance.m_resumeButton);
#if DEBUG
        ZooPage? zoo = null;
        AddButton(__instance, "Zoo", () => (zoo ??= ZooPage.Build(handler, controls, template, __instance)).Open(), after: roster);
#endif
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
/// The Roster page: the room's Pairs and the local player's own Pair and
/// Role Preference, and for the Troop Leader the Pair changes and the Run
/// Rules. It shows whatever room it is given: the game's own, or a made-up
/// one in the Zoo.
/// </summary>
internal sealed class RosterPage : PaperPage
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

        PlayerRows.Sign(list, room);
    }

    protected override void Tick() => room.Tick(Time.unscaledDeltaTime);
}

/// <summary>The players the Troop Leader has picked for a Pair Assignment, by actor; -1 for none yet.</summary>
internal sealed class AssignPicks
{
    public int Carrier { get; set; } = -1;

    public int Rider { get; set; } = -1;
}

/// <summary>The parts of the Roster every player fills in: their own Pair and Role Preference, and the room's Pairs.</summary>
internal static class PlayerRows
{
    private static readonly RolePreference[] Preferences = [RolePreference.Random, RolePreference.Carrier, RolePreference.Rider];

    public static void Add(PaperList list, ICoopRoom room)
    {
        list.Masthead(() => Strings.FormTitle, () => Strings.FormMembers(room.Players.Count));

        list.Header(() => Strings.MenuYou);
        list.Choice(
            () => Strings.MenuRolePreference,
            () => room.Preference,
            value => room.Preference = value,
            () => Preferences,
            Strings.RoleName,
            about: () => Strings.AboutRolePreference);
        Func<bool> paired = () => room.PairOf(room.Me) != null;
        list.Field(() => Strings.MenuPartner, () => PartnerName(room), visible: paired);
        list.Field(() => Strings.MenuYourRole, () => Strings.RoleOf(room.PairOf(room.Me)?.Carrier == room.Me), visible: paired);
        list.Stamp(
            () => Strings.SwapPlaces,
            () => room.SwapPending ? Strings.StampCancel : Strings.StampAsk,
            room.ToggleSwap,
            available: () => room.SwapPending || room.CanAskSwap,
            about: () => Strings.AboutSwapPlaces,
            visible: paired);
        list.Field(
            () => Strings.MenuPartner,
            () => "—",
            about: () => $"{Strings.MenuNoPartner}. {Strings.AboutNoPartner}",
            visible: () => room.Formed && !paired());
        list.Field(
            () => Strings.MenuNextShuffle,
            () => NextShuffle(room),
            about: () => Strings.AboutNextShuffle,
            visible: () => room.Formed);
        list.Note(() => Strings.MenuPairsForm, visible: () => !room.Formed);

        if (!room.Formed)
        {
            return;
        }

        list.Header(() => Strings.MenuPairs);
        int number = 0;
        foreach (Pair pair in room.Pairs)
        {
            int shown = ++number;
            Func<string> names = () => Strings.MenuPair(room.NameOf(pair.Carrier), room.NameOf(pair.Rider));
            if (room.IsTroopLeader)
            {
                // The Troop Leader turns any Pair round without waiting for both to ask.
                list.Stamp(
                    () => Strings.MenuPairNumber(shown),
                    () => Strings.StampSwap,
                    () => TroopLeaderRows.Order(room.SwapRoles(pair)),
                    available: () => !room.Busy,
                    value: names);
            }
            else
            {
                list.Field(() => Strings.MenuPairNumber(shown), names);
            }
        }

        list.Field(
            () => Strings.MenuUnpaired,
            () => string.Join(", ", room.Unpaired().Select(room.NameOf)),
            visible: () => room.Unpaired().Any());
    }

    /// <summary>The line at the foot of the form, signed by the Troop Leader.</summary>
    public static void Sign(PaperList list, ICoopRoom room) =>
        list.Signature(
            () => Strings.MenuTroopLeaderSign,
            () => room.TroopLeader >= 0 ? room.NameOf(room.TroopLeader) : string.Empty);

    private static string PartnerName(ICoopRoom room) =>
        room.PairOf(room.Me) is Pair pair ? room.NameOf(pair.PartnerOf(room.Me)) : string.Empty;

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

/// <summary>The Troop Leader's part of the Roster: orders for the Pairs, and the Run Rules.</summary>
internal static class TroopLeaderRows
{
    /// <summary>Timed Shuffle intervals the form steps through, in minutes.</summary>
    private static readonly int[] Intervals = [1, 2, 3, 5, 10, 15, 20, 30, 45, 60];

    public static void Add(PaperList list, ICoopRoom room, AssignPicks picks)
    {
        list.Header(() => Strings.MenuTroopLeader);
        list.Stamp(
            () => Strings.MenuShuffleNowLabel,
            () => Strings.StampShuffle,
            () => Order(room.Shuffle()),
            available: () => !room.Busy,
            about: () => Strings.AboutShuffleNow,
            visible: () => room.Formed);
        list.Stamp(
            () => Strings.MenuRecover,
            () => Strings.StampRestore,
            room.Recover,
            about: () => Strings.AboutRecover,
            visible: () => room.Formed);
        list.Choice(
            () => Strings.MenuAssignCarrier,
            () => picks.Carrier,
            actor => picks.Carrier = actor,
            () => room.Pickable,
            actor => PickName(room, actor),
            about: () => Strings.AboutAssignCarrier,
            visible: () => room.Formed);
        list.Choice(
            () => Strings.MenuAssignRider,
            () => picks.Rider,
            actor => picks.Rider = actor,
            () => room.Pickable,
            actor => PickName(room, actor),
            about: () => Strings.AboutAssignRider,
            visible: () => room.Formed);
        list.Stamp(
            () => Strings.MenuAssign,
            () => Strings.StampApproved,
            () => Assign(room, picks),
            available: () => !room.Busy && room.CanArrange(Arrangement.WithPair(room.Pairs, picks.Carrier, picks.Rider)),
            about: () => Strings.AboutAssign,
            visible: () => room.Formed);

        list.Header(() => Strings.MenuRunRules);
        Rule(list, room, RunRule.CampfireShuffle, () => Strings.RuleCampfireShuffle, () => Strings.AboutCampfireShuffle);
        Rule(list, room, RunRule.TimedShuffle, () => Strings.RuleTimedShuffle, () => Strings.AboutTimedShuffle);
        list.Choice(
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
        list.Check(label, () => room.Rule(rule), on => room.SetRule(rule, on), about);

    private static void Assign(ICoopRoom room, AssignPicks picks)
    {
        IReadOnlyList<Pair> pairs = Arrangement.WithPair(room.Pairs, picks.Carrier, picks.Rider);
        if (picks.Carrier == picks.Rider || !room.CanArrange(pairs))
        {
            CoopNotice.Info(Strings.MenuAssignUnavailable);
            return;
        }

        Order(room.Arrange(pairs));
    }

    private static string PickName(ICoopRoom room, int actor) =>
        actor >= 0 && room.Pickable.Contains(actor) ? room.NameOf(actor) : "-";
}

/// <summary>
/// The game's own room as the Roster sees it. A Run Rule is the Troop
/// Leader's own setting, saved and published to the room as any change in
/// the config editor is.
/// </summary>
internal sealed class LiveRoom : ICoopRoom
{
    public bool InRoom => PhotonNetwork.InRoom;

    public bool IsTroopLeader => PhotonNetwork.IsMasterClient;

    public int TroopLeader => PhotonNetwork.MasterClient?.ActorNumber ?? -1;

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

    public bool CanArrange(IReadOnlyList<Pair> pairs) => PairShuffle.CanArrange(pairs);

    public bool Arrange(IReadOnlyList<Pair> pairs) => PairShuffle.RequestAssignment(pairs);

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
