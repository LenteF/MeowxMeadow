using RainMeadow;
using System;

namespace MeowxMeadow
{
    internal static class MeowSerializer
    {
        public static string GetLocalSteamId()
        {
            try
            {
                return Steamworks.SteamUser.GetSteamID().m_SteamID.ToString();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[MeowFancyUI] Steam no disponible ({ex.Message}); usando identidad de RainMeadow.");
                return MeowSerializer.GetPlayerSteamId(OnlineManager.mePlayer);
            }
        }
        public static string GetPlayerSteamId(OnlinePlayer player)
        {
            if (player?.id == null) return string.Empty;
            if (player.id is SteamMatchmakingManager.SteamPlayerId steamPlayerId) return steamPlayerId.steamID.m_SteamID.ToString();
            return player.id.ToString();
        }
    }
}
