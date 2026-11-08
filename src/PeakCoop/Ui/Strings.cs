namespace PeakCoop.Ui;

/// <summary>Player-facing text in Korean and English, following the game language.</summary>
internal static class Strings
{
    private static bool Korean => LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.Korean;

    public static string ModDisabled => Korean
        ? "코옵: 이 게임 버전과 맞지 않아 모드가 꺼졌습니다."
        : "Co-op: the mod is off because it does not match this game version.";

    public static string PairFormed(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님(Carrier, 키보드)이 {rider} 님(Rider, 마우스)을 업습니다."
        : $"Co-op: {carrier} (Carrier, keyboard) carries {rider} (Rider, mouse).";

    public static string PreferenceOverridden(string player) => Korean
        ? $"코옵: 역할이 겹쳐 {player} 님은 원하던 역할과 반대 역할을 맡습니다."
        : $"Co-op: roles clashed, so {player} takes the other role.";

    public static string Unpaired(string player) => Korean
        ? $"코옵: {player} 님은 이번 Run에서 짝 없이 플레이합니다."
        : $"Co-op: {player} plays this Run without a Pair.";

    public static string PairDissolved(string carrier, string rider) => Korean
        ? $"코옵: {carrier} 님과 {rider} 님의 Pair가 해체되었습니다."
        : $"Co-op: the Pair of {carrier} and {rider} has ended.";

    public static string NoBackpack => Korean
        ? "코옵: 사람을 업은 상태에서는 가방을 멜 수 없습니다."
        : "Co-op: you cannot wear a backpack while carrying your Rider.";

    public static string Recovered => Korean
        ? "코옵: 방장이 Pair 업기 상태를 복구했습니다."
        : "Co-op: the host repaired every Pair's carry.";

    public static string SoloPairOn(int latencyMs) => Korean
        ? $"코옵: Solo Pair 테스트 모드 (마우스 지연 {latencyMs}ms)"
        : $"Co-op: Solo Pair test mode (mouse delayed {latencyMs} ms)";

    public static string SoloPairOff => Korean
        ? "코옵: Solo Pair 테스트 모드가 꺼졌습니다."
        : "Co-op: Solo Pair test mode is off.";

    public static string OddPlayerCount(int count) => Korean
        ? $"코옵: 출발하려면 짝수 인원이 필요합니다. (현재 {count}명)"
        : $"Co-op: an even number of players is needed to depart. ({count} in the room)";

    public static string MissingMod(string player) => Korean
        ? $"코옵: {player} 님에게 모드가 없습니다."
        : $"Co-op: {player} does not have the mod.";

    public static string OtherVersion(string player, string theirs, string required) => Korean
        ? $"코옵: {player} 님의 모드 버전은 {theirs}입니다. (필요: {required})"
        : $"Co-op: {player} has mod version {theirs}. (needed: {required})";
}
