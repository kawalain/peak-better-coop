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
