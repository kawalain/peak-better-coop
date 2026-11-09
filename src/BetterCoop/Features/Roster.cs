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

    /// <summary>The Troop Leader's draft of a Pair Assignment or a Pair Plan, kept while what it started from holds.</summary>
    private PairDraft? draft;

    private PairBoard? board;

    /// <summary>The header over the board, where a shuffle carries the eye.</summary>
    private RectTransform? pairsHeading;

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

    /// <summary>The Roster goes back to the Scoutmaster however it is left, read or changed.</summary>
    protected override bool HandsInOnLeave => true;

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
        if (room.HasReport())
        {
            ReportRows.Add(list, room, OpenReport);
        }

        board = null;
        pairsHeading = null;
        if (room.Formed || room.Planning)
        {
            PairDraft shown;
            if (leads)
            {
                draft ??= new PairDraft(Basis(), room.Players);
                draft.Follow(Basis(), room.Players);
                StartFromLastRun(draft);
                shown = draft;
            }
            else
            {
                shown = new PairDraft(Basis(), room.Players);
            }

            board = PairRows.Add(list, room, shown, portraits, leads, Moved, StartOver, HandIn, out RectTransform heading, () => fromLastRun && shown.Changed);
            pairsHeading = heading;
        }

        if (room.Formed)
        {
            RecordRows.Add(list, () => room.Record, room.Present);
        }

        if (leads)
        {
            TroopLeaderRows.Add(list, room, DrawPlan, ShowPairs);
        }

        if (room.InRoom)
        {
            RunRuleRows.Add(list, room, leads);
        }

        PlayerRows.Sign(list, room);
        Makers.Add(list);
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

    protected override void DropDraft() => draft?.Reset(Basis(), room.Players);

    private RunReportPage? reportPage;

    /// <summary>Opens the room's Run Report on its own page, which comes back here.</summary>
    private void OpenReport()
    {
        reportPage ??= CreateChild<RunReportPage>(() => Strings.MenuRunReport);
        reportPage.Show(room);
        reportPage.Open();
    }

    /// <summary>Whether this visit to the Airport has already offered the last Run's Pairs.</summary>
    private bool offeredLastRun;

    /// <summary>Whether the draft now holds the last Run's Pairs as they were laid out.</summary>
    private bool fromLastRun;

    /// <summary>
    /// At the Airport with no plan yet, lays the last Run's Pairs into the
    /// draft once, when the same scouts gather again; it stays a draft until
    /// announced.
    /// </summary>
    private void StartFromLastRun(PairDraft planDraft)
    {
        if (room.Formed)
        {
            offeredLastRun = false;
            fromLastRun = false;
            return;
        }

        if (offeredLastRun || room.Plan.Count > 0 || planDraft.Changed)
        {
            return;
        }

        offeredLastRun = true;
        IReadOnlyList<Pair> last = room.LastRunPairs;
        if (last.Count > 0)
        {
            planDraft.Lay(last, room.Players);
            fromLastRun = true;
        }
    }

    /// <summary>What the draft starts from: the Pairs on the island, the Pair Plan at the Airport.</summary>
    private IReadOnlyList<Pair> Basis() => room.Formed ? room.Pairs : room.Plan;

    /// <summary>Draws a plan at random into the draft, to be announced or changed further.</summary>
    private void DrawPlan()
    {
        if (draft == null)
        {
            return;
        }

        draft.Lay(room.DrawPlan(), room.Players);
        board?.Redraw();
    }

    /// <summary>Carries the eye up to the Pairs, as after a shuffle, so the new Pairs can be checked.</summary>
    private void ShowPairs()
    {
        if (pairsHeading != null && board != null)
        {
            ShowPart(pairsHeading, (RectTransform)board.transform);
        }
    }

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
        draft?.Reset(Basis(), room.Players);
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
        Action<Action> handIn,
        out RectTransform heading,
        Func<bool>? fromLastRun = null)
    {
        bool planning = !room.Formed;
        heading = list.Header(() => planning ? Strings.MenuPairPlan : Strings.MenuPairs);
        if (planning)
        {
            list.Note(() => leads ? Strings.MenuPlanHelp : Strings.MenuPlanNone, visible: () => leads || room.Plan.Count == 0);
        }

        if (leads)
        {
            list.Note(() => Strings.MenuBoardHelp);
            list.Note(() => Strings.MenuDraft, visible: () => draft.Changed);
            list.Note(() => Strings.MenuPlanFromLastRun, visible: () => fromLastRun?.Invoke() ?? false);
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
                () =>
                {
                    if (planning)
                    {
                        Plan(room, draft, handIn);
                    }
                    else
                    {
                        Approve(room, draft, handIn);
                    }
                },
                available: () => draft.Changed && (planning ? PairingRules.IsPlan(draft.Pairs, room.Players) : !room.Busy && room.CanArrange(draft.Pairs)),
                about: () => planning ? Strings.AboutPlan : Strings.AboutAssign,
                face: new PostmarkFace(() => Strings.PostmarkOffice, () => Strings.PostmarkDate(System.DateTime.Now)));
            list.Stamp(
                () => Strings.MenuAssignReset,
                () => Strings.StampCancel,
                startOver,
                visible: () => draft.Changed);
        }

        return board;
    }

    /// <summary>Announces the draft as the Troop Leader's Pair Plan, the paper handed in as it goes.</summary>
    private static void Plan(ICoopRoom room, PairDraft draft, Action<Action> handIn)
    {
        if (!draft.Changed)
        {
            return;
        }

        if (!PairingRules.IsPlan(draft.Pairs, room.Players))
        {
            CoopNotice.Info(Strings.MenuAssignUnavailable);
            return;
        }

        handIn(() => TroopLeaderRows.Order(room.SetPlan(draft.Pairs)));
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

/// <summary>The last Run's report, back at the Airport: how it ended, and a stamp to read it.</summary>
internal static class ReportRows
{
    public static void Add(PaperList list, ICoopRoom room, Action open)
    {
        list.Header(() => Strings.MenuRunReport);
        list.Stamp(
            () => Strings.MenuReadReport,
            () => Strings.StampRead,
            open,
            about: () => Strings.AboutRunReport,
            value: () => room.Report is RunReport report
                ? $"{Strings.EndingName(report.Ending)} · {Strings.Duration(report.Seconds)}"
                : $"{Strings.EndingName(RunEnding.Underway)} · {Strings.Duration(room.RunSeconds)}");
    }
}

/// <summary>The Carry Record as a table, the most time carrying first, scouts who left greyed.</summary>
internal static class RecordRows
{
    public static void Add(PaperList list, Func<CarryRecord?> record, Func<string, bool> present)
    {
        list.Header(() => Strings.MenuCarryRecord);
        list.Note(() => Strings.AboutCarryRecord);
        Table(list, record, present);
    }

    /// <summary>The record's table alone, for a page that heads it itself.</summary>
    public static void Table(PaperList list, Func<CarryRecord?> record, Func<string, bool> present)
    {
        CarryRecord? ranked = null;
        IReadOnlyList<TableEntry> entries = [];
        list.Table(
            () => [Strings.HeadScout, Strings.HeadCarried, Strings.HeadRidden, Strings.HeadClimbed],
            () =>
            {
                // Ranked again only when the room shares a new record.
                if (record() is not CarryRecord now)
                {
                    return [];
                }

                if (!ReferenceEquals(now, ranked))
                {
                    ranked = now;
                    entries = now.Ranked()
                        .Select(s => new TableEntry(
                            [present(s.Id) ? s.Name : Strings.Left(s.Name), Strings.Duration(s.Carried), Strings.Duration(s.Ridden), Strings.Metres(s.Climbed)],
                            faint: !present(s.Id)))
                        .ToList();
                }

                return entries;
            },
            empty: () => Strings.CarryRecordEmpty);
    }
}

/// <summary>The Troop Leader's orders for the Pairs on the Roster.</summary>
internal static class TroopLeaderRows
{
    /// <param name="drawPlan">At the Airport, draws a plan at random into the Troop Leader's draft.</param>
    /// <param name="showPairs">Carries the eye up to the Pairs once they are shuffled.</param>
    public static void Add(PaperList list, ICoopRoom room, Action drawPlan, Action showPairs)
    {
        list.Header(() => Strings.MenuTroopLeader);
        list.Stamp(
            () => room.Formed ? Strings.MenuShuffleNowLabel : Strings.MenuShufflePlanLabel,
            () => Strings.StampShuffle,
            () =>
            {
                if (room.Formed)
                {
                    bool started = room.Shuffle();
                    Order(started);
                    if (!started)
                    {
                        return;
                    }
                }
                else
                {
                    drawPlan();
                }

                showPairs();
            },
            available: () => !room.Busy,
            about: () => room.Formed ? Strings.AboutShuffleNow : Strings.AboutShufflePlan,
            visible: () => room.Formed || room.Planning,
            confirm: true);
        list.Stamp(
            () => Strings.MenuCallOffLabel,
            () => Strings.StampCallOff,
            () =>
            {
                if (!room.CallOffShuffle())
                {
                    CoopNotice.Info(Strings.CallOffUnavailable);
                }
            },
            available: () => room.CanCallOffShuffle,
            about: () => Strings.AboutCallOff,
            visible: () => room.Formed && (room.SecondsToTimedShuffle != null || room.Busy),
            value: () => room.Busy ? Strings.MenuShuffleNow : room.SecondsToTimedShuffle is double s ? Strings.Clock(s) : string.Empty,
            confirm: true);
        list.Stamp(
            () => Strings.MenuRecover,
            () => Strings.StampRestore,
            room.Recover,
            about: () => Strings.AboutRecover,
            visible: () => room.Formed,
            confirm: true);
    }

    /// <summary>Says why a Pair change the Troop Leader ordered did not start.</summary>
    internal static void Order(bool started)
    {
        if (!started)
        {
            CoopNotice.Info(Strings.MenuBusy);
        }
    }
}

/// <summary>The Run Rules: the Troop Leader sets them; every other player reads them.</summary>
internal static class RunRuleRows
{
    /// <summary>Timed Shuffle intervals the form steps through, in minutes.</summary>
    private static readonly int[] Intervals = [1, 2, 3, 5, 10, 15, 20, 30, 45, 60];

    public static void Add(PaperList list, ICoopRoom room, bool leads)
    {
        Func<string?> locked = () => leads ? null : Strings.RuleLeaderOnly;
        list.Header(() => Strings.MenuRunRules);
        if (!leads)
        {
            list.Note(() => Strings.MenuRulesRead);
        }

        void Rule(RunRule rule, Func<string> label, Func<string> about) =>
            list.Check(label, () => room.Rule(rule), on => room.SetRule(rule, on), about, locked: locked);

        list.Subheading(() => Strings.RulesPairs);
        Rule(RunRule.FollowRolePreferences, () => Strings.RuleFollowRolePreferences, () => Strings.AboutFollowRolePreferences);
        Rule(RunRule.AllowSwapRequests, () => Strings.RuleAllowSwapRequests, () => Strings.AboutAllowSwapRequests);
        Rule(RunRule.LatePairing, () => Strings.RuleLatePairing, () => Strings.AboutLatePairing);
        Rule(RunRule.AllowOddPlayerCount, () => Strings.RuleAllowOddPlayerCount, () => Strings.AboutAllowOddPlayerCount);
        list.Subheading(() => Strings.RulesShuffles);
        Rule(RunRule.CampfireShuffle, () => Strings.RuleCampfireShuffle, () => Strings.AboutCampfireShuffle);
        Rule(RunRule.TimedShuffle, () => Strings.RuleTimedShuffle, () => Strings.AboutTimedShuffle);
        list.Choice(
            () => Strings.RuleTimedShuffleInterval,
            () => room.ShuffleMinutes,
            minutes => room.ShuffleMinutes = minutes,
            () => Intervals,
            Strings.Minutes,
            about: () => Strings.AboutTimedShuffleInterval,
            locked: locked);
        list.Subheading(() => Strings.RulesHandover);
        Rule(RunRule.HandOverPlace, () => Strings.RuleHandOverPlace, () => Strings.AboutHandOverPlace);
        Rule(RunRule.HandOverItems, () => Strings.RuleHandOverItems, () => Strings.AboutHandOverItems);
        list.Subheading(() => Strings.RulesCarrying);
        Rule(RunRule.CarryWeight, () => Strings.RuleCarryWeight, () => Strings.AboutCarryWeight);
        Rule(RunRule.RiderPicksFromBackpack, () => Strings.RuleRiderPicksFromBackpack, () => Strings.AboutRiderPicksFromBackpack);
    }
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

    public bool Planning => PhotonNetwork.InRoom && !GameHandler.IsOnIsland;

    public IReadOnlyList<Pair> Plan => PairPlans.Read();

    public IReadOnlyList<Pair> LastRunPairs => PairPlans.LastRun();

    public CarryRecord? Record => CarryRecords.Shared;

    public RunReport? Report => CarryRecords.Report;

    public double RunSeconds => RunManager.Instance != null ? RunManager.Instance.TimeSinceRunStarted : 0;

    /// <summary>A player is known by user id, or by actor number when Photon gives none.</summary>
    public bool Present(string id) =>
        PhotonNetwork.InRoom && PhotonNetwork.PlayerList.Any(p => !p.IsInactive && (p.UserId == id || $"#{p.ActorNumber}" == id));

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

    /// <summary>The Troop Leader's own setting, which the room follows; every other player reads the room's.</summary>
    public int ShuffleMinutes
    {
        get => IsTroopLeader
            ? ShuffleInterval.Clamp(CoopConfig.TimedShuffleInterval.Value)
            : (int)Math.Round(RunRules.TimedShuffleSeconds / 60.0);
        set
        {
            if (IsTroopLeader)
            {
                CoopConfig.TimedShuffleInterval.Value = value;
            }
        }
    }

    public string NameOf(int actor) =>
        PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.NickName ?? $"#{actor}";

    public void ToggleSwap() => RoleSwap.Toggle();

    public bool Shuffle() => PairShuffle.Request(OperationKind.OrderedShuffle);

    public void Recover() => CarryKeeper.RequestRecovery();

    public bool CanCallOffShuffle => PairShuffle.CanCallOff;

    public bool CallOffShuffle() => PairShuffle.CallOff();

    public bool CanArrange(IReadOnlyList<Pair> pairs) => PairShuffle.CanArrange(pairs);

    public bool Arrange(IReadOnlyList<Pair> pairs) => PairShuffle.RequestAssignment(pairs);

    public bool SetPlan(IReadOnlyList<Pair> plan) => PairPlans.Announce(plan);

    public IReadOnlyList<Pair> DrawPlan() => PairPlans.Draw();

    public bool Rule(RunRule rule) => IsTroopLeader ? RunRules.Entry(rule).Value : RunRules.Is(rule);

    public void SetRule(RunRule rule, bool on)
    {
        if (IsTroopLeader)
        {
            RunRules.Entry(rule).Value = on;
        }
    }

    public void Tick(float seconds)
    {
    }
}
