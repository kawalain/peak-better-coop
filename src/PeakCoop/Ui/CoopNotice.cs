using UnityEngine;

namespace PeakCoop.Ui;

/// <summary>
/// Shows co-op messages in the game's own player connection log (top-left
/// feed that announces joins and leaves), styled like the game's entries.
/// </summary>
internal static class CoopNotice
{
    private static PlayerConnectionLog? log;

    public static void Info(string message) => Show(message, warning: false);

    public static void Warning(string message) => Show(message, warning: true);

    private static void Show(string message, bool warning)
    {
        Plugin.Log.LogInfo($"[Notice] {message}");

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
