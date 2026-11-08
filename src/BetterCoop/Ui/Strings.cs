namespace BetterCoop.Ui;

/// <summary>
/// Player-facing text in Korean and English, following the game language.
/// Korean matches the game's own Korean: formal endings (합니다체) and the
/// Korean terms in GLOSSARY.md.
/// </summary>
internal static class Strings
{
    private static bool Korean => LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.Korean;

    public static string ModDisabled => Korean
        ? "코옵: 지금 게임 버전과 맞지 않아 모드를 껐습니다."
        : "Coop: the mod is off because it does not match this game version.";

    public static string ModDisabledByCoopMod => Korean
        ? "코옵: Co-op Mod와 함께 쓸 수 없어 모드를 껐습니다. Co-op Mod를 지워 주세요."
        : "Coop: the mod is off because Co-op Mod is also installed. Remove Co-op Mod to play.";

    public static string PairFormed(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님이 {rider} 님을 업습니다. {carrier} 님은 키보드, {rider} 님은 마우스로 조작합니다."
        : $"Coop: {carrier} (Carrier, keyboard) carries {rider} (Rider, mouse).";

    public static string PreferenceOverridden(string player) => Korean
        ? $"코옵: 원하는 역할이 겹쳐 {player} 님은 다른 역할을 맡습니다."
        : $"Coop: roles clashed, so {player} takes the other role.";

    public static string Unpaired(string player) => Korean
        ? $"코옵: {player} 님은 지금은 짝 없이 올라갑니다."
        : $"Coop: {player} plays without a Pair for now.";

    public static string PairDissolved(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님과 {rider} 님의 짝이 풀렸습니다."
        : $"Coop: the Pair of {carrier} and {rider} has ended.";

    public static string ShuffleCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 짝을 섞습니다."
        : $"Coop: shuffling Pairs in {seconds}...";

    public static string RoleSwapCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 자리를 바꿉니다."
        : $"Coop: swapping places in {seconds}...";

    public static string LatePairingCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 짝을 이어 줍니다."
        : $"Coop: pairing you up in {seconds}...";

    public static string CoopMenu => Korean ? "코옵" : "Coop";

    public static string MenuRolePreference => Korean ? "원하는 역할" : "Role Preference";

    public static string RoleName(Core.RolePreference preference) => preference switch
    {
        Core.RolePreference.Carrier => Korean ? "업는 사람" : "Carrier",
        Core.RolePreference.Rider => Korean ? "업힌 사람" : "Rider",
        _ => Korean ? "무작위" : "Random",
    };

    public static string MenuPartner(string partner, bool carrying) => Korean
        ? carrying ? $"짝: {partner} 님을 업고 있습니다" : $"짝: {partner} 님에게 업혀 있습니다"
        : carrying ? $"Partner: you carry {partner}" : $"Partner: {partner} carries you";

    public static string MenuNoPartner => Korean ? "지금은 짝이 없습니다" : "You have no partner for now";

    public static string MenuNextShuffle => Korean ? "다음 짝 섞기" : "Next shuffle";

    public static string MenuShuffleNow => Korean ? "진행 중" : "Now";

    public static string MenuShuffleOff => Korean ? "꺼짐" : "Off";

    public static string Clock(double seconds)
    {
        int whole = (int)System.Math.Ceiling(seconds);
        return $"{whole / 60}:{whole % 60:00}";
    }

    public static string MenuPairsForm => Korean ? "짝은 섬에 도착하면 편성됩니다" : "Pairs form on the island";

    public static string MenuPairs => Korean ? "짝 (업는 사람 ⇄ 업힌 사람)" : "Pairs (Carrier ⇄ Rider)";

    public static string MenuPair(string carrier, string rider) => $"{carrier} ⇄ {rider}";

    public static string MenuTroopLeader => Korean ? "분대장" : "Troop Leader";

    public static string MenuShuffleNowLabel => Korean ? "지금 짝 섞기" : "Shuffle Pairs now";

    public static string MenuRecover => Korean ? "모든 짝 다시 붙이기" : "Re-attach every Pair";

    public static string MenuGo => Korean ? "실행" : "Go";

    public static string MenuBusy => Korean
        ? "코옵: 다른 짝 변경이 진행 중이라 지금은 할 수 없습니다."
        : "Coop: another Pair change is under way; try again in a moment.";

    public static string MenuRunRules => Korean ? "원정 규칙" : "Run Rules";

    public static string On => Korean ? "켬" : "On";

    public static string Off => Korean ? "끔" : "Off";

    public static string Minutes(int minutes) => Korean ? $"{minutes}분" : $"{minutes} min";

    public static string RuleCampfireShuffle => Korean ? "모닥불에서 짝 섞기" : "Campfire Shuffle";

    public static string RuleTimedShuffle => Korean ? "시간마다 짝 섞기" : "Timed Shuffle";

    public static string RuleTimedShuffleInterval => Korean ? "짝 섞기 간격" : "Timed Shuffle Interval";

    public static string RuleHandOverPlace => Korean ? "몸 인계: 자리" : "Hand Over Place";

    public static string RuleHandOverItems => Korean ? "몸 인계: 아이템" : "Hand Over Items";

    public static string RuleCarryWeight => Korean ? "사람 무게 적용" : "Carry Weight";

    public static string RuleRiderPicksFromBackpack => Korean ? "가방은 업힌 사람이 고르기" : "Rider Picks From Backpack";

    public static string RuleAllowOddPlayerCount => Korean ? "홀수 인원 출발 허용" : "Allow Odd Player Count";

    public static string AboutRolePreference => Korean
        ? "짝 편성과 짝 섞기에서 원하는 역할입니다. 다음 번부터 적용됩니다."
        : "The role you would like when Pairs are formed or shuffled; applies from the next time.";

    public static string AboutSwapPlaces => Korean
        ? "둘 다 요청하면 업는 사람과 업힌 사람이 바뀝니다."
        : "When both of you ask, you trade Carrier and Rider.";

    public static string AboutNoPartner => Korean
        ? "짝 없는 사람이 생기거나 짝을 섞으면 짝이 생깁니다."
        : "You get a partner when another player is free or the Pairs shuffle.";

    public static string AboutNextShuffle => Korean
        ? "시간마다 짝 섞기까지 남은 시간입니다."
        : "Time left until the next Timed Shuffle.";

    public static string AboutShuffleNow => Korean
        ? "모든 짝을 지금 새로 정합니다."
        : "Forms every Pair anew now.";

    public static string AboutRecover => Korean
        ? "업기가 풀렸을 때 모든 짝을 다시 업히고 업힌 사람을 깨웁니다."
        : "Re-attaches every Pair's carry and wakes every Rider.";

    public static string AboutAssignCarrier => Korean
        ? "짝으로 묶을 사람 중 업을 사람을 고릅니다."
        : "Picks who carries in the Pair you assign.";

    public static string AboutAssignRider => Korean
        ? "짝으로 묶을 사람 중 업힐 사람을 고릅니다."
        : "Picks who rides in the Pair you assign.";

    public static string AboutAssign => Korean
        ? "고른 두 사람을 짝으로 묶습니다. 원래 짝은 남은 사람끼리 다시 묶입니다."
        : "Pairs the two you picked; their former partners pair up with each other.";

    public static string AboutCampfireShuffle => Korean
        ? "모닥불을 피워 다음 구간으로 갈 때 짝을 새로 정합니다."
        : "Forms the Pairs anew when a campfire is lit.";

    public static string AboutTimedShuffle => Korean
        ? "정해진 간격마다 짝을 새로 정합니다."
        : "Forms the Pairs anew at a fixed interval.";

    public static string AboutTimedShuffleInterval => Korean
        ? "시간마다 짝 섞기의 간격입니다."
        : "How often a Timed Shuffle comes.";

    public static string AboutHandOverPlace => Korean
        ? "짝이 바뀌면 새 업는 사람이 넘겨받는 몸의 자리로 갑니다."
        : "A new Carrier moves to where the body they take over stood.";

    public static string AboutHandOverItems => Korean
        ? "짝이 바뀌면 새 업는 사람이 넘겨받는 몸의 아이템을 가집니다."
        : "A new Carrier takes the items of the body they take over.";

    public static string AboutCarryWeight => Korean
        ? "업는 사람에게 사람을 업은 무게를 적용합니다."
        : "Carriers feel the weight of the person they carry.";

    public static string AboutRiderPicksFromBackpack => Korean
        ? "가방 휠에서 업힌 사람이 아이템을 고릅니다. 끄면 업는 사람이 고릅니다."
        : "The Rider picks from the backpack wheel; off, the Carrier does.";

    public static string AboutAllowOddPlayerCount => Korean
        ? "인원이 홀수여도 출발합니다. 남는 사람은 짝 없이 올라갑니다."
        : "Lets a Run depart with an odd count; one player climbs without a Pair.";

    public static string MenuUnpaired => Korean ? "짝 없는 사람" : "Unpaired";

    public static string AssignmentCountdown(int seconds) => Korean
        ? $"코옵: {seconds}초 뒤 분대장이 정한 짝이 됩니다."
        : $"Coop: the Troop Leader pairs you up in {seconds}...";

    public static string MenuAssignCarrier => Korean ? "짝 지정: 업는 사람" : "Assign: Carrier";

    public static string MenuAssignRider => Korean ? "짝 지정: 업힐 사람" : "Assign: Rider";

    public static string MenuAssign => Korean ? "짝 지정" : "Assign Pair";

    public static string MenuAssignUnavailable => Korean
        ? "코옵: 두 사람이 서로 달라야 하고, 업는 사람은 깨어 있어야 하며, 둘 다 같은 버전의 모드가 있어야 합니다."
        : "Coop: pick two different players with this version; the Carrier must be awake.";

    public static string SwapPlaces => Korean ? "자리 바꾸기" : "Swap Places";

    public static string CancelSwap => Korean ? "자리 바꾸기 취소" : "Cancel Swap";

    public static string SwapUnavailable => Korean
        ? "코옵: 짝과 둘 다 섬에서 살아 있을 때만 자리를 바꿀 수 있습니다."
        : "Coop: you can swap places only while you and your partner are alive on the island.";

    public static string SwapAsked(string partner) => Korean
        ? $"코옵: {partner} 님에게 자리 바꾸기를 청했습니다. 30초 안에 상대도 누르면 바꿉니다."
        : $"Coop: asked {partner} to swap places. It happens if they agree within 30 seconds.";

    public static string SwapOffered(string partner) => Korean
        ? $"코옵: {partner} 님이 자리를 바꾸고 싶어 합니다. ESC에서 자리 바꾸기를 누르면 바꿉니다."
        : $"Coop: {partner} wants to swap places. Press Swap Places in the ESC menu to agree.";

    public static string SwapWithdrawn(string partner) => Korean
        ? $"코옵: {partner} 님의 자리 바꾸기 요청이 취소되었습니다."
        : $"Coop: {partner} no longer asks to swap places.";

    public static string SwapCancelled => Korean
        ? "코옵: 자리 바꾸기 요청을 취소했습니다."
        : "Coop: your request to swap places is cancelled.";

    public static string SwapExpired => Korean
        ? "코옵: 30초 동안 답이 없어 자리 바꾸기 요청을 거두었습니다."
        : "Coop: no answer in 30 seconds, so your request to swap places lapsed.";

    public static string SwapCancelledByShuffle => Korean
        ? "코옵: 짝 섞기가 시작되어 자리 바꾸기 요청을 취소했습니다."
        : "Coop: a Pair Shuffle is starting, so your request to swap places is cancelled.";

    public static string RiderHandEmptied => Korean
        ? "코옵: 업힌 사람은 아이템을 손에 들 수 없어 바닥에 내려놓았습니다."
        : "Coop: a Rider cannot hold items, so it was dropped.";

    public static string RiderBackEmptied => Korean
        ? "코옵: 업힌 사람은 등에 아이템을 멜 수 없어 바닥에 내려놓았습니다."
        : "Coop: a Rider cannot wear a back item, so it was dropped.";

    public static string Recovered => Korean
        ? "코옵: 분대장이 모든 짝의 업기 상태를 되돌렸습니다."
        : "Coop: the Troop Leader repaired every Pair's carry.";

    public static string SoloPairOn(int latencyMs) => Korean
        ? $"코옵: 1인 짝 테스트를 켰습니다. (마우스 지연 {latencyMs}ms)"
        : $"Coop: Solo Pair is on (mouse delayed {latencyMs} ms).";

    public static string SoloPairOff => Korean
        ? "코옵: 1인 짝 테스트를 껐습니다."
        : "Coop: Solo Pair is off.";

    public static string OddPlayerCount(int count) => Korean
        ? $"코옵: 출발하려면 인원이 짝수여야 합니다. (현재 {count}명)"
        : $"Coop: an even number of players is needed to depart. ({count} in the room)";

    public static string MissingMod(string player) => Korean
        ? $"코옵: {player} 님에게 모드가 없습니다."
        : $"Coop: {player} does not have the mod.";

    public static string OtherVersion(string player, string theirs, string required) => Korean
        ? $"코옵: {player} 님의 모드 버전이 다릅니다. ({theirs}, 필요한 버전 {required})"
        : $"Coop: {player} has mod version {theirs}. (needed: {required})";
}
