# BetterCoop

Climb PEAK in Pairs. One player carries the other for the whole Run, and the
two of them steer that one body together: the **Carrier** on the keyboard, the
**Rider** on the mouse. They share one set of stamina and statuses, and they
live, die, and come back together.

[한국어 설명은 아래에 있습니다.](#한국어)

## How a Run plays

When the Run departs from the Airport, everyone is put into Pairs. Each player
can say which role they would like (Carrier, Rider, or Random) in the mod's
settings; when two wishes clash, a coin flip decides and the game tells you.

### Who controls what

| Carrier (keyboard) | Rider (mouse) |
| --- | --- |
| Move, jump, sprint, crouch | Look and steer |
| Interact, pick up, drop, throw | Use and secondary use of the held item |
| Hotbar slots, emotes | Scroll, ping |
| Open the backpack wheel | Pick from the backpack wheel |

The split follows the game's actions, not the keys, so your own key bindings
keep working.

- The body walks where the Rider looks. The Carrier sets the pace but cannot
  turn alone.
- To climb, the Rider holds the grip (left mouse button) and the Carrier climbs.
- Tapping the back-slot key brings the back item to hand. Holding it opens the
  Carrier's own backpack wheel; the Carrier keeps it open while the Rider
  clicks the item. Releasing the key first cancels.

### One body, one view

- Both players see through the Carrier's eyes in first person, with the same
  crosshair, placement previews, item prompts, and use progress.
- The Rider's screen shows the Carrier's hotbar, item uses, stamina, and
  statuses: they are the Pair's own.
- The Rider aims without network delay; the Carrier's view follows a moment
  later.
- The Rider never passes out or dies alone. When the Carrier goes down, both
  do; reviving either one brings both back.
- A Rider holds no items and wears nothing on their back. Anything that lands
  there goes to the Carrier's back if it is free, and is set down otherwise.

### Changing Pairs

- **Campfire Shuffle**: when a campfire is lit, the Pairs are formed anew.
  With enough players everyone gets a new partner; otherwise partners trade
  roles. On by default.
- **Timed Shuffle**: the same at a fixed interval. Off by default.
- **Swap Places**: in the ESC menu. When both members of a Pair press it within
  30 seconds, they trade roles.
- When Pairs change, each new Carrier takes over a former body: its statuses
  always, and its place and items if the Troop Leader allows it. Items are
  swapped, never lost or copied.
- A three-second countdown announces every change.

### Departing

Everyone in the room needs the same version of BetterCoop. At the Airport
check-in kiosk the game names anyone who is missing the mod or has another
version, and it asks for an even number of players unless the Troop Leader
allows an odd one. An extra player then plays the Run alone, as in the normal
game.

## Installing

Install BetterCoop with a mod manager such as Gale, r2modman, or the
Thunderstore Mod Manager; it brings BepInEx along. For a test build you were
sent as a zip, import the zip as a local mod (Gale: *Import* → *Local mod*).

Every player in the room needs it, at the same version.

## Settings

The settings are in your mod manager's config editor under
`in.kawala.bettercoop`. The Troop Leader's values apply to the whole room,
except for your own Role Preference and the debug settings.

| Section | Setting | Default | What it does |
| --- | --- | --- | --- |
| Pair | Role Preference | Random | The role you would like: Carrier, Rider, or Random. |
| Pair | Rider Picks From Backpack | On | The Rider picks from the backpack wheel. Off: the Carrier picks, and only the Carrier sees the wheel. |
| Party | Allow Odd Player Count | Off | Lets a Run depart with an odd number of players. |
| Shuffle | Campfire Shuffle | On | New Pairs at each campfire. |
| Shuffle | Timed Shuffle | Off | New Pairs at a fixed interval. |
| Shuffle | Timed Shuffle Interval | 10 | Minutes between Timed Shuffles (1–60). |
| Shuffle | Hand Over Place | On | A new Carrier moves to where the body they take over stood. |
| Shuffle | Hand Over Items | On | A new Carrier takes over that body's items. |
| Balance | Carry Weight | Off | Applies the game's weight for carrying a person to every Carrier. |

Debug settings, for reporting problems:

- **Verbose Log** (on): writes what the mod does to `BepInEx/LogOutput.log`.
  Please attach that file when you report a bug.
- **Recovery Key** (on): if a carry ever comes apart, the Troop Leader can
  press F8 to put every Pair back together.
- **Solo Pair** (off): lets one player try both roles alone in a room.

## Compatibility

- BetterCoop works next to other mods. Mods that change carrying (spectating
  while carried, dropping or swapping backpacks) are welcome; while you are in
  a Pair, the Pair's rules win.
- BetterCoop turns itself off when Co-op Mod is also installed, because both
  change the same carry. Remove one of them.
- After a game update that BetterCoop does not fit yet, it turns itself off
  and says so, rather than half working. Everyone then sees it as missing at
  the kiosk until an update comes.

## Antivirus warnings

Some antivirus programs, Windows Defender among them, may flag
`BetterCoop.dll` with a name ending in `!ml`. That suffix means a
machine-learning guess, not a match with known malware. Game mods are
easily mistaken this way: BetterCoop changes the game's code while it runs,
which is also what some harmful programs do.

BetterCoop runs only inside PEAK, through BepInEx. It does not download or
start other programs, and it talks only to the other players in your room,
through the game's own connection. The only files it writes are its settings
file and the BepInEx log.

If your antivirus flags it:

1. Make sure the file came from this mod's page or straight from the person
   who sent you the test build.
2. Report it to your antivirus vendor as a false positive. Microsoft takes
   reports at <https://www.microsoft.com/en-us/wdsi/filesubmission>.
3. Restore the file from quarantine, or reinstall the mod. A new version is a
   different file and is often not flagged.

## Credits

- [Sappheiros009](https://github.com/Sappheiros009/CoopMod): the original
  idea, from Co-op Mod.
- [Puro](https://steamcommunity.com/profiles/76561198105109754): ideas.
- [kawalain](https://steamcommunity.com/profiles/76561198436496102):
  maintainer.

---

## 한국어

PEAK를 짝을 지어 오르는 모드입니다. 한 사람이 원정 내내 다른 사람을 업고,
두 사람이 한 몸을 함께 움직입니다. **업는 사람**은 키보드, **업힌 사람**은
마우스를 맡습니다. 스태미나와 상태 이상을 함께 쓰고, 함께 살고 죽고
되살아납니다.

### 원정 진행

공항에서 원정을 떠나면 모두 짝이 됩니다. 원하는 역할(업는 사람, 업힌 사람,
무작위)은 모드 설정에서 고를 수 있고, 둘의 희망이 겹치면 무작위로 정한 뒤
알려 줍니다.

#### 조작 나누기

| 업는 사람 (키보드) | 업힌 사람 (마우스) |
| --- | --- |
| 이동, 점프, 달리기, 웅크리기 | 시점과 방향 |
| 상호작용, 줍기, 내려놓기, 던지기 | 든 아이템 사용, 보조 사용 |
| 단축 슬롯, 감정 표현 | 스크롤, 핑 |
| 가방 휠 열기 | 가방 휠에서 고르기 |

키가 아니라 게임의 동작 단위로 나누므로, 키를 바꿔 설정해도 그대로 됩니다.

- 몸은 업힌 사람이 보는 쪽으로 걷습니다. 업는 사람은 속도를 정하지만 혼자
  방향을 바꿀 수는 없습니다.
- 벽을 오를 때는 업힌 사람이 잡기(마우스 왼쪽 버튼)를 누르고 있고, 업는
  사람이 오릅니다.
- 등 슬롯 키를 짧게 누르면 등의 아이템을 손에 듭니다. 길게 누르면 업는
  사람의 가방 휠이 열립니다. 업는 사람이 키를 누르고 있는 동안 업힌 사람이
  아이템을 클릭해 고릅니다. 먼저 키를 떼면 취소됩니다.

#### 한 몸, 한 시점

- 두 사람 모두 업는 사람의 눈으로 1인칭 화면을 봅니다. 조준점, 설치
  미리보기, 아이템 안내, 사용 진행도도 같습니다.
- 업힌 사람의 화면에는 업는 사람의 단축 슬롯, 아이템 남은 사용량, 스태미나,
  상태 이상이 보입니다. 짝이 함께 쓰는 것이기 때문입니다.
- 업힌 사람은 네트워크 지연 없이 조준하고, 업는 사람의 화면이 잠시 뒤
  따라옵니다.
- 업힌 사람은 혼자 기절하거나 죽지 않습니다. 업는 사람이 쓰러지면 둘 다
  쓰러지고, 누구를 되살려도 둘 다 돌아옵니다.
- 업힌 사람은 아이템을 들거나 등에 메지 않습니다. 그런 아이템이 생기면 업는
  사람의 등이 비어 있을 때 그쪽으로 옮기고, 아니면 바닥에 내려놓습니다.

#### 짝 바꾸기

- **모닥불 짝 섞기**: 모닥불을 피우면 짝을 새로 짓습니다. 인원이 충분하면
  모두 새 짝을 만나고, 아니면 같은 짝끼리 역할을 바꿉니다. 기본으로 켜져
  있습니다.
- **시간 짝 섞기**: 정해진 간격마다 같은 일을 합니다. 기본으로 꺼져 있습니다.
- **자리 바꾸기**: ESC 메뉴에 있습니다. 짝 두 사람이 30초 안에 모두 누르면
  역할을 바꿉니다.
- 짝이 바뀌면 새 업는 사람은 이전의 몸 하나를 이어받습니다. 상태 이상은
  항상, 위치와 아이템은 분대장이 허용하면 이어받습니다. 아이템은 서로
  맞바꾸므로 사라지거나 복사되지 않습니다.
- 바뀌기 전에는 항상 3초를 셉니다.

#### 출발

방의 모든 사람이 같은 버전의 BetterCoop을 써야 합니다. 공항 탑승 키오스크가
모드가 없거나 버전이 다른 사람을 알려 주고, 분대장이 허용하지 않으면 인원이
짝수여야 출발합니다. 허용하면 남는 한 사람은 원래 게임처럼 혼자 원정합니다.

### 설치

Gale, r2modman, Thunderstore Mod Manager 같은 모드 매니저로 설치하세요.
BepInEx도 함께 설치됩니다. zip 파일로 받은 테스트 빌드는 로컬 모드로
가져오면 됩니다(Gale: *Import* → *Local mod*).

방의 모든 사람이 같은 버전으로 설치해야 합니다.

### 설정

모드 매니저의 설정 편집기에서 `in.kawala.bettercoop` 아래에 있습니다.
자신의 원하는 역할과 디버그 설정을 빼면, 분대장의 값이 방 전체에
적용됩니다.

| 구역 | 설정 | 기본값 | 설명 |
| --- | --- | --- | --- |
| Pair | Role Preference | Random | 원하는 역할: Carrier(업는 사람), Rider(업힌 사람), Random(무작위). |
| Pair | Rider Picks From Backpack | 켬 | 가방 휠에서 업힌 사람이 고릅니다. 끄면 업는 사람이 고르고, 업는 사람만 휠을 봅니다. |
| Party | Allow Odd Player Count | 끔 | 인원이 홀수여도 출발합니다. |
| Shuffle | Campfire Shuffle | 켬 | 모닥불마다 짝을 새로 짓습니다. |
| Shuffle | Timed Shuffle | 끔 | 정해진 간격마다 짝을 새로 짓습니다. |
| Shuffle | Timed Shuffle Interval | 10 | 시간 짝 섞기 간격(분, 1–60). |
| Shuffle | Hand Over Place | 켬 | 새 업는 사람이 이어받는 몸이 있던 자리로 옮겨 갑니다. |
| Shuffle | Hand Over Items | 켬 | 새 업는 사람이 그 몸의 아이템을 이어받습니다. |
| Balance | Carry Weight | 끔 | 사람을 업을 때의 게임 무게를 모든 업는 사람에게 적용합니다. |

문제를 알릴 때 쓰는 디버그 설정:

- **Verbose Log**(켬): 모드가 하는 일을 `BepInEx/LogOutput.log`에 적습니다.
  버그를 알릴 때 이 파일을 함께 보내 주세요.
- **Recovery Key**(켬): 업기가 풀리면 분대장이 F8을 눌러 모든 짝을 다시
  붙일 수 있습니다.
- **Solo Pair**(끔): 혼자 방에서 두 역할을 모두 시험해 볼 수 있습니다.

### 호환성

- 다른 모드와 함께 쓸 수 있습니다. 업기를 바꾸는 모드(업혔을 때 관전 화면,
  가방 내려놓기나 맞바꾸기 등)도 괜찮고, 짝인 동안에는 짝의 규칙이
  우선합니다.
- Co-op Mod가 함께 설치되어 있으면 BetterCoop이 스스로 꺼집니다. 둘 다 같은
  업기를 바꾸기 때문입니다. 둘 중 하나를 지우세요.
- 게임 업데이트로 맞지 않게 되면, 반쯤 동작하는 대신 스스로 꺼지고
  알려 줍니다. 업데이트가 나올 때까지 키오스크에서는 모드가 없는 것으로
  보입니다.

### 백신 경고

Windows Defender 등 일부 백신이 `BetterCoop.dll`을 `!ml`로 끝나는 이름으로
잡을 수 있습니다. 이 꼬리표는 알려진 악성코드와 일치한다는 뜻이 아니라
머신러닝이 추측했다는 뜻입니다. 게임 모드는 이런 오진을 받기 쉽습니다.
BetterCoop은 실행 중인 게임의 코드를 바꾸는데, 일부 악성 프로그램도 같은
일을 하기 때문입니다.

BetterCoop은 BepInEx를 통해 PEAK 안에서만 동작합니다. 다른 프로그램을
내려받거나 실행하지 않고, 게임 자체의 연결로 같은 방의 플레이어와만
통신합니다. 직접 쓰는 파일은 설정 파일과 BepInEx 로그뿐입니다.

백신이 잡았다면:

1. 파일을 이 모드의 페이지나 테스트 빌드를 보내 준 사람에게서 직접 받았는지
   확인하세요.
2. 백신 회사에 오진으로 신고하세요. Microsoft는
   <https://www.microsoft.com/en-us/wdsi/filesubmission>에서 받습니다.
3. 격리된 파일을 복원하거나 모드를 다시 설치하세요. 새 버전은 다른 파일이라
   잡히지 않는 경우가 많습니다.

### 만든 사람들

- [Sappheiros009](https://github.com/Sappheiros009/CoopMod): Co-op Mod의
  원래 아이디어.
- [Puro](https://steamcommunity.com/profiles/76561198105109754): 아이디어 제공.
- [kawalain](https://steamcommunity.com/profiles/76561198436496102): 관리자.
