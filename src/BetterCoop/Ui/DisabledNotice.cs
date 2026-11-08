using UnityEngine;

namespace BetterCoop.Ui;

/// <summary>Tells the player, once per scene, that the mod turned itself off.</summary>
internal sealed class DisabledNotice : MonoBehaviour
{
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
            CoopNotice.Warning(Strings.ModDisabled);
        }
    }
}
