#nullable enable
namespace BlueSage.QoLTweaks.Core;

internal static class VoiceWelcomePolicy
{
    internal static string Build(bool? voiceChatOn, bool? lobbyVoiceOn, bool? pushToTalk)
    {
        string mode = pushToTalk == true ? "Push to Talk" : pushToTalk == false ? "Voice Activation" : "not ready";
        if (voiceChatOn == false)
            return "Voice chat is Off in Settings; your saved mic mode is " + mode + ". Recheck Settings > Audio before enabling voice.";
        if (lobbyVoiceOn == false)
            return "Lobby voice is Off; your saved mic mode is " + mode + ". Recheck Settings > Audio if voice is enabled later.";
        if (pushToTalk == true)
            return "Lobby voice is " + (lobbyVoiceOn == true ? "On" : "starting") + " and Push to Talk is enabled; hold your mic key to speak.";
        if (pushToTalk == false)
            return "VOICE WARNING: lobby voice is " + (lobbyVoiceOn == true ? "On" : "starting") + " and Voice Activation is enabled, so your mic may be live. Check Settings > Audio or the mic icon.";
        return "Voice mode is still loading. Check Settings > Audio for Push to Talk vs Voice Activation before assuming your mic is closed.";
    }
}
