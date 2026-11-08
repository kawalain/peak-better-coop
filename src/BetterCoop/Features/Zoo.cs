#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Ui;
using BetterCoop.Ui.Paper;
using UnityEngine;
using UnityEngine.UI;
using Zorro.Core;
using Zorro.UI;

namespace BetterCoop.Features;

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

    private RosterPage example = null!;

    private static readonly int[] ManyChoices = [1, 2, 3, 5, 10, 15, 20, 30];

    private bool check = true;

    private int minutes = 10;

    private bool swapped;

    private string choice = Choices[0];

    private int presses;

    private int players = 8;

    private bool leader = true;

    private bool formed = true;

    private int seed = 1;

    public static ZooPage Build(UIPageHandler handler, PauseMenuControlsPage controls, Button template, UIPage parent)
    {
        ZooPage zoo = Create<ZooPage>(handler, controls, template, parent, () => "Zoo");
        zoo.example = Create<RosterPage>(handler, controls, template, zoo, () => $"{Strings.Roster} (Zoo)");
        return zoo;
    }

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
        list.Signature(() => T("서명", "Signed"), () => "Alder");

        list.Header(() => T("편성표 예제", "Roster examples"));
        list.Choice(
            () => T("인원", "Players"),
            () => players,
            count => players = count,
            () => Counts,
            count => T($"{count}명", $"{count}"),
            about: () => T("가짜 방의 인원입니다. 첫 사람이 나입니다.", "Players in the made-up room; the first is you."));
        list.Check(() => T("내가 분대장", "You lead"), () => leader, on => leader = on);
        list.Check(() => T("짝 편성 끝남", "Pairs formed"), () => formed, on => formed = on);
        list.Stamp(
            () => T("예제 열기", "Open the example"),
            () => T("열기", "Open"),
            OpenExample,
            about: () => T(
                "실제 규칙으로 짝을 섞고 지정하는 가짜 방에서 편성표를 엽니다. 게임, 설정, 네트워크에는 닿지 않습니다.",
                "Opens the Roster on a made-up room that shuffles and assigns Pairs by the real rules. It never touches the game, the config, or the network."));

        list.Header(() => T("긴 목록", "Long list"));
        for (int i = 1; i <= 20; i++)
        {
            int line = i;
            list.Field(() => T($"{line}번째 줄", $"Line {line}"), () => line.ToString());
        }
    }

    private static string T(string korean, string english) => Strings.Korean ? korean : english;

    private void OpenExample()
    {
        example.Show(new FakeRoom(players, leader, formed, seed), new ZooPortraits(seed));
        seed++;
        example.Open();
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
#endif
