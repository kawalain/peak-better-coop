using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Ui;
using BetterCoop.Ui.Paper;

namespace BetterCoop.Features;

/// <summary>
/// The Run Report on its own paper, opened from the Roster: how the Run
/// ended and how long it took, the Merit Badges, who carried whom in each
/// segment, and everyone's Carry Record, scouts who left greyed. While the
/// Run is under way it reads the record so far and follows it as the Troop
/// Leader shares it; once the Run ends, the report written then. It shows
/// whatever room it is given: the game's own, or a made-up one in the Zoo.
/// </summary>
internal sealed class RunReportPage : PaperPage
{
    private ICoopRoom? room;

    private RunReport? live;

    /// <summary>The report the page last worked out, and what it worked out from it.</summary>
    private RunReport? read;

    private IReadOnlyList<MeritBadge> badges = [];

    private IReadOnlyList<(int Segment, string Title, IReadOnlyList<CarryRecord.Stretch> Pairs)> segments = [];

    private IReadOnlyList<BadgeCard> cards = [];

    private readonly Dictionary<int, IReadOnlyList<TableEntry>> segmentEntries = new();

    private string structure = "none";

    /// <summary>Shows the report of <paramref name="shown"/>, filled anew at once.</summary>
    public void Show(ICoopRoom shown)
    {
        room = shown;
        live = null;
        read = null;
        Refill();
    }

    /// <summary>The report to read now: the written one, or the one under way.</summary>
    private RunReport? Current => room != null ? Read(RunReport.Now(room.Report, room.Formed ? room.Record : null, ref live)) : null;

    /// <summary>
    /// What the rows depend on: how the Run ended, which badges there are,
    /// and which segments were climbed. Everything else (who leads, the
    /// times, the Pairs of a segment, the record) follows the record in place.
    /// </summary>
    protected override string Shape()
    {
        _ = Current;
        return structure;
    }

    protected override string FormNumber() => Strings.ReportFormTitle;

    protected override string FormNote() => Current is RunReport report ? Strings.FormMembers(report.Record.Scouts.Count) : string.Empty;

    protected override void Tick() => room?.Tick(UnityEngine.Time.unscaledDeltaTime);

    protected override void Fill(PaperList list)
    {
        if (Current == null)
        {
            list.Header(() => Strings.MenuRunReport);
            list.Note(() => Strings.ReportGone);
            return;
        }

        list.Header(() => Strings.ReportResult);
        list.Note(() => Strings.ReportUnderway, visible: () => Current?.Ending == RunEnding.Underway);
        list.Field(() => Strings.ReportEnding, () => Strings.EndingName(Current?.Ending ?? RunEnding.Underway));
        list.Field(
            () => Strings.ReportRunTime,
            () => Strings.Duration(Current is { Ending: not RunEnding.Underway } report ? report.Seconds : room?.RunSeconds ?? 0));

        list.Header(() => Strings.MenuMeritBadges);
        if (cards.Count == 0)
        {
            list.Note(() => Strings.NoBadges);
        }
        else
        {
            list.Badges(() =>
            {
                _ = Current;
                return cards;
            });
        }

        list.Header(() => Strings.ReportBySegment);
        if (segments.Count == 0)
        {
            list.Note(() => Strings.CarryRecordEmpty);
        }

        foreach ((int segment, string title, _) in segments)
        {
            string name = SegmentName(title);
            int which = segment;
            list.Subheading(() => name);
            list.Table(
                () => [Strings.HeadPair, Strings.HeadTogether],
                () =>
                {
                    _ = Current;
                    return segmentEntries.TryGetValue(which, out IReadOnlyList<TableEntry>? entries) ? entries : [];
                });
        }

        list.Header(() => Strings.MenuCarryRecord);
        RecordRows.Table(list, () => Current?.Record, Present);
    }

    /// <summary>Works out what the page shows from a report, once for each report.</summary>
    private RunReport? Read(RunReport? report)
    {
        if (ReferenceEquals(report, read))
        {
            return report;
        }

        read = report;
        segmentEntries.Clear();
        if (report == null)
        {
            badges = [];
            segments = [];
            cards = [];
            structure = "none";
            return null;
        }

        CarryRecord record = report.Record;
        badges = record.Badges();
        segments = record.Segments();
        cards = badges
            .Select(b => new BadgeCard(
                b.Kind,
                Strings.BadgeName(b.Kind),
                string.Join(" & ", b.Ids.Select(id => Name(record, id))),
                Strings.BadgeAmount(b.Kind, b.Amount)))
            .ToList();
        foreach ((int segment, _, IReadOnlyList<CarryRecord.Stretch> pairs) in segments)
        {
            segmentEntries[segment] = pairs
                .Select(p => new TableEntry(
                    [$"{Name(record, p.Carrier)} → {Name(record, p.Rider)}", Strings.Duration(p.Seconds)],
                    faint: !Present(p.Carrier) || !Present(p.Rider)))
                .ToList();
        }

        structure = $"{report.Ending}|{string.Join(",", badges.Select(b => b.Kind))}|{string.Join(",", segments.Select(s => s.Segment))}";
        return report;
    }

    private bool Present(string id) => room?.Present(id) ?? true;

    /// <summary>A scout's name in the record, marked when they have left the room.</summary>
    private string Name(CarryRecord record, string id)
    {
        string name = record.Find(id)?.Name is { Length: > 0 } known ? known : id;
        return Present(id) ? name : Strings.Left(name);
    }

    /// <summary>A segment's name in the game's own words, or its key when the game has none.</summary>
    private static string SegmentName(string title)
    {
        try
        {
            string text = LocalizedText.GetText(title, printDebug: false);
            return string.IsNullOrWhiteSpace(text) ? title : text;
        }
        catch (Exception)
        {
            return title;
        }
    }
}
