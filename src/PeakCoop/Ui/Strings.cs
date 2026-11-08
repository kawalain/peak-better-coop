namespace PeakCoop.Ui;

/// <summary>Player-facing text in Korean and English, following the game language.</summary>
internal static class Strings
{
    private static bool Korean => LocalizedText.CURRENT_LANGUAGE == LocalizedText.Language.Korean;

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
