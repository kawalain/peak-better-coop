using HarmonyLib;

namespace PeakCoop.Features;

/// <summary>
/// A Pair lives and dies as one: when one member dies, the client that owns
/// the other member kills them too. The game's checkpoints still apply.
/// </summary>
[HarmonyPatch]
internal static class SharedDeath
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Character), nameof(Character.RPCA_Die))]
    private static void PartnerDiesToo(Character __instance)
    {
        if (!PairRoster.TryGetPartner(__instance, out Character partner)
            || !partner.IsLocal
            || partner.data.dead)
        {
            return;
        }

        Plugin.Verbose($"{__instance.characterName} died; their partner dies with them.");
        partner.DieInstantly();
    }
}
