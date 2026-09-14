using Newtonsoft.Json;
using PushToMeowMod;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MeowxMeadow
{
    public class MeowHandshakeDTO
    {
        public string SteamId { get; set; }
        public string SlugcatClass { get; set; }
        public bool ShareMeow { get; set; }
        public string ActiveModId { get; set; }
    }
    internal static class MeowSerializer
    {
        private static readonly HashSet<string> SentPlayersForCurrentMeow = new HashSet<string>();
        private static string lastSentJsonPayload = "";

        // Mapeo directo: SteamID -> ModID del maullido activo para su Slugcat actual
        private static readonly Dictionary<string, string> ActivePlayerModIds = new Dictionary<string, string>(StringComparer.Ordinal);

        public static void StoreActiveModId(string steamId, string activeModId)
        {
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(activeModId)) return;
            ActivePlayerModIds[steamId] = activeModId;
        }

        public static string GetActiveModId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return null;
            ActivePlayerModIds.TryGetValue(steamId, out var modId);
            return modId;
        }

        public static void BroadcastHandshake(string localSlugcatClass)
        {
            try
            {
                if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable)
                {
                    SentPlayersForCurrentMeow.Clear();
                    lastSentJsonPayload = "";
                    return;
                }

                string payloadJson = BuildHandshakePayload(localSlugcatClass);
                if (string.IsNullOrEmpty(payloadJson)) return;

                if (payloadJson != lastSentJsonPayload)
                {
                    lastSentJsonPayload = payloadJson;
                    SentPlayersForCurrentMeow.Clear();
                }

                foreach (var onlinePlayer in OnlineManager.players)
                {
                    if (onlinePlayer.isMe) continue;

                    string targetId = GetPlayerSteamId(onlinePlayer);
                    if (SentPlayersForCurrentMeow.Contains(targetId)) continue;

                    onlinePlayer.InvokeRPC(RPC_ReceiveMeowHandshake, payloadJson);
                    SentPlayersForCurrentMeow.Add(targetId);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al emitir Meow Handshake: {ex}");
            }
        }

        private static string BuildHandshakePayload(string slugcatClass)
        {
            string localSteamId = GetLocalSteamId();
            string selectedModId = null;

            if (PlayerMeowProfileManager.Profiles.TryGetValue((localSteamId, slugcatClass), out var activeModId))
            {
                selectedModId = activeModId;
            }

            if (string.IsNullOrEmpty(selectedModId))
            {
                selectedModId = GetCurrentMeowModId(slugcatClass);
            }

            var handshake = new MeowHandshakeDTO
            {
                SteamId = localSteamId,
                SlugcatClass = slugcatClass,
                ShareMeow = true,
                ActiveModId = selectedModId
            };

            return JsonConvert.SerializeObject(handshake);
        }

        [SoftRPCMethod]
        public static void RPC_ReceiveMeowHandshake(string payloadJson)
        {
            try
            {
                var handshake = JsonConvert.DeserializeObject<MeowHandshakeDTO>(payloadJson);
                if (handshake == null || string.IsNullOrEmpty(handshake.SteamId)) return;

                StoreActiveModId(handshake.SteamId, handshake.ActiveModId);

                Plugin.Logger.LogInfo($"Handshake recibido de {handshake.SteamId} ({handshake.SlugcatClass}). Maullido activo: '{handshake.ActiveModId}'");

                if (string.IsNullOrEmpty(handshake.ActiveModId)) return;

                PlayerMeowProfileManager.RemoteProfiles[(handshake.SteamId, handshake.SlugcatClass)] = handshake.ActiveModId;

                if (!PlayerMeowProfileManager.Profiles.ContainsKey((handshake.SteamId, handshake.SlugcatClass)))
                {
                    if (!MeowRegistration.HasAudioPackLoadedLocally(handshake.ActiveModId, handshake.SlugcatClass) && handshake.ShareMeow)
                    {
                        Plugin.Logger.LogInfo($"ModID '{handshake.ActiveModId}' no encontrado en AllCustomMeows para '{handshake.SlugcatClass}'. Solicitando vía P2P...");
                        OnlinePlayer sender = ResolvePlayerByIdentity(handshake.SteamId);
                        if (sender != null)
                        {
                            // AudioTransfer.RequestAudioFromPlayer(sender, handshake.ActiveModId);
                        }
                    }

                    Plugin.Logger.LogInfo($"Perfil remoto para {handshake.SteamId} ({handshake.SlugcatClass}) asignado exitosamente al ModID local: '{handshake.ActiveModId}'");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al procesar RPC_ReceiveMeowHandshake: {ex}");
            }
        }

        private static string GetCurrentMeowModId(string slugcatClass)
        {
            try
            {
                if (MeowUtils.CustomMeows.TryGetValue(slugcatClass, out var customMeow))
                {
                    string filePath = customMeow.FilePath;

                    if (!string.IsNullOrEmpty(filePath))
                    {
                        DirectoryInfo pushToMeowDir = new DirectoryInfo(Path.GetDirectoryName(filePath));
                        DirectoryInfo modFolderPath = pushToMeowDir?.Parent;

                        if (modFolderPath != null)
                        {
                            return MeowRegistration.GetModId(modFolderPath.FullName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al obtener ModID nativo de fallback para '{slugcatClass}': {ex}");
            }

            return null;
        }

        public static void ForgetPlayer(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return;
            SentPlayersForCurrentMeow.Remove(steamId);
            ActivePlayerModIds.Remove(steamId);
        }

        public static void ClearAll()
        {
            SentPlayersForCurrentMeow.Clear();
            ActivePlayerModIds.Clear();
            lastSentJsonPayload = "";
        }

        /// <summary>
        /// Helper para extraer el identificador SteamID64 de un OnlinePlayer de Rain Meadow.
        /// </summary>
        public static string GetPlayerSteamId(OnlinePlayer player)
        {
            if (player?.id == null) return string.Empty;
            if (player.id is SteamMatchmakingManager.SteamPlayerId steamPlayerId) return steamPlayerId.steamID.m_SteamID.ToString();
            return player.id.ToString();
        }

        internal static string GetLocalSteamId()
        {
            try { return Steamworks.SteamUser.GetSteamID().m_SteamID.ToString(); }
            catch { return GetPlayerSteamId(OnlineManager.mePlayer); }
        }

        internal static OnlinePlayer ResolvePlayerByIdentity(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return null;
            return OnlineManager.players.FirstOrDefault(p => GetPlayerSteamId(p) == identity);
        }
    }
}
