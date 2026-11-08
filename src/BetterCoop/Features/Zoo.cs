#if DEBUG
using System.Linq;
using BetterCoop.Core;
using BetterCoop.Ui;
using BetterCoop.Ui.Paper;
using UnityEngine.UI;
using Zorro.UI;

namespace BetterCoop.Features;

/// <summary>
/// The Zoo, in debug builds only: a pause menu page that shows every paper
/// component in its states, and opens the Coop Menu on a made-up room of any
/// size, led by the local player or not, so its Pair changes can be tried
/// without other players. Nothing here reaches the config, the room, or the
/// network.
/// </summary>
internal sealed class ZooPage : PaperPage
{
    private static readonly string[] Choices = ["A", "B", "C"];

    private static readonly int[] Counts = Enumerable.Range(1, 12).ToArray();

    private CoopMenuPage example = null!;

    private bool toggle = true;

    private string choice = Choices[0];

    private int presses;

    private int players = 8;

    private bool leader = true;

    private bool formed = true;

    private int seed = 1;

    public static ZooPage Build(UIPageHandler handler, PauseMenuControlsPage controls, Button template, UIPage parent)
    {
        ZooPage zoo = Create<ZooPage>(handler, controls, template, parent, () => "Zoo");
        zoo.example = Create<CoopMenuPage>(handler, controls, template, zoo, () => $"{Strings.CoopMenu} (Zoo)");
        return zoo;
    }

    protected override string Shape() => "zoo";

    protected override void Fill(PaperList list)
    {
        list.Header(() => T("컴포넌트", "Components"));
        list.Info(() => T("정보 행", "Info row"));
        list.Info(() => T("값이 있는 정보 행", "Info row with a value"), () => T("값", "Value"));
        list.Info(
            () => T("설명이 있는 정보 행", "Info row with an explanation"),
            about: () => T("라벨 아래에 한 줄 설명이 붙습니다.", "A line of explanation sits under the label."));
        list.Info(
            () => T("긴 설명", "Long explanation"),
            about: () => T(
                "설명이 한 줄을 넘으면 잘리지 않고 다음 줄로 넘어가며, 행은 그만큼 높아집니다. 이 문장은 그 모습을 보이려고 일부러 길게 썼습니다.",
                "An explanation longer than a line wraps instead of being cut off, and its row grows to fit. This sentence is long on purpose to show that."));
        list.Action(
            () => T($"버튼 행 (누른 횟수 {presses})", $"Action row (pressed {presses})"),
            () => T("누르기", "Press"),
            () => presses++,
            about: () => T("오른쪽 버튼은 게임의 메뉴 버튼입니다.", "The button on the right is the game's menu button."));
        list.Action(
            () => T("쓸 수 없는 버튼", "Unavailable action"),
            () => T("누르기", "Press"),
            () => CoopNotice.Info(T("Zoo: 지금은 쓸 수 없어 이유를 알립니다.", "Zoo: unavailable now, so this says why.")),
            available: () => false,
            about: () => T("흐리게 보이지만 누르면 이유를 알려 줍니다.", "Dimmed, but pressing it says why."));
        list.Toggle(() => T("켬/끔", "Switch"), () => toggle, on => toggle = on, () => T("누를 때마다 켜고 끕니다.", "Each press turns it on or off."));
        list.Cycle(
            () => T("고르기", "Choice"),
            () => choice,
            value => choice = value,
            () => Choices,
            value => value,
            about: () => T("누를 때마다 다음 값으로 넘어갑니다.", "Each press steps to the next value."));
        list.Action(() => T("종이 다시 올리기", "Raise the paper again"), () => T("재생", "Play"), Rise);

        list.Header(() => T("코옵 메뉴 예제", "Coop Menu examples"));
        list.Cycle(
            () => T("인원", "Players"),
            () => players,
            count => players = count,
            () => Counts,
            count => T($"{count}명", $"{count}"),
            about: () => T("가짜 방의 인원입니다. 첫 사람이 나입니다.", "Players in the made-up room; the first is you."));
        list.Toggle(() => T("내가 분대장", "You lead"), () => leader, on => leader = on);
        list.Toggle(() => T("짝 편성 끝남", "Pairs formed"), () => formed, on => formed = on);
        list.Action(
            () => T("예제 열기", "Open the example"),
            () => T("열기", "Open"),
            OpenExample,
            about: () => T(
                "실제 규칙으로 짝을 섞고 지정하는 가짜 방에서 코옵 메뉴를 엽니다. 게임, 설정, 네트워크에는 닿지 않습니다.",
                "Opens the Coop Menu on a made-up room that shuffles and assigns Pairs by the real rules. It never touches the game, the config, or the network."));

        list.Header(() => T("긴 목록", "Long list"));
        for (int i = 1; i <= 20; i++)
        {
            int line = i;
            list.Info(() => T($"{line}번째 줄", $"Line {line}"), () => line.ToString());
        }
    }

    private static string T(string korean, string english) => Strings.Korean ? korean : english;

    private void OpenExample()
    {
        example.Show(new FakeRoom(players, leader, formed, seed++));
        example.Open();
    }
}
#endif
