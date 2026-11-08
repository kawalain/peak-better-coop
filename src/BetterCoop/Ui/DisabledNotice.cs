using System;
using UnityEngine;

namespace BetterCoop.Ui;

/// <summary>Tells the player, once per scene, that the mod turned itself off.</summary>
internal sealed class DisabledNotice : MonoBehaviour
{
    /// <summary>Why the mod is off; read when shown, so it follows the game language.</summary>
    internal Func<string> Message = () => Strings.ModDisabled;

    private PlayerConnectionLog? shownOn;

    private float nextCheck;

    private void Update()
    {
        if (Time.unscaledTime < nextCheck)
        {
            return;
        }

        nextCheck = Time.unscaledTime + 2f;
        PlayerConnectionLog? log = FindAnyObjectByType<PlayerConnectionLog>();
        if (log != null && log != shownOn)
        {
            shownOn = log;
            CoopNotice.Warning(Message());
        }
    }
}
