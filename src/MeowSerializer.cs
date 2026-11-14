using RainMeadow;

namespace MeowxMeadow
{
    internal static class MeowSerializer
    {
        public static string GetPlayerSteamId(OnlinePlayer player)
        {
            if (player?.id == null) return string.Empty;
            if (player.id is SteamMatchmakingManager.SteamPlayerId steamPlayerId) return steamPlayerId.steamID.m_SteamID.ToString();
            return player.id.ToString();
        }
    }
}
