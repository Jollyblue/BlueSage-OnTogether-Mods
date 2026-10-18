using HarmonyLib;
using PurrLobby;
using PurrNet;
using TMPro;

namespace BlueSagePatched.PlayerLimitLift.Patches;

[HarmonyPatch(typeof(PlayerPanelController))]
internal static class PanelPatch
{
    [HarmonyPatch("UpdateServerPanel")]
    [HarmonyPostfix]
    private static void SizeMarker()
    {
        LobbyManager lobbyManager = MonoSingleton<MultiplayerManager>.I?._lobbyManager;
        if (lobbyManager?.CurrentLobby == null || NetworkSingleton<PlayerPanelController>.I == null)
        {
            return;
        }

        TextMeshProUGUI lobbyNameText = AccessTools
            .FieldRefAccess<PlayerPanelController, TextMeshProUGUI>("_lobbyNameText")
            .Invoke(NetworkSingleton<PlayerPanelController>.I);

        if (lobbyNameText == null)
        {
            return;
        }

        int visibleMax = lobbyManager.CurrentLobby.MaxPlayers - 1;
        int memberCount = lobbyManager.CurrentLobby.Members.Count;
        lobbyNameText.text = $"{MonoSingleton<MultiplayerManager>.I.LobbyName} ({memberCount}/{visibleMax})";
    }
}
