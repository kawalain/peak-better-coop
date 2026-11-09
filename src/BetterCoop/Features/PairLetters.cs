using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Net;
using BetterCoop.Ui;
using BetterCoop.Ui.Notices;
using BetterCoop.Ui.Paper;
using Photon.Pun;

namespace BetterCoop.Features;

/// <summary>
/// Sends the local player the Pair letter when their own Pair or role
/// changes: through an operation's countdown, showing the partner it will
/// give them as far as known beforehand, and when Pairs are first formed.
/// Once the change is made the letter shows the Pair it made and slips away.
/// </summary>
internal static class PairLetters
{
    /// <summary>How long the letter stays once the bodies have changed.</summary>
    private const float AfterChange = 3f;

    /// <summary>How long a letter for Pairs formed without an operation stays.</summary>
    private const float AfterForming = 5f;

    private static readonly LivePortraits Portraits = new();

    private static int followedId;

    private static (int? Partner, bool? Carries)? preview;

    /// <summary>The player's partner, and whether they carried, when the followed operation began.</summary>
    private static (int? Partner, bool? Carries) before;

    private static bool closing;

    /// <summary>Follows an operation that involves the player; false when the letter cannot be shown, so the countdown goes to the log.</summary>
    public static bool Follow(Operation op, int secondsLeft)
    {
        PairLetter? letter = NoticeBoard.Letter;
        if (letter == null)
        {
            return false;
        }

        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if (op.Id != followedId)
        {
            if (op.Done)
            {
                // Done before this player saw it, as on joining: nothing to announce.
                return true;
            }

            followedId = op.Id;
            closing = false;
            IReadOnlyList<Pair>? pairs = op.Kind switch
            {
                OperationKind.PairAssignment => Arrangement.Decode(RoomProtocol.Get(RoomKeys.Assignment), op.Id),
                OperationKind.LatePairing => op.Joining,
                _ => null,
            };
            preview = PairLetterPlan.Preview(op.Kind, me, op.SwapCarrier, op.SwapRider, pairs);
            Pair? had = PairRoster.Pairs.FirstOrDefault(p => p.Contains(me));
            before = (had?.PartnerOf(me), had == null ? null : had.Carrier == me);
            letter.Open(Strings.LetterHeading(op.Kind), Portraits, NameOf, me);
            NoticeBoard.Chime(bad: false);
        }

        if (op.CalledOff)
        {
            // Nothing changed: the Pair stays as it was.
            Pair? stays = PairRoster.Pairs.FirstOrDefault(p => p.Contains(me));
            letter.Show(stays?.PartnerOf(me), stays == null ? null : stays.Carrier == me, known: true, Strings.LetterCalledOff, Strings.LetterStays);
            if (!closing)
            {
                closing = true;
                letter.CloseAfter(AfterChange);
            }

            return true;
        }

        if (op.Done)
        {
            // The bodies have changed: show the Pair as it now stands.
            Pair? now = PairRoster.Pairs.FirstOrDefault(p => p.Contains(me));
            int? partner = now?.PartnerOf(me);
            bool? carries = now == null ? null : now.Carrier == me;
            PairChangeForMe done = PairLetterPlan.ChangeFor(before.Partner, before.Carries, partner, carries, known: true);
            letter.Show(partner, carries, known: true, Strings.LetterArrived, Strings.LetterDoneFor(done));
            if (!closing)
            {
                closing = true;
                letter.CloseAfter(AfterChange);
            }

            return true;
        }

        (int? soon, bool? role) = preview ?? (null, null);
        bool known = !op.IsShuffle;
        string word = secondsLeft > 0 ? secondsLeft.ToString() : Strings.LetterArrived;
        PairChangeForMe change = PairLetterPlan.ChangeFor(before.Partner, before.Carries, soon, role, known && (soon != null || preview != null));
        string line = secondsLeft > 0 ? Strings.LetterSoon(change, secondsLeft) : Strings.LetterDoneFor(change);
        letter.Show(soon, role, known, word, line);
        return true;
    }

    /// <summary>
    /// Shows a Pair the player was put in when Pairs are first formed;
    /// false when the letter cannot be shown, so the news goes elsewhere.
    /// </summary>
    public static bool Formed(Pair pair)
    {
        PairLetter? letter = NoticeBoard.Letter;
        int me = PhotonNetwork.LocalPlayer.ActorNumber;
        if (letter == null || !pair.Contains(me))
        {
            return false;
        }

        if (!letter.Showing)
        {
            letter.Open(Strings.LetterHeading(OperationKind.None), Portraits, NameOf, me);
            NoticeBoard.Chime(bad: false);
            letter.CloseAfter(AfterForming);
        }

        letter.Show(pair.PartnerOf(me), pair.Carrier == me, known: true, Strings.LetterArrived, Strings.LetterDone);
        return true;
    }

    private static string NameOf(int actor) => KnownPlayers.NameOf(actor);
}
