using BetterCoop.Core;
using BetterCoop.Ui.Notices;
using UnityEngine;

namespace BetterCoop.Ui;

/// <summary>
/// Tells the player something: on paper over the game, the paper chosen by
/// what kind of notice it is. When the paper cannot be shown, as before a
/// font is at hand or if building it fails, the notice goes to the game's
/// own player log (the top-left feed that announces joins and leaves) in
/// the log's own colours, as it also does while the mod is off.
/// </summary>
internal static class CoopNotice
{
    private static PlayerConnectionLog? log;

    /// <summary>Holds back the same notice again soon, and a flood of them.</summary>
    private static readonly NoticeGate Gate = new();

    /// <summary>News from the Troop Leader or the mod, on a telegram.</summary>
    public static void Info(string message) => Show(NoticeKind.Telegram, message);

    /// <summary>Something wrong with the party, as a caution.</summary>
    public static void Warning(string message) => Show(NoticeKind.Caution, message);

    /// <summary>Holds back what another player's doing puts up, apart from <see cref="Gate"/>.</summary>
    private static readonly SourceGate FromPlayers = new();

    /// <summary>
    /// A notice of something another player did, such as asking for a Role
    /// Swap: it passes its own gate, by player, so it never crowds out the
    /// mod's own notices.
    /// </summary>
    public static void From(int player, NoticeKind kind, string message)
    {
        if (!FromPlayers.Allow(player, Time.unscaledTimeAsDouble))
        {
            Plugin.Verbose($"[Notice held back] {message}");
            return;
        }

        Put(kind, message);
    }

    public static void Show(NoticeKind kind, string message)
    {
        if (!Gate.Allow(message, Time.unscaledTimeAsDouble))
        {
            Plugin.Verbose($"[Notice held back] {message}");
            return;
        }

        Put(kind, message);
    }

    private static void Put(NoticeKind kind, string message)
    {

        Plugin.Log.LogInfo($"[Notice] {message}");
        if (!NoticeBoard.TryShow(kind, Strings.Unprefixed(message)))
        {
            InGameLog(message, kind == NoticeKind.Caution);
        }
    }

    /// <summary>Writes to the game's player log: the fallback, and the only way while the mod is off.</summary>
    public static void InGameLog(string message, bool warning)
    {
        if (log == null)
        {
            log = Object.FindAnyObjectByType<PlayerConnectionLog>();
        }

        if (log == null)
        {
            return;
        }

        Color color = warning ? log.leftColor : log.joinedColor;
        log.AddMessage(log.GetColorTag(color) + message + "</color>");

        SFX_Instance? sfx = warning ? log.sfxLeave : log.sfxJoin;
        if (sfx != null)
        {
            sfx.Play();
        }
    }
}
