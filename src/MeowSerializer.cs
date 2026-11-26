using Newtonsoft.Json;
using PushToMeowMod;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MeowxMeadow
{
    internal static class MeowSerializer
    {
        private static readonly HashSet<string> SentPlayersForCurrentMeow = new HashSet<string>();
        private static string lastSentJsonMeow = "";
        private static bool lastSentShareMeow = false;

        private static readonly Dictionary<string, Dictionary<string, CustomMeowDto>> ReceivedCustomMeows =
            new Dictionary<string, Dictionary<string, CustomMeowDto>>(StringComparer.Ordinal);

        public static void StoreReceivedCustomMeow(string steamId, string slugcatName, CustomMeowDto dto)
        {
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(slugcatName) || dto == null) return;

            if (!ReceivedCustomMeows.TryGetValue(steamId, out var bySlugcat))
            {
                bySlugcat = new Dictionary<string, CustomMeowDto>(StringComparer.Ordinal);
                ReceivedCustomMeows[steamId] = bySlugcat;
            }

            CustomMeowDto stored = dto.Copy();
            stored.VolumeMultiplier = stored.ResolveDefaultVolumeMultiplier(slugcatName);
            bySlugcat[slugcatName] = stored;
        }

        public static CustomMeowDto GetReceivedCustomMeow(string steamId, string slugcatName)
        {
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(slugcatName)) return null;

            if (ReceivedCustomMeows.TryGetValue(steamId, out var bySlugcat) &&
                bySlugcat.TryGetValue(slugcatName, out var dto))
            {
                ApplyRemapToCustomMeow(steamId, dto);
                return dto;
            }

            return null;
        }

        private static void ApplyRemapToCustomMeow(string steamId, CustomMeowDto dto)
        {
            if (string.IsNullOrEmpty(steamId) || dto?.SoundIds == null) return;

            foreach (MeowSoundType type in Enum.GetValues(typeof(MeowSoundType)))
            {
                string soundId = dto.GetSoundId(type);
                if (string.IsNullOrEmpty(soundId)) continue;

                string renamedId = MeowRegistration.GetActiveRemap(steamId, soundId);
                if (string.IsNullOrEmpty(renamedId)) continue;

                dto.SetSoundId(type, renamedId);
            }
        }

        public class MeadowHandshakeDTO
        {
            public string SteamId { get; set; }
            public string Slugcat { get; set; }
            public bool ShareMeow { get; set; }
            public List<string> RequiredSoundIds { get; set; } = new List<string>();
            public string MeowDtoJson { get; set; }
        }

        public static void BroadcastHandshake(string slugcatName)
        {
            try
            {
                if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable)
                {
                    SentPlayersForCurrentMeow.Clear();
                    lastSentJsonMeow = "";
                    return;
                }

                string jsonMeow = CustomMeowDto.SerializeLocalMeowDto(slugcatName);
                if (string.IsNullOrEmpty(jsonMeow))
                {
                    return;
                }

                // TODO: Conectar con opción de configuración cuando exista; por ahora true.
                bool localShareMeow = true;

                if (jsonMeow != lastSentJsonMeow || localShareMeow != lastSentShareMeow)
                {
                    lastSentJsonMeow = jsonMeow;
                    lastSentShareMeow = localShareMeow;
                    SentPlayersForCurrentMeow.Clear();
                }

                string payloadJson = BuildHandshakePayload(slugcatName, jsonMeow, localShareMeow);
                if (string.IsNullOrEmpty(payloadJson)) return;

                SentPlayersForCurrentMeow.RemoveWhere(id => !OnlineManager.players.Select(p => GetPlayerSteamId(p)).ToHashSet().Contains(id));

                foreach (var onlinePlayer in OnlineManager.players)
                {
                    if (onlinePlayer.isMe) continue;

                    string targetId = GetPlayerSteamId(onlinePlayer);
                    if (SentPlayersForCurrentMeow.Contains(targetId)) continue;

                    onlinePlayer.InvokeRPC(RPC_ReceiveHandshake, payloadJson);
                    SentPlayersForCurrentMeow.Add(targetId);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al emitir Handshake: {ex}");
            }
        }

        private static string BuildHandshakePayload(string slugcatName, string jsonMeow, bool localShareMeow)
        {
            if (jsonMeow == null)
            {
                jsonMeow = CustomMeowDto.SerializeLocalMeowDto(slugcatName);
            }
            if (string.IsNullOrEmpty(jsonMeow)) return null;

            var dto = JsonConvert.DeserializeObject<CustomMeowDto>(jsonMeow);
            var requiredSounds = new List<string>();

            if (dto != null)
            {
                foreach (MeowSoundType type in Enum.GetValues(typeof(MeowSoundType)))
                {
                    string soundId = dto.GetSoundId(type);
                    if (!string.IsNullOrEmpty(soundId) && !requiredSounds.Contains(soundId))
                    {
                        requiredSounds.Add(soundId);
                    }
                }
            }

            var handshake = new MeadowHandshakeDTO
            {
                SteamId = GetLocalSteamId(),
                Slugcat = slugcatName,
                ShareMeow = localShareMeow,
                RequiredSoundIds = requiredSounds,
                MeowDtoJson = jsonMeow
            };

            return JsonConvert.SerializeObject(handshake);
        }

        public static void SendHandshakeTo(OnlinePlayer target, string reason)
        {
            if (target == null || target.isMe) return;
            if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable) return;

            try
            {
                var (slugcatName, _) = Plugin.ResolveLocalSlugcatName();
                if (string.IsNullOrEmpty(slugcatName)) return;

                // TODO: Conectar con opción de configuración cuando exista; por ahora true.
                string payloadJson = BuildHandshakePayload(slugcatName, null, true);
                if (string.IsNullOrEmpty(payloadJson)) return;

                target.InvokeRPC(RPC_ReceiveHandshake, payloadJson);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error reenviando handshake a {target.id}: {ex.Message}");
            }
        }

        public static void RequestHandshakeFrom(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return;
            if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable) return;
            // TODO: Saltar si jugador está muteado

            var target = ResolvePlayerByIdentity(steamId);
            if (target == null || target.isMe)
            {
                return;
            }

            target.InvokeRPC(RPC_RequestHandshake, GetLocalSteamId());
            Plugin.Logger.LogInfo($"Handshake re-solicitado a {target.id}: si su ShareSkin está ON, se descargará su maullido.");
        }

        [SoftRPCMethod]
        public static void RPC_RequestHandshake(string requesterIdentity)
        {
            try
            {
                var requester = ResolvePlayerByIdentity(requesterIdentity);
                if (requester == null)
                {
                    return;
                }
                SendHandshakeTo(requester, "resolicitud recibida");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al procesar RPC_RequestHandshake: {ex}");
            }
        }

        private class HandshakeRequestState
        {
            public float LastTime;
            public int Attempts;
            public float RearmInterval;
        }

        private static readonly Dictionary<string, HandshakeRequestState> _handshakeRequests = 
            new Dictionary<string, HandshakeRequestState>(StringComparer.Ordinal);

        private const float HandshakeRequestCooldownSeconds = 10f;
        private const int MaxHandshakeRequests = 6;
        private const float HandshakeRequestRearmSeconds = 105f;
        private const float HandshakeRequestRearmJitter = 20f;

        public static void PollMissingHandshakes()
        {
            if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable) return;

            foreach (var onlinePlayer in new List<OnlinePlayer>(OnlineManager.players))
            {
                try
                {
                    if (onlinePlayer == null || onlinePlayer.isMe) continue;

                    string steamId = GetPlayerSteamId(onlinePlayer);
                    if (string.IsNullOrEmpty(steamId)) continue;
                    if (ReceivedCustomMeows.ContainsKey(steamId)) continue;
                    // TODO: Saltar si jugador está muteado
                    if (!IsSteamFriendAllowed(steamId)) continue;

                    if (!_handshakeRequests.TryGetValue(steamId, out var state))
                    {
                        _handshakeRequests[steamId] = new HandshakeRequestState
                        {
                            LastTime = Time.time,
                            RearmInterval = HandshakeRequestRearmSeconds + UnityEngine.Random.Range(-HandshakeRequestRearmJitter, HandshakeRequestRearmJitter)
                        };
                        continue;
                    }

                    float elapsedSinceLast = Time.time - state.LastTime;

                    if (state.Attempts >= MaxHandshakeRequests)
                    {
                        if (elapsedSinceLast < state.RearmInterval) continue;
                        state.Attempts = 0;
                        state.RearmInterval = HandshakeRequestRearmSeconds + UnityEngine.Random.Range(-HandshakeRequestRearmJitter, HandshakeRequestRearmJitter);
                    }
                    else
                    {
                        if (elapsedSinceLast < HandshakeRequestCooldownSeconds) continue;
                    }

                    state.LastTime = Time.time;
                    state.Attempts++;

                    onlinePlayer.InvokeRPC(RPC_RequestHandshake, GetLocalSteamId());
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error en poll de handshakes: {ex.Message}");
                }
            }
        }

        public static void ForgetPlayer(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return;
            _handshakeRequests.Remove(steamId);
            SentPlayersForCurrentMeow.Remove(steamId);
            ReceivedCustomMeows.Remove(steamId);
        }

        public static void ForgetAllPlayers()
        {
            ReceivedCustomMeows.Clear();
            SentPlayersForCurrentMeow.Clear();
            lastSentJsonMeow = "";
            lastSentShareMeow = false;
            _handshakeRequests.Clear();
        }

        [SoftRPCMethod]
        public static void RPC_ReceiveHandshake(string payloadJson)
        {
            try
            {
                var handshake = JsonConvert.DeserializeObject<MeadowHandshakeDTO>(payloadJson);
                if (handshake == null)
                {
                    Plugin.Logger.LogWarning("Recibido Handshake nulo o no válido.");
                    return;
                }

                var sender = ResolvePlayerByIdentity(handshake.SteamId);
                if (sender == null)
                {
                    return;
                }

                string senderSteamId = GetPlayerSteamId(sender);
                // TODO: return si jugador está muteado

                if (!IsSteamFriendAllowed(senderSteamId))
                {
                    return;
                }

                var dto = CustomMeowDto.DeserializeToCustomMeowDto(handshake.MeowDtoJson);
                
                if (dto != null && !string.IsNullOrEmpty(senderSteamId))
                {
                    StoreReceivedCustomMeow(senderSteamId, handshake.Slugcat, dto);

                    Plugin.Logger.LogInfo($"[DIAG/handshake-in] sender={senderSteamId} slugcat={handshake.Slugcat} shareMeow={handshake.ShareMeow} sounds={handshake.RequiredSoundIds?.Count ?? 0}");
                }

                // TODO: SkinTransfer.ForgetSenderState(sender);
                // TODO: Si handshake.ShareMeow es true, aquí pediremos los RequiredSoundIds faltantes.
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error procesando RPC_ReceiveHandshake: {ex}");
            }
        }

        public static string GetPlayerSteamId(OnlinePlayer player)
        {
            if (player?.id == null) return string.Empty;
            if (player.id is SteamMatchmakingManager.SteamPlayerId steamPlayerId) return steamPlayerId.steamID.m_SteamID.ToString();
            return player.id.ToString();
        }

        internal static OnlinePlayer ResolvePlayerByIdentity(string identity)
        {
            if (string.IsNullOrEmpty(identity)) return null;
            return OnlineManager.players.FirstOrDefault(p => GetPlayerSteamId(p) == identity);
        }

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

        internal static bool IsSteamFriendAllowed(string steamId)
        {
            // TODO: Conectar con opción de "Friends Only" cuando exista menú Remix.
            return true;
        }
    }
}
