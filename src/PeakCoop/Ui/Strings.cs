namespace PeakCoop.Ui;

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
        : "Co-op: the mod is off because it does not match this game version.";

    public static string PairFormed(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님이 {rider} 님을 업습니다. {carrier} 님은 키보드, {rider} 님은 마우스로 조작합니다."
        : $"Co-op: {carrier} (Carrier, keyboard) carries {rider} (Rider, mouse).";

    public static string PreferenceOverridden(string player) => Korean
        ? $"코옵: 원하는 역할이 겹쳐 {player} 님은 다른 역할을 맡습니다."
        : $"Co-op: roles clashed, so {player} takes the other role.";

    public static string Unpaired(string player) => Korean
        ? $"코옵: 이번 원정에서 {player} 님은 짝 없이 올라갑니다."
        : $"Co-op: {player} plays this Run without a Pair.";

    public static string PairDissolved(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님과 {rider} 님의 짝이 풀렸습니다."
        : $"Co-op: the Pair of {carrier} and {rider} has ended.";

    public static string NoBackpack => Korean
        ? "코옵: 짝을 업고 있을 때는 배낭을 멜 수 없습니다."
        : "Co-op: you cannot wear a backpack while carrying your Rider.";

    public static string Recovered => Korean
        ? "코옵: 분대장이 모든 짝의 업기 상태를 되돌렸습니다."
        : "Co-op: the Troop Leader repaired every Pair's carry.";

    public static string SoloPairOn(int latencyMs) => Korean
        ? $"코옵: 1인 짝 테스트를 켰습니다. (마우스 지연 {latencyMs}ms)"
        : $"Co-op: Solo Pair is on (mouse delayed {latencyMs} ms).";

    public static string SoloPairOff => Korean
        ? "코옵: 1인 짝 테스트를 껐습니다."
        : "Co-op: Solo Pair is off.";

    public static string OddPlayerCount(int count) => Korean
        ? $"코옵: 출발하려면 인원이 짝수여야 합니다. (현재 {count}명)"
        : $"Co-op: an even number of players is needed to depart. ({count} in the room)";

    public static string MissingMod(string player) => Korean
        ? $"코옵: {player} 님에게 모드가 없습니다."
        : $"Co-op: {player} does not have the mod.";

    public static string OtherVersion(string player, string theirs, string required) => Korean
        ? $"코옵: {player} 님의 모드 버전이 다릅니다. ({theirs}, 필요한 버전 {required})"
        : $"Co-op: {player} has mod version {theirs}. (needed: {required})";
}
