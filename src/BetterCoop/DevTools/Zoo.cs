using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Features;
using BetterCoop.Ui;
using BetterCoop.Ui.Notices;
using BetterCoop.Ui.Paper;
using UnityEngine;
using UnityEngine.UI;
using Zorro.Core;
using Zorro.UI;

namespace BetterCoop.DevTools;

/// <summary>
/// The Zoo, in debug builds only: a pause menu page that shows every paper
/// component in its states, and opens the Roster on a made-up room of any
/// size, led by the local player or not, so its Pair changes can be tried
/// without other players. Nothing here reaches the config, the room, or the
/// network.
/// </summary>
internal sealed class ZooPage : PaperPage
{
    private static readonly string[] Choices = ["A", "B", "C"];

    private static readonly int[] Counts = Enumerable.Range(1, 12).ToArray();

    private static readonly int[] UnmoddedCounts = [0, 1, 2];

    private RosterPage example = null!;

    private RunReportPage reportExample = null!;

    private bool reported = true;

    private static readonly int[] ManyChoices = [1, 2, 3, 5, 10, 15, 20, 30];

    private bool check = true;

    private int minutes = 10;

    private bool swapped;

    /// <summary>How struck the specimen stamps show, or -1 to follow their presses.</summary>
    private float struck = -1f;

    private static readonly float[] StruckChoices = [-1f, 0f, 0.5f, 1f];

    private string choice = Choices[0];

    private int presses;

    private static readonly int[] TableCounts = Enumerable.Range(0, 13).ToArray();

    private int tableLines = 5;

    private int players = 8;

    private int unmodded;

    private bool resting;

    private bool leader = true;

    private bool formed = true;

    private int seed = 1;

    private ZooLetter.Scene letterScene = ZooLetter.Scenes[0];

    private RunEnding ending = RunEnding.Summit;

    private static readonly RunEnding[] Endings = [RunEnding.Summit, RunEnding.Lost, RunEnding.Returned];

    public static ZooPage Build(UIPageHandler handler, PauseMenuControlsPage controls, Button template, UIPage parent)
    {
        ZooPage zoo = Create<ZooPage>(handler, controls, template, parent, () => "Zoo");
        zoo.example = Create<RosterPage>(handler, controls, template, zoo, () => $"{Strings.Roster} (Zoo)");
        zoo.reportExample = Create<RunReportPage>(handler, controls, template, zoo, () => $"{Strings.MenuRunReport} (Zoo)");
        return zoo;
    }

    private void Specimen(PaperList list, string label, string word, StampFace face) =>
        list.Stamp(() => label, () => word, () => presses++, face: face, pose: () => struck < 0f ? null : (float?)struck);

    protected override string Shape() => "zoo";

    protected override string FormNumber() => T("Zoo 서식 제0호 · 견본", "Zoo Form No. 0 · Specimen");

    protected override string FormNote() => T($"찍은 횟수 {presses}", $"Stamped {presses}");

    protected override void Fill(PaperList list)
    {
        list.Header(() => T("서식 컴포넌트", "Form components"));
        list.Note(() => T(
            "작은 글씨의 안내문입니다. 한 줄을 넘으면 다음 줄로 넘어가며, 항목은 그만큼 높아집니다.",
            "A note in small print. Longer than a line, it wraps and its entry grows to fit."));
        list.Field(() => T("항목", "Field"), () => T("펜으로 쓴 답", "Answer in pen"));
        list.Field(
            () => T("설명이 붙은 항목", "Field with small print"),
            () => T("값", "Value"),
            about: () => T("라벨 아래에 작은 글씨로 설명이 붙습니다.", "Small print sits under the label."));
        list.Check(
            () => T("체크 박스", "Check box"),
            () => check,
            on => check = on,
            () => T("줄 어디를 눌러도 체크하거나 지웁니다. 오른쪽 클릭도 같습니다.", "Pressing anywhere on the line ticks or clears it; so does a right click."));
        list.Choice(
            () => T("동그라미 고르기", "Circle one"),
            () => choice,
            value => choice = value,
            () => Choices,
            value => value,
            about: () => T(
                "답을 누르면 펜으로 동그라미를 칩니다. 오른쪽 클릭이나 ←는 앞의 답, →는 다음 답입니다.",
                "Pressing an answer rings it. A right click or left goes back, right goes forward."));
        list.Choice(
            () => T("넘겨 고르기", "Step through"),
            () => minutes,
            value => minutes = value,
            () => ManyChoices,
            value => T($"{value}분", $"{value} min"),
            about: () => T(
                "답이 많으면 하나만 보이고 화살표로 넘깁니다. 누르면 다음, 오른쪽 클릭이나 ←는 앞의 답입니다.",
                "Many answers show one at a time with arrows. A press steps forward; a right click or left steps back."));
        list.Stamp(
            () => T("도장", "Stamp"),
            () => T("확정", "Approved"),
            () => presses++,
            about: () => T("누르면 쿵 하고 찍힙니다.", "Pressed, it comes down with a thump."));
        list.Stamp(
            () => T("쓸 수 없는 도장", "Unavailable stamp"),
            () => T("반려", "Denied"),
            () => CoopNotice.Info(T("Zoo: 지금은 쓸 수 없어 이유를 알립니다.", "Zoo: unavailable now, so this says why.")),
            available: () => false,
            about: () => T("흐리게 보이지만 누르면 이유를 알려 줍니다.", "Faint, but pressing it says why."));
        list.Stamp(
            () => T("되묻는 도장", "Stamp that asks first"),
            () => T("섞기", "Shuffle"),
            () => presses++,
            about: () => T(
                "처음 누르면 \"정말로?\"로 바뀌고, 3초 안에 한 번 더 눌러야 찍힙니다. 다른 곳을 누르거나 선택을 옮기면 돌아옵니다.",
                "The first press turns it to \"Really?\"; a second press within three seconds stamps it. Using or selecting anything else puts it back."),
            confirm: true);
        list.Stamp(
            () => T("우편 소인", "Postmark"),
            () => T("통보", "Notified"),
            () => presses++,
            about: () => T("옛 우체국 소인처럼 찍히는 도장입니다. 도장의 얼굴만 바꿔 다른 도장을 만들 수 있습니다.", "A stamp that lands as an old post office's postmark; a stamp's face is all that makes it another kind."),
            face: new PostmarkFace(() => T("ZOO 우체국", "ZOO P.O."), () => Strings.PostmarkDate(System.DateTime.Now)));
        list.Stamp(
            () => T("값과 도장", "Value and stamp"),
            () => T("교대", "Swap"),
            () => swapped = !swapped,
            value: () => swapped ? "Birch ⇄ Alder" : "Alder ⇄ Birch");
        list.Stamp(() => T("종이 다시 올리기", "Raise the paper again"), () => T("재생", "Play"), Rise);
        list.Stamp(
            () => T("구겨서 버리기", "Crumple and throw away"),
            () => T("재생", "Play"),
            () => ThrowAway(Rise),
            about: () => T("초안을 버릴 때처럼 종이를 구겨 던지고, 새 종이가 올라옵니다.", "Crumples the paper and throws it, as when a draft is thrown away; a fresh sheet rises."));
        list.Stamp(
            () => T("손으로 가져가기", "Handed in"),
            () => T("재생", "Play"),
            () => HandIn(() => presses++),
            about: () => T("짝 명단을 통보할 때처럼 스카우트 지도자의 손이 종이를 가져갑니다.", "The Scoutmaster's hand takes the paper, as when the Pairs are announced."));
        list.Choice(
            () => T("표의 줄 수", "Table lines"),
            () => tableLines,
            count => tableLines = count,
            () => TableCounts,
            count => count.ToString(),
            about: () => T("아래 표의 줄 수입니다. 줄은 바뀌는 대로 생기고 사라집니다.", "Lines in the table below; they come and go as the count changes."));
        list.Table(
            () => [T("대원", "Scout"), T("업은 시간", "Carried"), T("오른 높이", "Climbed")],
            () => Enumerable.Range(1, tableLines)
                .Select(i => new TableEntry(
                    [i % 4 == 0 ? Strings.Left($"Scout {i}") : i == 1 ? "A scout with a very long name indeed, to see it cut" : $"Scout {i}", Strings.Duration(i * 437), Strings.Metres(i * 61.5)],
                    faint: i % 4 == 0))
                .ToList(),
            empty: () => T("줄이 없으면 이렇게 안내합니다.", "With no lines, the table says so."));
        list.Badges(() =>
        [
            new BadgeCard(MeritBadgeKind.SteadyBack, Strings.BadgeName(MeritBadgeKind.SteadyBack), "Alder", Strings.BadgeAmount(MeritBadgeKind.SteadyBack, 1934)),
            new BadgeCard(MeritBadgeKind.ComfyPassenger, Strings.BadgeName(MeritBadgeKind.ComfyPassenger), Strings.Left("Quill"), Strings.BadgeAmount(MeritBadgeKind.ComfyPassenger, 1702)),
            new BadgeCard(MeritBadgeKind.RescueRanger, Strings.BadgeName(MeritBadgeKind.RescueRanger), "Elm", Strings.BadgeAmount(MeritBadgeKind.RescueRanger, 95)),
            new BadgeCard(MeritBadgeKind.BestBuddies, Strings.BadgeName(MeritBadgeKind.BestBuddies), "Birch & Dahlia", Strings.BadgeAmount(MeritBadgeKind.BestBuddies, 2410)),
        ]);
        list.Signature(() => T("서명", "Signed"), () => "Alder");
        Makers.Add(list);

        list.Header(() => T("도장 견본", "Stamp specimens"));
        list.Note(() => T(
            "20세기 중반 미국 사무실과 우체국의 손도장 모양입니다. 누르면 잉크로 채워졌다가, 잉크가 얼룩덜룩 마르듯 조각조각 돌아옵니다.",
            "Hand stamps after mid-century American offices and post offices. Pressed, each strikes solid and dries back in patches, as ink soaks in."));
        list.Choice(
            () => T("도장 상태", "Stamps show"),
            () => struck,
            on => struck = on,
            () => StruckChoices,
            on => on < 0f ? T("누르는 대로", "As pressed") : on <= 0f ? T("찍기 전", "Before striking") : on < 1f ? T("마르는 중", "Drying") : T("찍은 뒤", "Struck"));
        Specimen(list, T("1. 사무용 고무 도장", "1. Office rubber stamp"), T("취소", "Void"), new RubberFace());
        Specimen(list, T("2. 원형 일부인", "2. Circular date stamp"), T("통보", "Notify"),
            new SealFace(StampShape.DateRing, FormDrawing.StampBlue, () => "BETTERCOOP P.O.", () => Strings.PostmarkDate(DateTime.Now), seed: 6));
        Specimen(list, T("3. 이중 원 일부인", "3. Double-ring date stamp"), T("섞기", "Shuffle"),
            new SealFace(StampShape.DoubleRing, FormDrawing.PostmarkInk, () => "BETTERCOOP TROOP", () => "★ ORDERS ★", seed: 7));
        Specimen(list, T("4. 듀플렉스 + 킬러 바", "4. Duplex with killer bars"), T("취소", "Cancel"),
            new SealFace(StampShape.Duplex, FormDrawing.PostmarkInk, () => "BETTERCOOP P.O.", () => Strings.PostmarkDate(DateTime.Now), seed: 8));
        Specimen(list, T("5. 뮤트 오벌", "5. Mute oval"), T("열람", "Read"),
            new SealFace(StampShape.MuteOval, FormDrawing.StampViolet, seed: 9));
        Specimen(list, T("6. 타깃", "6. Target"), T("섞기", "Shuffle"),
            new SealFace(StampShape.Target, FormDrawing.PostmarkInk, seed: 10));
        Specimen(list, T("7. 팬시 소인 (별)", "7. Fancy cancel (star)"), T("요청", "Ask"),
            new SealFace(StampShape.Star, FormDrawing.StampBlue, seed: 11));
        Specimen(list, T("8. USDA 검사인", "8. Inspection mark"), T("복구", "Restore"),
            new SealFace(StampShape.Inspection, FormDrawing.StampViolet, () => "U.S. INSPECTED", () => "AND PASSED", seed: 12));
        Specimen(list, T("9. 공증 인장", "9. Notary seal"), T("요청", "Ask"),
            new SealFace(StampShape.Notary, FormDrawing.StampBlue, () => "TROOP NOTARY", () => "BETTERCOOP", seed: 14));
        Specimen(list, T("10. 우편 소인 (지금 통보)", "10. Postmark (Notify now)"), T("통보", "Notify"),
            new PostmarkFace(() => T("ZOO 우체국", "ZOO P.O."), () => Strings.PostmarkDate(DateTime.Now)));

        list.Header(() => T("알림", "Notices"));
        list.Note(() => T(
            "알림은 종류마다 다른 종이에 실려 왼쪽에서 들어옵니다. 종이로 보일 수 없을 때만 게임의 접속 기록에 씁니다.",
            "Each kind of notice comes in from the left on its own paper. Only when the paper cannot be shown does it go to the game's join log."));
        list.Stamp(
            () => T("전보", "Telegram"),
            () => T("재생", "Play"),
            () => CoopNotice.Info(T("코옵: 분대장이 30초 뒤 짝을 섞습니다.", "Coop: the Troop Leader shuffles Pairs in 30 seconds.")));
        list.Stamp(
            () => T("메모", "Memo"),
            () => T("재생", "Play"),
            () => CoopNotice.Show(NoticeKind.Memo, Strings.SwapOffered("Birch")));
        list.Stamp(
            () => T("꼬리표", "Baggage tag"),
            () => T("재생", "Play"),
            () => CoopNotice.Show(NoticeKind.Tag, Strings.RiderBackEmptied));
        list.Stamp(
            () => T("주의 통지", "Caution"),
            () => T("재생", "Play"),
            () => CoopNotice.Warning(Strings.UnmoddedPlayer("Cedar", null, null)));
        list.Choice(
            () => T("짝 편지의 경우", "Pair letter case"),
            () => letterScene,
            scene => letterScene = scene,
            () => ZooLetter.Scenes,
            scene => scene.Label(),
            about: () => T(
                "편지는 받는 사람에게 무엇이 바뀌는지 말합니다. 아래 줄은 실제 편지와 같은 규칙으로 정해집니다.",
                "The letter says what changes for its reader; its last line comes from the same rules as a real letter's."));
        list.Stamp(
            () => T("짝 편지", "Pair letter"),
            () => T("재생", "Play"),
            () => ZooLetter.Play(letterScene, seed));
        list.Stamp(
            () => T("메모: 자리 바꾸기 요청함", "Memo: swap asked"),
            () => T("재생", "Play"),
            () => CoopNotice.Show(NoticeKind.Memo, Strings.SwapAsked("Birch")));
        list.Stamp(
            () => T("메모: 자리 바꾸기 요청 받음", "Memo: swap offered"),
            () => T("재생", "Play"),
            () => CoopNotice.Show(NoticeKind.Memo, Strings.SwapOffered("Birch")));
        list.Stamp(
            () => T("메모: 자리 바꾸기 요청 취소됨", "Memo: swap withdrawn"),
            () => T("재생", "Play"),
            () => CoopNotice.Show(NoticeKind.Memo, Strings.SwapWithdrawn("Birch")));
        list.Stamp(
            () => T("전보: 자리 바꾸기 만료", "Telegram: swap expired"),
            () => T("재생", "Play"),
            () => CoopNotice.Info(Strings.SwapExpired));
        list.Stamp(
            () => T("게임 기록으로 보내기", "Send to the game's log"),
            () => T("재생", "Play"),
            () => CoopNotice.InGameLog(T("코옵: 종이로 보일 수 없을 때는 이렇게 보입니다.", "Coop: this is how a notice looks when the paper cannot be shown."), warning: false));

        list.Header(() => T("편성표 예제", "Roster examples"));
        list.Choice(
            () => T("인원", "Players"),
            () => players,
            count => players = count,
            () => Counts,
            count => T($"{count}명", $"{count}"),
            about: () => T("가짜 방의 인원입니다. 첫 사람이 나입니다.", "Players in the made-up room; the first is you."));
        list.Choice(
            () => T("모드 없는 대원", "Unmodded Players"),
            () => unmodded,
            count => unmodded = count,
            () => UnmoddedCounts,
            count => T($"{count}명", $"{count}"),
            about: () => T("인원과 별개로 방에 있는 모드 없는 대원입니다. 둘째마다 옛 버전입니다.", "Unmodded Players in the room besides; every other one has an old version."));
        list.Check(() => T("내가 분대장", "You lead"), () => leader, on => leader = on);
        list.Check(
            () => T("이 방에서 쉼", "Resting here"),
            () => resting,
            on => resting = on,
            () => T("분대장의 모드가 내 것과 맞지 않아 이 방에서 모드를 쉬는 편성표입니다. 내가 분대장이면 쉬지 않습니다.", "The Roster of a player whose Troop Leader's mod does not work with theirs. Leading, you never rest."));
        list.Check(() => T("짝 편성 끝남", "Pairs formed"), () => formed, on => formed = on);
        list.Check(
            () => T("지난 원정 보고서", "Last Run's report"),
            () => reported,
            on => reported = on,
            () => T("공항(짝 편성 전)의 편성표에 지난 원정의 보고서가 있습니다. 결말은 열 때마다 바뀝니다.", "The Roster at the Airport (Pairs not formed) holds the last Run's report; its ending changes each time it opens."));
        list.Stamp(
            () => T("예제 열기", "Open the example"),
            () => T("열기", "Open"),
            OpenExample,
            about: () => T(
                "실제 규칙으로 짝을 섞고 지정하는 가짜 방에서 편성표를 엽니다. 게임, 설정, 네트워크에는 닿지 않습니다.",
                "Opens the Roster on a made-up room that shuffles and assigns Pairs by the real rules. It never touches the game, the config, or the network."));

        list.Choice(
            () => T("지난 원정의 결말", "Last Run's ending"),
            () => ending,
            value => ending = value,
            () => Endings,
            value => value switch
            {
                RunEnding.Summit => T("정상 도착", "Summit"),
                RunEnding.Lost => T("전멸", "Lost"),
                _ => T("공항 귀환", "Returned"),
            },
            about: () => T("공항의 편성표와 보고서 예제가 보일 지난 원정의 결말입니다.", "How the last Run ended, for the Roster at the Airport and the report example."));
        list.Stamp(
            () => T("보고서 예제 열기", "Open the report example"),
            () => T("열기", "Open"),
            OpenReport,
            about: () => T(
                "지금 인원으로 가짜 원정의 보고서를 엽니다. 짝 편성이 끝났으면 진행 중인 원정의 실시간 보고서를, 아니면 산 전체를 오른 지난 원정의 보고서를 엽니다. 떠난 대원 한 명이 흐리게 적힙니다.",
                "Opens the report of a made-up Run with this many players: live for a Run under way when the Pairs are formed, else the last Run's up the whole mountain. One scout who left is greyed."));

        list.Header(() => T("긴 목록", "Long list"));
        for (int i = 1; i <= 20; i++)
        {
            int line = i;
            list.Field(() => T($"{line}번째 줄", $"Line {line}"), () => line.ToString());
        }
    }

    private static string T(string korean, string english) => Strings.Korean ? korean : english;

    private void OpenReport()
    {
        reportExample.Show(new FakeRoom(players, leader, formed, seed, reported: true, ending: ending));
        seed++;
        reportExample.Open();
    }

    private void OpenExample()
    {
        example.Show(new FakeRoom(players, leader, formed, seed, reported, ending, unmodded, resting), new ZooPortraits(seed));
        seed++;
        example.Open();
    }
}
/// <summary>
/// Plays the Pair letter in the Zoo on made-up scouts: a three-second
/// countdown, the change, and the letter slipping away. It runs on the
/// letter itself, so it finishes after the pause menu closes.
/// </summary>
internal sealed class ZooLetter : MonoBehaviour
{
    private const float Countdown = 3f;

    /// <summary>
    /// A Pair letter's case for its reader, scout 1: their partner and role
    /// before, as known during the countdown, and after.
    /// </summary>
    internal sealed class Scene
    {
        public Scene(OperationKind kind, string korean, string english, int? before, bool? carried, int? soon, bool? soonCarries, bool known, int? after, bool? carries)
        {
            Kind = kind;
            this.korean = korean;
            this.english = english;
            Before = before;
            Carried = carried;
            Soon = soon;
            SoonCarries = soonCarries;
            Known = known;
            After = after;
            Carries = carries;
        }

        private readonly string korean;

        private readonly string english;

        public OperationKind Kind { get; }

        public int? Before { get; }

        public bool? Carried { get; }

        public int? Soon { get; }

        public bool? SoonCarries { get; }

        public bool Known { get; }

        public int? After { get; }

        public bool? Carries { get; }

        public string Label() => Strings.Korean ? korean : english;
    }

    public static readonly Scene[] Scenes =
    [
        new(OperationKind.TimedShuffle, "짝 섞기: 새 짝 (모름)", "Shuffle: new partner (a surprise)", 2, false, null, null, false, 4, true),
        new(OperationKind.LatePairing, "짝 이어주기: 짝이 생김", "Late Pairing: gains a Pair", null, null, 4, null, true, 4, true),
        new(OperationKind.PairAssignment, "짝 지정: 짝이 바뀜", "Assignment: changes partner", 2, false, 4, true, true, 4, true),
        new(OperationKind.RoleSwap, "자리 바꾸기: 자리를 바꿈", "Role Swap: swaps places", 2, false, 2, true, true, 2, true),
        new(OperationKind.PairAssignment, "짝 지정: 짝이 풀림", "Assignment: loses the Pair", 2, false, null, null, true, null, null),
        new(OperationKind.TimedShuffle, "짝 섞기: 짝 없던 사람", "Shuffle: had no Pair", null, null, null, null, false, 4, true),
    ];

    private Scene scene = Scenes[0];

    private FakeRoom room = null!;

    private float age;

    private bool closing;

    public static void Play(Scene scene, int seed)
    {
        PairLetter? letter = NoticeBoard.Letter;
        if (letter == null)
        {
            CoopNotice.Info(Strings.ShuffleCountdown(3));
            return;
        }

        ZooLetter play = letter.GetComponent<ZooLetter>() ?? letter.gameObject.AddComponent<ZooLetter>();
        play.scene = scene;
        play.room = new FakeRoom(6, true, true, seed);
        play.age = 0f;
        play.closing = false;
        play.enabled = true;
        letter.Open(Strings.LetterHeading(scene.Kind), new ZooPortraits(seed), play.room.NameOf, 1);
        NoticeBoard.Chime(bad: false);
        play.Update();
    }

    private void Update()
    {
        PairLetter letter = GetComponent<PairLetter>();
        age += Time.unscaledDeltaTime;
        if (age < Countdown)
        {
            int left = Mathf.CeilToInt(Countdown - age);
            PairChangeForMe soon = PairLetterPlan.ChangeFor(scene.Before, scene.Carried, scene.Soon, scene.SoonCarries, scene.Known);
            letter.Show(scene.Soon, scene.SoonCarries, scene.Known, left.ToString(), Strings.LetterSoon(soon, left));
            return;
        }

        PairChangeForMe done = PairLetterPlan.ChangeFor(scene.Before, scene.Carried, scene.After, scene.Carries, known: true);
        letter.Show(scene.After, scene.Carries, known: true, Strings.LetterArrived, Strings.LetterDoneFor(done));
        if (!closing)
        {
            closing = true;
            letter.CloseAfter(3f);
            enabled = false;
        }
    }
}

/// <summary>
/// Photos of made-up scouts, each dressed at random from the game's own
/// skins, outfits, hats, eyes, and mouths, the same way for the same seed.
/// </summary>
internal sealed class ZooPortraits : IPortraits
{
    private const int Wardrobe = 12;

    private static List<int[]>? looks;

    private readonly int seed;

    public ZooPortraits(int seed) => this.seed = seed;

    public Texture? Photo(int actor) =>
        Look(actor) is int[] look ? PhotoStudio.Take(Key(look), model => Dress(model, look)) : null;

    public Color Skin(int actor) => PhotoStudio.SkinColour(Look(actor)?[0] ?? actor % 4);

    /// <summary>Takes one photo of the Zoo's looks not yet taken; false when all are.</summary>
    public static bool WarmOne()
    {
        foreach (int[] look in Looks() ?? [])
        {
            if (!PhotoStudio.Has(Key(look)))
            {
                PhotoStudio.Take(Key(look), model => Dress(model, look));
                return true;
            }
        }

        return false;
    }

    private static string Key(int[] look) => "zoo:" + string.Join(",", look);

    /// <summary>The look of a made-up scout: the same set of looks, handed out anew for each example.</summary>
    private int[]? Look(int actor) =>
        Looks() is { } all ? all[(((actor - 1) + (seed * 5)) % all.Count + all.Count) % all.Count] : null;

    /// <summary>Skin, outfit, hat, eyes, and mouth for each look, or null when the game's lists are not there.</summary>
    private static List<int[]>? Looks()
    {
        if (looks != null)
        {
            return looks;
        }

        try
        {
            Customization options = Singleton<Customization>.Instance;
            var random = new System.Random(7);
            looks = Enumerable.Range(0, Wardrobe)
                .Select(_ => new[]
                {
                    random.Next(options.skins.Length),
                    random.Next(options.fits.Length),
                    random.Next(-1, options.hats.Length),
                    random.Next(options.eyes.Length),
                    random.Next(options.mouths.Length),
                })
                .ToList();
            return looks;
        }
        catch (Exception exception)
        {
            Plugin.Verbose($"No cosmetics for the Zoo: {exception.Message}");
            return null;
        }
    }

    private static void Dress(PlayerCustomizationDummy model, int[] look)
    {
        Customization options = Singleton<Customization>.Instance;
        model.SetPlayerColor(look[0]);
        model.SetPlayerCostume(look[1]);
        model.SetPlayerHat(look[2]);
        foreach (Renderer eye in model.refs.EyeRenderers)
        {
            eye.material.SetTexture("_MainTex", options.eyes[look[3]].texture);
        }

        model.refs.mouthRenderer.material.SetTexture("_MainTex", options.mouths[look[4]].texture);
    }
}
