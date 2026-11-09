using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using BetterCoop.Core;
using BetterCoop.Net;
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

    private IPortraits portraits = new LivePortraits();

    /// <summary>The Troop Leader's draft of a Pair Assignment, kept while the room's Pairs hold.</summary>
    private PairDraft? draft;

    private PairBoard? board;

    /// <summary>Where each scout's photo was before the board was drawn again, so it moves rather than jumps.</summary>
    private IReadOnlyDictionary<int, Vector3>? places;

    /// <summary>Shows another room, with its scouts' photos, its rows filled anew at once.</summary>
    public void Show(ICoopRoom shown, IPortraits faces)
    {
        room = shown;
        portraits = faces;
        draft = null;
        Refill();
    }

    protected override string Shape() => room.Shape();

    protected override bool HasDraft => draft is { Changed: true } && room.InRoom && room.IsTroopLeader && !room.Busy;

    protected override string FormNumber() => Strings.FormTitle;

    protected override string FormNote() => room.InRoom ? Strings.FormMembers(room.Players.Count) : string.Empty;

    /// <summary>A number for the troop, the same while the same scout leads it.</summary>
    protected override string FormSerial() =>
        room.InRoom && room.TroopLeader >= 0
            ? Strings.Serial((int)(FormDrawing.Hash(room.NameOf(room.TroopLeader)) % 10_000_000))
            : string.Empty;

    protected override void Fill(PaperList list)
    {
        bool leads = room.InRoom && room.IsTroopLeader;
        PlayerRows.Add(list, room);
        board = null;
        if (room.Formed)
        {
            PairDraft shown;
            if (leads)
            {
                draft ??= new PairDraft(room.Pairs, room.Players);
                draft.Follow(room.Pairs, room.Players);
                shown = draft;
            }
            else
            {
                shown = new PairDraft(room.Pairs, room.Players);
            }

            board = PairRows.Add(list, room, shown, portraits, leads, Moved, StartOver, HandIn);
        }

        if (leads)
        {
            TroopLeaderRows.Add(list, room);
        }

        PlayerRows.Sign(list, room);
    }

    protected override void Tick() => room.Tick(Time.unscaledDeltaTime);

    /// <summary>Draws the board again after a photo moved, its photo still chosen on a gamepad.</summary>
    private void Moved(int actor)
    {
        board?.Redraw();
        if (Zorro.ControllerSupport.InputHandler.GetCurrentUsedInputScheme() == Zorro.ControllerSupport.InputScheme.Gamepad)
        {
            board?.Select(actor);
        }
    }

    protected override void DropDraft() => draft?.Reset(room.Pairs, room.Players);

    protected override void BeforeRefill() => places = board != null ? board.Places() : null;

    protected override void AfterRefill()
    {
        if (board != null && places != null)
        {
            board.GlideFrom(places);
        }

        places = null;
    }

    private void StartOver()
    {
        draft?.Reset(room.Pairs, room.Players);
        board?.Redraw();
    }
}

/// <summary>The room's Pairs as a board of scout cards, which the Troop Leader rearranges and approves.</summary>
internal static class PairRows
{
    public static PairBoard Add(
        PaperList list,
        ICoopRoom room,
        PairDraft draft,
        IPortraits portraits,
        bool leads,
        Action<int> moved,
        Action startOver,
        Action<Action> handIn)
    {
        list.Header(() => Strings.MenuPairs);
        if (leads)
        {
            list.Note(() => Strings.MenuBoardHelp);
            list.Note(() => Strings.MenuDraft, visible: () => draft.Changed);
        }

        PairBoard? board = null;
        RectTransform holder = list.Custom("Board", () => { }, () => board?.First);
        board = PairBoard.Build(holder, list.Template, draft, portraits, room.NameOf, leads, moved);
        list.OnBack(board.PutBack);
        if (leads)
        {
            list.Stamp(
                () => Strings.MenuAssign,
                () => Strings.StampNotify,
                () => Approve(room, draft, handIn),
                available: () => draft.Changed && !room.Busy && room.CanArrange(draft.Pairs),
                about: () => Strings.AboutAssign,
                face: new PostmarkFace(() => Strings.PostmarkOffice, () => Strings.PostmarkDate(System.DateTime.Now)));
            list.Stamp(
                () => Strings.MenuAssignReset,
                () => Strings.StampCancel,
                startOver,
                visible: () => draft.Changed);
        }

        return board;
    }

    /// <summary>Sends the draft as the Troop Leader's Pair Assignment, the paper handed in as it goes.</summary>
    private static void Approve(ICoopRoom room, PairDraft draft, Action<Action> handIn)
    {
        if (!draft.Changed)
        {
            return;
        }

        if (!room.CanArrange(draft.Pairs))
        {
            CoopNotice.Info(Strings.MenuAssignUnavailable);
            return;
        }

        handIn(() => TroopLeaderRows.Order(room.Arrange(draft.Pairs)));
    }
}

/// <summary>The parts of the Roster every player fills in: their own Pair and Role Preference, and the room's Pairs.</summary>
internal static class PlayerRows
{
    private static readonly RolePreference[] Preferences = [RolePreference.Random, RolePreference.Carrier, RolePreference.Rider];

    public static void Add(PaperList list, ICoopRoom room)
    {
        list.Header(() => Strings.MenuYou);
        Func<bool> wishesHeard = () => room.Rule(RunRule.FollowRolePreferences);
        list.Choice(
            () => Strings.MenuRolePreference,
            () => room.Preference,
            value => room.Preference = value,
            () => Preferences,
            Strings.RoleName,
            about: () => wishesHeard() ? Strings.AboutRolePreference : Strings.AboutRolePreferenceIgnored,
            locked: () => wishesHeard() ? null : Strings.AboutRolePreferenceIgnored);
        Func<bool> paired = () => room.PairOf(room.Me) != null;
        list.Field(() => Strings.MenuPartner, () => PartnerName(room), visible: paired);
        list.Field(() => Strings.MenuYourRole, () => Strings.RoleOf(room.PairOf(room.Me)?.Carrier == room.Me), visible: paired);
        list.Stamp(
            () => Strings.SwapPlaces,
            () => room.SwapPending ? Strings.StampCancel : Strings.StampAsk,
            room.ToggleSwap,
            available: () => room.SwapPending || room.CanAskSwap,
            about: () => room.Rule(RunRule.AllowSwapRequests) ? Strings.AboutSwapPlaces : Strings.AboutSwapNotAllowed,
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

    public static void Add(PaperList list, ICoopRoom room)
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

        list.Header(() => Strings.MenuRunRules);
        list.Subheading(() => Strings.RulesPairs);
        Rule(list, room, RunRule.FollowRolePreferences, () => Strings.RuleFollowRolePreferences, () => Strings.AboutFollowRolePreferences);
        Rule(list, room, RunRule.AllowSwapRequests, () => Strings.RuleAllowSwapRequests, () => Strings.AboutAllowSwapRequests);
        Rule(list, room, RunRule.LatePairing, () => Strings.RuleLatePairing, () => Strings.AboutLatePairing);
        Rule(list, room, RunRule.AllowOddPlayerCount, () => Strings.RuleAllowOddPlayerCount, () => Strings.AboutAllowOddPlayerCount);
        list.Subheading(() => Strings.RulesShuffles);
        Rule(list, room, RunRule.CampfireShuffle, () => Strings.RuleCampfireShuffle, () => Strings.AboutCampfireShuffle);
        Rule(list, room, RunRule.TimedShuffle, () => Strings.RuleTimedShuffle, () => Strings.AboutTimedShuffle);
        list.Choice(
            () => Strings.RuleTimedShuffleInterval,
            () => room.ShuffleMinutes,
            minutes => room.ShuffleMinutes = minutes,
            () => Intervals,
            Strings.Minutes,
            about: () => Strings.AboutTimedShuffleInterval);
        list.Subheading(() => Strings.RulesHandover);
        Rule(list, room, RunRule.HandOverPlace, () => Strings.RuleHandOverPlace, () => Strings.AboutHandOverPlace);
        Rule(list, room, RunRule.HandOverItems, () => Strings.RuleHandOverItems, () => Strings.AboutHandOverItems);
        list.Subheading(() => Strings.RulesCarrying);
        Rule(list, room, RunRule.CarryWeight, () => Strings.RuleCarryWeight, () => Strings.AboutCarryWeight);
        Rule(list, room, RunRule.RiderPicksFromBackpack, () => Strings.RuleRiderPicksFromBackpack, () => Strings.AboutRiderPicksFromBackpack);
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

}

/// <summary>
/// The game's own room as the Roster sees it. A Run Rule is the Troop
/// Leader's own setting, saved and published to the room as any change in
/// the config editor is; every other player reads the room's.
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

    public int ShuffleMinutes
    {
        get => ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value);
        set => CoopConfig.TimedShuffleInterval.Value = value;
    }

    public string NameOf(int actor) =>
        PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.NickName ?? $"#{actor}";

    public void ToggleSwap() => RoleSwap.Toggle();

    public bool Shuffle() => PairShuffle.Request(OperationKind.OrderedShuffle);

    public void Recover() => CarryKeeper.RequestRecovery();

    public bool CanArrange(IReadOnlyList<Pair> pairs) => PairShuffle.CanArrange(pairs);

    public bool Arrange(IReadOnlyList<Pair> pairs) => PairShuffle.RequestAssignment(pairs);

    public bool Rule(RunRule rule) => IsTroopLeader ? RunRules.Entry(rule).Value : RunRules.Is(rule);

    public void SetRule(RunRule rule, bool on) => RunRules.Entry(rule).Value = on;

    public void Tick(float seconds)
    {
    }
}
