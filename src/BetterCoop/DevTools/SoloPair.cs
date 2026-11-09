using BepInEx.Configuration;
using BetterCoop.Ui;
using Photon.Pun;
using UnityEngine;

namespace BetterCoop.DevTools;

/// <summary>
/// Solo Pair's settings and switch. The Control Split keeps the loopback
/// itself (the Rider's actions encoded, held back, and replayed on the same
/// client), which runs only while <see cref="DevHooks.SoloPair"/> is true and
/// so never in a Release build.
/// </summary>
internal static class SoloPair
{
    public static ConfigEntry<bool> On { get; private set; } = null!;

    public static ConfigEntry<int> Latency { get; private set; } = null!;

    /// <summary>Switched on, and alone in a room.</summary>
    public static bool Wanted => On.Value && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.PlayerCount == 1;

    public static void Bind(ConfigFile config)
    {
        On = config.Bind(
            "Debug",
            "Solo Pair",
            false,
            "Development builds only. While alone in the room, play both Carrier and Rider: your mouse goes through the Rider's path (encoded, delayed, replayed) before it reaches your body. Also lets a solo Run depart.");
        Latency = config.Bind(
            "Debug",
            "Solo Pair Latency (ms)",
            100,
            new ConfigDescription(
                "Development builds only. How long Solo Pair holds the Rider's actions back, standing in for the network.",
                new AcceptableValueRange<int>(0, 1000)));
    }
}

/// <summary>Says when Solo Pair comes on or goes off, as its setting or the room changes.</summary>
internal sealed class SoloPairNotice : MonoBehaviour
{
    private bool announced;

    private void Update()
    {
        bool wanted = SoloPair.Wanted;
        if (wanted == announced)
        {
            return;
        }

        announced = wanted;
        CoopNotice.Info(wanted
            ? (Strings.Korean ? $"코옵: 1인 짝 테스트를 켰습니다. (마우스 지연 {SoloPair.Latency.Value}ms)" : $"Coop: Solo Pair is on (mouse delayed {SoloPair.Latency.Value} ms).")
            : (Strings.Korean ? "코옵: 1인 짝 테스트를 껐습니다." : "Coop: Solo Pair is off."));
    }
}
