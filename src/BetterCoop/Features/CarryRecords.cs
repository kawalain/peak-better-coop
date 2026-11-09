using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using PhotonHashtable = ExitGames.Client.Photon.Hashtable;

namespace BetterCoop.Features;

/// <summary>
/// The Carry Record and the Run Report. The Troop Leader samples the Pairs
/// on the island and shares the record as a room property; when the Run
/// ends, it writes the Run Report, which lasts until the next Run departs.
/// Every client reads both from the room.
/// </summary>
[HarmonyPatch]
[HarmonyWrapSafe]
internal static class CarryRecords
{
    /// <summary>How often the Troop Leader shares the record while it counts.</summary>
    private const float ShareSeconds = 15f;

    /// <summary>The longest gap between samples that still counts whole, as after a hitch.</summary>
    private const float MostPerSample = 2f;

    /// <summary>The game's metres per world unit, as its own end screen counts height.</summary>
    private const double MetresPerUnit = 1.6;

    private static CarryRecord? hostCopy;

    /// <summary>The game's Run clock at the last sample, so the record counts the time the Run's own clock counts.</summary>
    private static double lastSample;

    private static double RunClock => RunManager.Instance != null ? RunManager.Instance.TimeSinceRunStarted : 0;

    private static float nextShare;

    private static string sharedPairs = string.Empty;

    /// <summary>Whether the Run has ended and its report is written, so nothing more counts.</summary>
    private static bool reported;

    private static object? readRecordFrom;

    private static CarryRecord? readRecord;

    private static object? readReportFrom;

    private static RunReport? readReport;

    private static bool reportUntold;

    /// <summary>The room's Carry Record, as every client reads it.</summary>
    public static CarryRecord? Shared
    {
        get
        {
            object? value = RoomProtocol.Get(RoomKeys.Record);
            if (!ReferenceEquals(value, readRecordFrom))
            {
                readRecordFrom = value;
                readRecord = CarryRecord.Decode(value);
            }

            return readRecord;
        }
    }

    /// <summary>The room's Run Report of the last Run, as every client reads it.</summary>
    public static RunReport? Report
    {
        get
        {
            object? value = RoomProtocol.Get(RoomKeys.Report);
            if (!ReferenceEquals(value, readReportFrom))
            {
                readReportFrom = value;
                readReport = RunReport.Decode(value);
            }

            return readReport;
        }
    }

    /// <summary>As the Troop Leader forming the Pairs of a new Run: a fresh record, and the last Run's report gone.</summary>
    public static void Begin(PhotonHashtable properties)
    {
        hostCopy = new CarryRecord();
        reported = false;
        lastSample = RunClock;
        nextShare = Time.unscaledTime + ShareSeconds;
        properties[RoomKeys.Record] = hostCopy.Encode();
        properties[RoomKeys.Report] = null;
    }

    /// <summary>As the Troop Leader on the island, every half second: counts the Pairs since the last sample.</summary>
    public static void Sample(PairingResult current)
    {
        float now = Time.unscaledTime;
        double clock = RunClock;
        // The clock stands still before the Run starts and while it is paused, and goes back when it is synced.
        float seconds = (float)Math.Min(clock - lastSample, MostPerSample);
        lastSample = clock;
        if (reported || seconds <= 0f)
        {
            return;
        }

        // A new Troop Leader carries on from the room's record.
        CarryRecord record = hostCopy ??= CarryRecord.Decode(Room(RoomKeys.Record)) ?? new CarryRecord();
        var samples = new List<PairSample>();
        foreach (Pair pair in current.Pairs)
        {
            if (!PlayerHandler.TryGetCharacter(pair.Carrier, out Character carrier) || carrier == null)
            {
                continue;
            }

            samples.Add(new PairSample(
                IdOf(pair.Carrier),
                NameOf(pair.Carrier),
                IdOf(pair.Rider),
                NameOf(pair.Rider),
                !carrier.data.dead,
                carrier.HipPos().y * MetresPerUnit));
        }

        Segment segment = MapHandler.CurrentSegmentNumber;
        record.Count(samples, seconds, (int)segment, TitleOf(segment));

        string pairs = string.Join(",", current.Pairs.Select(p => $"{p.Carrier}>{p.Rider}"));
        if (now >= nextShare || pairs != sharedPairs)
        {
            nextShare = now + ShareSeconds;
            sharedPairs = pairs;
            RoomWrites.Set(new PhotonHashtable { [RoomKeys.Record] = record.Encode() });
        }
    }

    /// <summary>As the Troop Leader back at the Airport: a Run that ended without the game ending it still gets its report.</summary>
    public static void EndIfUnreported(PhotonHashtable properties)
    {
        if (!reported && hostCopy is { IsEmpty: false })
        {
            Write(RunEnding.Returned, properties);
        }

        hostCopy = null;
        reported = false;
    }

    /// <summary>
    /// The game ends the Run on every client; the Troop Leader writes its
    /// report first, by the game's own win check, so a mod that shortens or
    /// skips what follows (the cutscene, the end screen, the credits) changes
    /// nothing here.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Character), nameof(Character.RPCEndGame))]
    private static void EndRun()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || reported || PairRoster.Read() is not PairingResult current)
        {
            return;
        }

        Sample(current);
        bool summit = Character.AllCharacters.Any(c => c != null && Character.CheckWinCondition(c));
        var properties = new PhotonHashtable();
        Write(summit ? RunEnding.Summit : RunEnding.Lost, properties);
        RoomWrites.Set(properties);
    }

    private static void Write(RunEnding ending, PhotonHashtable properties)
    {
        CarryRecord record = hostCopy ?? CarryRecord.Decode(Room(RoomKeys.Record)) ?? new CarryRecord();
        double seconds = RunManager.Instance != null ? RunManager.Instance.TimeSinceRunStarted : 0;
        properties[RoomKeys.Record] = record.Encode();
        properties[RoomKeys.Report] = new RunReport(ending, seconds, record).Encode();
        reported = true;
        Plugin.Verbose($"Wrote the Run Report: {ending}, {seconds:F0} s, {record.Scouts.Count} scout(s).");
    }

    /// <summary>On every client: a new report waits to be told of until the party is back at the Airport.</summary>
    public static void OnRoomPropertiesChanged(PhotonHashtable changed)
    {
        if (RoomProtocol.Readable && changed.ContainsKey(RoomKeys.Report))
        {
            reportUntold = changed[RoomKeys.Report] != null;
        }
    }

    /// <summary>On every client, every frame: tells of the report once back at the Airport.</summary>
    public static void Tick()
    {
        if (!reportUntold || !PhotonNetwork.InRoom || GameHandler.IsOnIsland || Character.localCharacter == null || Report == null)
        {
            return;
        }

        reportUntold = false;
        CoopNotice.Show(NoticeKind.Telegram, Strings.ReportArrived);
    }

    public static void OnLeftRoom()
    {
        hostCopy = null;
        reported = false;
        reportUntold = false;
    }

    /// <summary>The game's text key for a segment's name, by the biome it holds this Run.</summary>
    internal static string TitleOf(Segment segment)
    {
        Biome.BiomeType biome = MapHandler.Instance != null ? MapHandler.Instance.GetCurrentBiome() : Biome.BiomeType.Shore;
        return segment switch
        {
            Segment.Beach => "SHORE",
            Segment.Tropics => biome == Biome.BiomeType.Roots ? "ROOTS" : "TROPICS",
            Segment.Alpine => biome == Biome.BiomeType.Mesa ? "MESA" : "ALPINE",
            Segment.Caldera => "CALDERA",
            Segment.TheKiln => "THE KILN",
            Segment.Peak => "PEAK",
            _ => "NADIR",
        };
    }

    private static object? Room(string key) => RoomProtocol.Get(key);

    private static string IdOf(int actor) => KnownPlayers.UserIdOf(actor) ?? $"#{actor}";

    private static string NameOf(int actor) => KnownPlayers.NameOf(actor);
}
