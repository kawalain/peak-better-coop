using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Ui;
using BetterCoop.Ui.Paper;

namespace BetterCoop.Features;

/// <summary>
/// The Run Report on its own paper, opened from the Roster back at the
/// Airport: how the Run ended and how long it took, the Merit Badges, who
/// carried whom in each segment, and everyone's Carry Record, scouts who
/// left greyed. It shows whatever report it is given: the room's, or a
/// made-up one in the Zoo.
/// </summary>
internal sealed class RunReportPage : PaperPage
{
    private Func<RunReport?> source = () => null;

    private Func<string, bool> present = _ => true;

    /// <summary>Shows the report <paramref name="report"/> gives, filled anew at once.</summary>
    public void Show(Func<RunReport?> report, Func<string, bool> isPresent)
    {
        source = report;
        present = isPresent;
        Refill();
    }

    /// <summary>The report shown, by identity: a new report fills the page anew.</summary>
    protected override string Shape() => source() is RunReport report ? report.GetHashCode().ToString() : "none";

    protected override string FormNumber() => Strings.ReportFormTitle;

    protected override string FormNote() => source() is RunReport report ? Strings.FormMembers(report.Record.Scouts.Count) : string.Empty;

    protected override void Fill(PaperList list)
    {
        if (source() is not RunReport report)
        {
            list.Header(() => Strings.MenuRunReport);
            list.Note(() => Strings.ReportGone);
            return;
        }

        CarryRecord record = report.Record;
        list.Header(() => Strings.ReportResult);
        list.Field(() => Strings.ReportEnding, () => Strings.EndingName(report.Ending));
        list.Field(() => Strings.ReportRunTime, () => Strings.Duration(report.Seconds));

        list.Header(() => Strings.MenuMeritBadges);
        IReadOnlyList<MeritBadge> badges = record.Badges();
        if (badges.Count == 0)
        {
            list.Note(() => Strings.NoBadges);
        }
        else
        {
            list.Badges(badges
                .Select(b => new BadgeCard(
                    b.Kind,
                    Strings.BadgeName(b.Kind),
                    string.Join(" & ", b.Ids.Select(id => Name(record, id))),
                    Strings.BadgeAmount(b.Kind, b.Amount)))
                .ToList());
        }

        list.Header(() => Strings.ReportBySegment);
        var segments = record.Segments();
        if (segments.Count == 0)
        {
            list.Note(() => Strings.CarryRecordEmpty);
        }

        foreach ((int _, string title, IReadOnlyList<CarryRecord.Stretch> pairs) in segments)
        {
            string name = SegmentName(title);
            list.Subheading(() => name);
            IReadOnlyList<TableEntry> entries = pairs
                .Select(p => new TableEntry(
                    [$"{Name(record, p.Carrier)} → {Name(record, p.Rider)}", Strings.Duration(p.Seconds)],
                    faint: !present(p.Carrier) || !present(p.Rider)))
                .ToList();
            list.Table(() => [Strings.HeadPair, Strings.HeadTogether], () => entries);
        }

        list.Header(() => Strings.MenuCarryRecord);
        RecordRows.Table(list, () => report.Record, present);
    }

    /// <summary>A scout's name in the record, marked when they have left the room.</summary>
    private string Name(CarryRecord record, string id)
    {
        string name = record.Find(id)?.Name is { Length: > 0 } known ? known : id;
        return present(id) ? name : Strings.Left(name);
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
