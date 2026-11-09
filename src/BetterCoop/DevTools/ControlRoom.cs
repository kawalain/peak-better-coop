using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Features;
using BetterCoop.Net;
using Newtonsoft.Json.Linq;
using Photon.Pun;

namespace BetterCoop.DevTools;

/// <summary>
/// The mod's side of the Control Line: its state as the Roster reads it, and
/// its orders through the Roster's own <see cref="LiveRoom"/>, so a tool can do
/// only what a player could, under the same rules (most orders are the Troop
/// Leader's alone).
/// </summary>
internal static partial class ControlCommands
{
    private static readonly LiveRoom Live = new();

    static partial void AddModState(JObject state)
    {
        PairingResult? published = PairRoster.Read();
        Operation? change = PairShuffle.Current;
        CarryRecord? record = Live.Record;
        RunReport? report = Live.Report;
        state["mod"] = new JObject
        {
            ["troopLeader"] = Live.TroopLeader,
            ["planning"] = Live.Planning,
            ["formed"] = Live.Formed,
            ["pairs"] = PairsJson(Live.Pairs),
            ["unpaired"] = published == null ? null : new JArray(published.Unpaired),
            ["plan"] = PairsJson(Live.Plan),
            ["myRole"] = PairRoster.RoleOf(Character.localCharacter).ToString(),
            ["preference"] = Live.Preference.ToString(),
            ["swapPending"] = Live.SwapPending,
            ["pairChange"] = change == null ? null : new JObject
            {
                ["kind"] = change.Kind.ToString(),
                ["at"] = change.At,
                ["now"] = PhotonNetwork.Time,
                ["done"] = change.Done,
                ["calledOff"] = change.CalledOff,
            },
            ["secondsToTimedShuffle"] = Live.SecondsToTimedShuffle,
            ["rules"] = new JObject(Enum.GetValues(typeof(RunRule)).Cast<RunRule>()
                .Select(r => new JProperty(r.ToString(), Live.Rule(r)))),
            ["shuffleMinutes"] = Live.ShuffleMinutes,
            ["runSeconds"] = Math.Round(Live.RunSeconds, 1),
            ["record"] = record == null ? null : new JArray(record.Ranked().Select(s => new JObject
            {
                ["id"] = s.Id,
                ["name"] = s.Name,
                ["carried"] = Math.Round(s.Carried, 1),
                ["ridden"] = Math.Round(s.Ridden, 1),
                ["climbed"] = Math.Round(s.Climbed, 1),
            })),
            ["report"] = report == null ? null : new JObject
            {
                ["ending"] = report.Ending.ToString(),
                ["seconds"] = Math.Round(report.Seconds, 1),
                ["badges"] = new JArray(report.Record.Badges().Select(b => new JObject
                {
                    ["kind"] = b.Kind.ToString(),
                    ["ids"] = new JArray(b.Ids),
                    ["amount"] = Math.Round(b.Amount, 1),
                })),
            },
        };
    }

    private static JToken PairsJson(IReadOnlyList<Pair> pairs) =>
        new JArray(pairs.Select(p => new JArray(p.Carrier, p.Rider)));

    private static IReadOnlyList<Pair> PairsFrom(JToken? given) =>
        (given as JArray ?? throw new ArgumentException("needs pairs: [[carrier, rider], ...] by actor number"))
            .Select(p => new Pair((int)p[0]!, (int)p[1]!))
            .ToList();

    private static JToken Order(JObject args)
    {
        string what = (string?)args["what"] ?? string.Empty;
        switch (what)
        {
            case "shuffle":
                return Live.Shuffle();
            case "callOff":
                return Live.CallOffShuffle();
            case "assign":
            {
                IReadOnlyList<Pair> pairs = PairsFrom(args["pairs"]);
                return Live.CanArrange(pairs) ? Live.Arrange(pairs) : throw new InvalidOperationException("the Roster would not take these Pairs now");
            }

            case "plan":
                return Live.SetPlan(PairsFrom(args["pairs"]));
            case "drawPlan":
                return PairsJson(Live.DrawPlan());
            case "recover":
                Live.Recover();
                return true;
            case "rule":
            {
                RunRule rule = (RunRule)Enum.Parse(typeof(RunRule), (string?)args["rule"] ?? string.Empty, ignoreCase: true);
                Live.SetRule(rule, (bool?)args["on"] ?? throw new ArgumentException("needs on"));
                return Live.Rule(rule);
            }

            case "shuffleMinutes":
                Live.ShuffleMinutes = (int?)args["minutes"] ?? throw new ArgumentException("needs minutes");
                return Live.ShuffleMinutes;
            case "preference":
                Live.Preference = (RolePreference)Enum.Parse(typeof(RolePreference), (string?)args["role"] ?? string.Empty, ignoreCase: true);
                return Live.Preference.ToString();
            case "swap":
                Live.ToggleSwap();
                return Live.SwapPending;
            default:
                throw new ArgumentException(
                    "what: shuffle, callOff, assign {pairs}, plan {pairs}, drawPlan, recover, rule {rule, on}, "
                    + "shuffleMinutes {minutes}, preference {role}, swap");
        }
    }
}
