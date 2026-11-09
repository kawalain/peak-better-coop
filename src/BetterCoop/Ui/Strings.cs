using System;
using System.Globalization;
using System.IO;
using BetterCoop.Core;

namespace BetterCoop.Ui;

/// <summary>
/// Player-facing text in the game's language. Every sentence lives in the
/// one strings file, <c>Ui/Locale/strings.json</c>, built into the plugin;
/// this class only picks the key and fills its placeholders, so adding a
/// language means adding its sentences there and nothing here.
/// </summary>
internal static class Strings
{
    /// <summary>The strings file's name inside the plugin.</summary>
    internal const string Resource = "BetterCoop.strings.json";

    private static TextTable? table;

    private static TextTable Table => table ??= Load();

    /// <summary>The strings file's code for the game's language, as the player set it.</summary>
    internal static string LanguageCode => CodeOf(LocalizedText.CURRENT_LANGUAGE);

    private static string CodeOf(LocalizedText.Language language) => language switch
    {
        LocalizedText.Language.French => "fr",
        LocalizedText.Language.Italian => "it",
        LocalizedText.Language.German => "de",
        LocalizedText.Language.SpanishSpain => "es-ES",
        LocalizedText.Language.SpanishLatam => "es-419",
        LocalizedText.Language.BRPortuguese => "pt-BR",
        LocalizedText.Language.Russian => "ru",
        LocalizedText.Language.Ukrainian => "uk",
        LocalizedText.Language.SimplifiedChinese => "zh-Hans",
        LocalizedText.Language.TraditionalChinese => "zh-Hant",
        LocalizedText.Language.Japanese => "ja",
        LocalizedText.Language.Korean => "ko",
        LocalizedText.Language.Polish => "pl",
        LocalizedText.Language.Turkish => "tr",
        _ => TextTable.Fallback,
    };

    private static TextTable Load()
    {
        try
        {
            using Stream? stream = typeof(Strings).Assembly.GetManifestResourceStream(Resource);
            if (stream == null)
            {
                Plugin.Log.LogError($"The plugin has no {Resource}; text shows as its keys.");
                return TextTable.Empty;
            }

            using var reader = new StreamReader(stream);
            return TextTable.Parse(reader.ReadToEnd());
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError($"Could not read {Resource}; text shows as its keys. {exception.Message}");
            return TextTable.Empty;
        }
    }

    /// <summary>A key's sentence in the game's language.</summary>
    private static string T(string key) => Table.Get(key, LanguageCode);

    /// <summary>A key's sentence in the game's language, its placeholders filled.</summary>
    private static string T(string key, params (string Name, object? Value)[] values) => TextTable.Fill(T(key), values);

    /// <summary>A key's sentence in English, for text that does not follow the game's language.</summary>
    internal static string English(string key, params (string Name, object? Value)[] values) =>
        TextTable.Fill(Table.Get(key, TextTable.Fallback), values);

    /// <summary>
    /// A notice as the game's own player log shows it, marked as the mod's:
    /// the log is the fallback when the paper cannot be shown, and all there
    /// is while the mod is off.
    /// </summary>
    public static string Prefixed(string message) => T("notice.prefix") + message;

    public static string ModDisabled => T("notice.modDisabled");

    public static string ModDisabledByCoopMod => T("notice.modDisabledByCoopMod");

    public static string PairFormed(string carrier, string rider) => T("notice.pairFormed", ("carrier", carrier), ("rider", rider));

    public static string PreferenceOverridden(string player) => T("notice.preferenceOverridden", ("player", player));

    public static string Unpaired(string player) => T("notice.unpaired", ("player", player));

    public static string PairDissolved(string carrier, string rider) => T("notice.pairDissolved", ("carrier", carrier), ("rider", rider));

    public static string ShuffleCountdown(int seconds) => T("notice.shuffleCountdown", ("seconds", seconds));

    public static string RoleSwapCountdown(int seconds) => T("notice.roleSwapCountdown", ("seconds", seconds));

    public static string LatePairingCountdown(int seconds) => T("notice.latePairingCountdown", ("seconds", seconds));

    public static string Roster => T("roster.title");

    public static string MenuRolePreference => T("roster.rolePreference");

    public static string RoleName(Core.RolePreference preference) => preference switch
    {
        Core.RolePreference.Carrier => T("role.carrier"),
        Core.RolePreference.Rider => T("role.rider"),
        _ => T("role.either"),
    };

    /// <summary>The Roster's printed head: what form it is.</summary>
    public static string FormTitle => T("roster.form");

    public static string FormMembers(int count) => T(count == 1 ? "roster.scouts.one" : "roster.scouts.other", ("count", count));

    public static string MenuYou => T("roster.yourPair");

    public static string MenuPartner => T("roster.partner");

    public static string MenuYourRole => T("roster.yourRole");

    public static string RoleOf(bool carrying) => T(carrying ? "role.carrier" : "role.rider");

    public static string MenuNoPartner => T("roster.noPartner");

    public static string MenuNextShuffle => T("roster.nextShuffle");

    public static string MenuShuffleNow => T("roster.shuffleUnderway");

    public static string MenuShuffleOff => T("roster.shuffleOff");

    public static string Clock(double seconds)
    {
        int whole = (int)Math.Ceiling(seconds);
        return $"{whole / 60}:{whole % 60:00}";
    }

    /// <summary>A length of time as hours, minutes, and seconds, the hours left out while there are none.</summary>
    public static string Duration(double seconds)
    {
        int whole = (int)Math.Floor(Math.Max(0, seconds));
        return whole >= 3600
            ? $"{whole / 3600}:{whole / 60 % 60:00}:{whole % 60:00}"
            : $"{whole / 60}:{whole % 60:00}";
    }

    public static string Metres(double metres) => T("unit.metres", ("metres", Math.Floor(metres).ToString("0", CultureInfo.InvariantCulture)));

    public static string MenuCarryRecord => T("roster.carryRecord");

    public static string AboutCarryRecord => T("roster.aboutCarryRecord");

    public static string CarryRecordEmpty => T("roster.carryRecordEmpty");

    public static string HeadScout => T("head.scout");

    public static string HeadCarried => T("head.carried");

    public static string HeadRidden => T("head.ridden");

    public static string HeadClimbed => T("head.climbed");

    public static string HeadRescuing => T("head.rescuing");

    public static string MenuRunReport => T("roster.runReport");

    public static string ReportFormTitle => T("report.form");

    public static string MenuReadReport => T("roster.readReport");

    public static string StampRead => T("stamp.read");

    public static string AboutRunReport => T("roster.aboutRunReport");

    public static string ReportUnderway => T("report.underway");

    public static string ReportGone => T("report.gone");

    public static string ReportResult => T("report.result");

    public static string ReportEnding => T("report.ending");

    public static string ReportRunTime => T("report.runTime");

    public static string EndingName(RunEnding ending) => ending switch
    {
        RunEnding.Summit => T("ending.summit"),
        RunEnding.Lost => T("ending.lost"),
        RunEnding.Underway => T("ending.underway"),
        _ => T("ending.airport"),
    };

    public static string MenuMeritBadges => T("report.meritBadges");

    public static string NoBadges => T("report.noBadges");

    public static string BadgeName(MeritBadgeKind kind) => kind switch
    {
        MeritBadgeKind.SteadyBack => T("badge.steadyBack"),
        MeritBadgeKind.ComfyPassenger => T("badge.comfyPassenger"),
        MeritBadgeKind.RescueRanger => T("badge.rescueRanger"),
        _ => T("badge.bestBuddies"),
    };

    /// <summary>What won a Merit Badge, as it counts: time.</summary>
    public static string BadgeAmount(MeritBadgeKind kind, double amount) => T(
        "badge.amount",
        ("what", kind switch
        {
            MeritBadgeKind.SteadyBack => HeadCarried,
            MeritBadgeKind.ComfyPassenger => HeadRidden,
            MeritBadgeKind.RescueRanger => HeadRescuing,
            _ => HeadTogether,
        }),
        ("time", Duration(amount)));

    public static string ReportBySegment => T("report.bySegment");

    public static string HeadPair => T("head.pair");

    public static string HeadTogether => T("head.together");

    /// <summary>A scout in a record who is no longer in the room.</summary>
    public static string Left(string name) => T("report.left", ("name", name));

    /// <summary>Two names that share one thing, such as a Merit Badge.</summary>
    public static string Both(string first, string second) => T("symbol.and", ("first", first), ("second", second));

    /// <summary>Who carried whom.</summary>
    public static string Carries(string carrier, string rider) => T("symbol.carries", ("carrier", carrier), ("rider", rider));

    /// <summary>Two short values side by side, as a Run's ending and its time.</summary>
    public static string Then(string first, string second) => T("symbol.then", ("first", first), ("second", second));

    /// <summary>Two sentences one after the other, the first without its full stop.</summary>
    public static string Sentences(string first, string second) => T("symbol.sentences", ("first", first), ("second", second));

    /// <summary>A value that is not there, such as a partner's name with no partner.</summary>
    public static string None => T("symbol.none");

    public static string MenuPairsForm => T("roster.pairsForm");

    public static string MenuPairs => T("roster.pairs");

    public static string MenuPairPlan => T("roster.pairPlan");

    public static string MenuPlanHelp => T("roster.planHelp");

    public static string MenuPlanFromLastRun => T("roster.planFromLastRun");

    public static string MenuPlanNone => T("roster.planNone");

    public static string AboutPlan => T("roster.aboutPlan");

    public static string PlanAnnounced => T("notice.planAnnounced");

    public static string MenuPairNumber(int number) => T("roster.pairNumber", ("number", number));

    public static string MenuTroopLeader => T("roster.troopLeaderOrders");

    public static string MenuTroopLeaderSign => T("roster.troopLeaderSign");

    public static string MenuShuffleNowLabel => T("roster.shuffleNow");

    public static string MenuShufflePlanLabel => T("roster.shufflePlan");

    public static string AboutShufflePlan => T("roster.aboutShufflePlan");

    public static string MenuRecover => T("roster.recover");

    public static string StampShuffle => T("stamp.shuffle");

    public static string StampRestore => T("stamp.restore");

    public static string StampReally => T("stamp.really");

    public static string ReportArrived => T("notice.reportArrived");

    public static string StampCallOff => T("stamp.callOff");

    public static string MenuCallOffLabel => T("roster.callOff");

    public static string AboutCallOff => T("roster.aboutCallOff");

    public static string CallOffUnavailable => T("notice.callOffUnavailable");

    public static string ShuffleCalledOff => T("notice.shuffleCalledOff");

    /// <summary>A Role Swap or Late Pairing called off because the Troop Leader turned off the rule that started it.</summary>
    public static string CalledOffByRule(OperationKind kind) => T(kind == OperationKind.RoleSwap
        ? "notice.roleSwapCalledOffByRule"
        : "notice.latePairingCalledOffByRule");

    public static string ShuffleCalledOffUntil(string clock) => T("notice.shuffleCalledOffUntil", ("clock", clock));

    public static string LetterCalledOff => T("letter.calledOff");

    public static string LetterStays => T("letter.stays");

    public static string Serial(int number) => T("roster.serial", ("number", number.ToString("D7", CultureInfo.InvariantCulture)));

    public static string NoticeHeading(Core.NoticeKind kind) => kind switch
    {
        Core.NoticeKind.Memo => T("notice.kind.memo"),
        Core.NoticeKind.Tag => T("notice.kind.tag"),
        Core.NoticeKind.Caution => T("notice.kind.caution"),
        _ => T("notice.kind.telegram"),
    };

    /// <summary>The word on the rubber stamp of a caution.</summary>
    public static string CautionStamp => T("notice.cautionStamp");

    /// <summary>The Pair letter's heading: what is changing.</summary>
    public static string LetterHeading(Core.OperationKind kind) => kind switch
    {
        Core.OperationKind.RoleSwap => T("letter.heading.roleSwap"),
        Core.OperationKind.LatePairing => T("letter.heading.latePairing"),
        Core.OperationKind.PairAssignment => T("letter.heading.assignment"),
        Core.OperationKind.None => T("letter.heading.formation"),
        _ => T("letter.heading.shuffle"),
    };

    /// <summary>The countdown on a Pair letter, saying what the change does to the reader.</summary>
    public static string LetterSoon(PairChangeForMe change, int seconds) => T(
        change switch
        {
            PairChangeForMe.Gains => "letter.soon.gains",
            PairChangeForMe.Swaps => "letter.soon.swaps",
            PairChangeForMe.Loses => "letter.soon.loses",
            PairChangeForMe.Mixed => "letter.soon.mixed",
            _ => "letter.soon.changes",
        },
        ("seconds", seconds));

    /// <summary>The last line of a Pair letter once the change is made.</summary>
    public static string LetterDoneFor(PairChangeForMe change) => change switch
    {
        PairChangeForMe.Swaps => T("letter.doneSwapped"),
        PairChangeForMe.Loses => LetterAlone,
        _ => LetterDone,
    };

    public static string LetterDone => T("letter.done");

    public static string LetterAlone => T("letter.alone");

    /// <summary>The postmark's word once the change is made.</summary>
    public static string LetterArrived => T("letter.arrived");

    /// <summary>The partner not yet known, as before a Pair Shuffle.</summary>
    public static string LetterUnknown => T("letter.unknown");

    public static string LetterYou => T("letter.you");

    public static string LetterTo(string player) => T("letter.to", ("player", player));

    public static string LetterRoleUnknown => T("letter.roleUnknown");

    /// <summary>The heading printed on a slip that asks before going on.</summary>
    public static string SlipQuery => T("slip.query");

    public static string StampNotify => T("stamp.notify");

    /// <summary>The post office round a postmark's top.</summary>
    public static string PostmarkOffice => T("postmark.office");

    /// <summary>The date round a postmark's foot.</summary>
    /// <remarks>Figures are kept to two places, as a dater's wheels set them.</remarks>
    public static string PostmarkDate(DateTime day) =>
        day.ToString(T("postmark.date"), CultureInfo.InvariantCulture).ToUpperInvariant();

    /// <summary>The time in a postmark's middle, on a twelve-hour dial.</summary>
    public static string PostmarkTime(DateTime time) => T(
        "postmark.time",
        ("hour", time.ToString("hh", CultureInfo.InvariantCulture)),
        ("minute", time.ToString("mm", CultureInfo.InvariantCulture)),
        ("half", T(time.Hour < 12 ? "postmark.am" : "postmark.pm")));

    public static string StampDiscard => T("stamp.discard");

    public static string StampKeep => T("stamp.keep");

    public static string DiscardDraft => T("roster.discardDraft");

    public static string DiscardDraftNote => T("roster.discardDraftNote");

    public static string MenuDraft => T("roster.draft");

    public static string StampAsk => T("stamp.ask");

    public static string StampAsked => T("stamp.asked");

    public static string StampAgree => T("stamp.agree");

    /// <summary>Round the top of the Role Swap's notary seal.</summary>
    public static string SealNotary => T("seal.notary");

    /// <summary>Round the foot of the troop's seals.</summary>
    public static string SealTroop => T("seal.troop");

    /// <summary>Round the top of the Pair Shuffle's stamp.</summary>
    public static string SealOrders => T("seal.orders");

    /// <summary>Round the Recover stamp, as an inspector's mark.</summary>
    public static string SealInspected => T("seal.inspected");

    public static string AboutSwapWaiting => T("roster.aboutSwapWaiting");

    public static string AboutSwapOffered(string partner) => T("roster.aboutSwapOffered", ("partner", partner));

    public static string StampCancel => T("stamp.cancel");

    public static string MenuTooSoon => T("notice.tooSoon");

    public static string ShuffleQueued => T("notice.shuffleQueued");

    public static string PlanUnavailable => T("notice.planUnavailable");

    public static string MenuBusy => T("notice.busy");

    public static string MenuRunRules => T("roster.runRules");

    public static string MenuRulesRead => T("roster.rulesRead");

    /// <summary>A rule's name, as the Roster prints it.</summary>
    public static string RuleName(RunRule rule) => rule switch
    {
        RunRule.AllowOddPlayerCount => RuleAllowOddPlayerCount,
        RunRule.CarryWeight => RuleCarryWeight,
        RunRule.CampfireShuffle => RuleCampfireShuffle,
        RunRule.TimedShuffle => RuleTimedShuffle,
        RunRule.HandOverPlace => RuleHandOverPlace,
        RunRule.HandOverItems => RuleHandOverItems,
        RunRule.RiderPicksFromBackpack => RuleRiderPicksFromBackpack,
        RunRule.FollowRolePreferences => RuleFollowRolePreferences,
        RunRule.AllowSwapRequests => RuleAllowSwapRequests,
        RunRule.LatePairing => RuleLatePairing,
        RunRule.RescueCarry => RuleRescueCarry,
        _ => rule.ToString(),
    };

    public static string RuleLeaderOnly => T("roster.rulesLeaderOnly");

    public static string RuleTurnedOn(string rule) => T("notice.ruleOn", ("rule", rule));

    public static string RuleTurnedOff(string rule) => T("notice.ruleOff", ("rule", rule));

    public static string RuleSetTo(string rule, string value) => T("notice.ruleSetTo", ("rule", rule), ("value", value));

    public static string RulesChanged(string changes) => T("notice.rulesChanged", ("changes", changes));

    public static string Minutes(int minutes) => T("unit.minutes", ("minutes", minutes));

    public static string RuleCampfireShuffle => T("rule.campfireShuffle");

    public static string RuleTimedShuffle => T("rule.timedShuffle");

    public static string RuleTimedShuffleInterval => T("rule.timedShuffleInterval");

    public static string RuleHandOverPlace => T("rule.handOverPlace");

    public static string RuleHandOverItems => T("rule.handOverItems");

    public static string RuleCarryWeight => T("rule.carryWeight");

    public static string RuleRescueCarry => T("rule.rescueCarry");

    public static string RuleRiderPicksFromBackpack => T("rule.riderPicksFromBackpack");

    public static string RuleAllowOddPlayerCount => T("rule.allowOddPlayerCount");

    public static string RuleFollowRolePreferences => T("rule.followRolePreferences");

    public static string RuleAllowSwapRequests => T("rule.allowSwapRequests");

    public static string RuleLatePairing => T("rule.latePairing");

    public static string RulesPairs => T("rules.group.pairs");

    public static string RulesShuffles => T("rules.group.shuffles");

    public static string RulesHandover => T("rules.group.handover");

    public static string RulesCarrying => T("rules.group.carrying");

    public static string AboutFollowRolePreferences => T("rule.followRolePreferences.about");

    public static string AboutAllowSwapRequests => T("rule.allowSwapRequests.about");

    public static string AboutLatePairing => T("rule.latePairing.about");

    public static string AboutRolePreferenceIgnored => T("roster.aboutRolePreferenceIgnored");

    public static string AboutSwapNotAllowed => T("roster.aboutSwapNotAllowed");

    public static string SwapNotAllowed => T("notice.swapNotAllowed");

    public static string AboutRolePreference => T("roster.aboutRolePreference");

    public static string AboutSwapPlaces => T("roster.aboutSwapPlaces");

    public static string AboutNoPartner => T("roster.aboutNoPartner");

    public static string AboutNextShuffle => T("roster.aboutNextShuffle");

    public static string AboutShuffleNow => T("roster.aboutShuffleNow");

    public static string AboutRecover => T("roster.aboutRecover");

    public static string AboutAssign => T("roster.aboutAnnounce");

    public static string MenuBoardHelp => T("roster.boardHelp");

    public static string MenuNewPair => T("roster.newPair");

    public static string MenuDropHere => T("roster.dropHere");

    public static string MenuEmptyPlace => T("roster.emptyPlace");

    public static string MenuAssignReset => T("roster.startOver");

    public static string AboutCampfireShuffle => T("rule.campfireShuffle.about");

    public static string AboutTimedShuffle => T("rule.timedShuffle.about");

    public static string AboutTimedShuffleInterval => T("rule.timedShuffleInterval.about");

    public static string AboutHandOverPlace => T("rule.handOverPlace.about");

    public static string AboutHandOverItems => T("rule.handOverItems.about");

    public static string AboutCarryWeight => T("rule.carryWeight.about");

    public static string AboutRescueCarry => T("rule.rescueCarry.about");

    public static string AboutRiderPicksFromBackpack => T("rule.riderPicksFromBackpack.about");

    public static string AboutAllowOddPlayerCount => T("rule.allowOddPlayerCount.about");

    public static string MenuUnpaired => T("roster.unpaired");

    public static string MenuUnmodded => T("roster.unmodded");

    public static string AboutUnmodded => T("roster.aboutUnmodded");

    public static string UnmoddedVersion(string? version) => version == null
        ? T("roster.noMod")
        : T("roster.modVersion", ("version", version));

    public static string AssignmentCountdown(int seconds) => T("notice.assignmentCountdown", ("seconds", seconds));

    public static string MenuAssign => T("roster.announce");

    public static string MenuAssignUnavailable => T("notice.assignUnavailable");

    public static string SwapPlaces => T("roster.swapPlaces");

    public static string CancelSwap => T("roster.cancelSwap");

    public static string SwapUnavailable => T("notice.swapUnavailable");

    public static string SwapAsked(string partner) => T("notice.swapAsked", ("partner", partner));

    public static string SwapOffered(string partner) => T("notice.swapOffered", ("partner", partner));

    public static string SwapWithdrawn(string partner) => T("notice.swapWithdrawn", ("partner", partner));

    public static string SwapCancelled => T("notice.swapCancelled");

    public static string SwapExpired => T("notice.swapExpired");

    public static string SwapCancelledByShuffle => T("notice.swapCancelledByShuffle");

    public static string RiderHandEmptied => T("notice.riderHandEmptied");

    public static string RiderBackEmptied => T("notice.riderBackEmptied");

    public static string Recovered => T("notice.recovered");

    public static string OddPlayerCount(int pairable, int unmodded) => unmodded > 0
        ? T("notice.oddPlayerCountWithUnmodded", ("pairable", pairable), ("unmodded", unmodded))
        : T("notice.oddPlayerCount", ("pairable", pairable));

    /// <summary>More players who can pair are in the room than the mod pairs at once.</summary>
    public static string Overfull(int seats) => T("notice.overfull", ("seats", seats));

    /// <summary>An Unmodded Player in the room, told to everyone who plays with the mod.</summary>
    public static string UnmoddedPlayer(string player, string? theirs, string? leaders) => theirs == null
        ? T("notice.unmoddedPlayer", ("player", player))
        : T("notice.unmoddedPlayerVersion", ("player", player), ("theirs", theirs), ("leaders", leaders ?? "?"));

    /// <summary>This player rests in a room whose Troop Leader has another version of the mod, or none.</summary>
    public static string Resting(string leader, string? leaders, string mine) => leaders == null
        ? T("notice.resting", ("leader", leader))
        : T("notice.restingVersion", ("leader", leader), ("leaders", leaders), ("mine", mine));
}
