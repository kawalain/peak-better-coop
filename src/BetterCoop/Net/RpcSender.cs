using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace BetterCoop.Net;

/// <summary>
/// The player who sent the game RPC running now, for the RPCs that take no
/// <see cref="PhotonMessageInfo"/> but must still be checked, such as
/// <c>CharacterData.RPC_SyncOnJoin</c>. Null outside an RPC, or when PUN no
/// longer has the method this watches; checks then let the call through.
/// </summary>
[HarmonyPatch]
internal static class RpcSender
{
    internal static Photon.Realtime.Player? Current { get; private set; }

    // PhotonNetwork.ExecuteRpc is internal to PUN, which is not publicized, so
    // it is found by name; a PUN update that drops it skips this patch rather
    // than failing the others.
    private static MethodBase? Target() => AccessTools.Method(typeof(PhotonNetwork), "ExecuteRpc");

    private static bool Prepare()
    {
        if (Target() == null)
        {
            Plugin.Log.LogWarning("PUN has no ExecuteRpc to read RPC senders from; RPCs without a sender are not checked.");
            return false;
        }

        return true;
    }

    private static MethodBase TargetMethod() => Target()!;

    private static void Prefix(Photon.Realtime.Player sender, out Photon.Realtime.Player? __state)
    {
        __state = Current;
        Current = sender;
    }

    private static void Finalizer(Photon.Realtime.Player? __state) => Current = __state;
}
