using BepInEx;
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using PushToMeowMod;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MeowxMeadow
{
    [BepInPlugin("meowxmeadow", "MeowxMeadow", "1.3.1")]
    [BepInDependency("henpemaz.rainmeadow", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("pushtomeow", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static new ManualLogSource Logger;

        public static SoundLoader ActiveSoundLoader { get; private set; }

        // --- DICCIONARIOS DE ESTADO ONLINE (STEAM ID -> DATO) ---
        public Dictionary<string, bool> OnlinePlayersMeowButtonLastState = new Dictionary<string, bool>();
        public Dictionary<string, MeowState> OnlinePlayersMeowingState = new Dictionary<string, MeowState>();
        public Dictionary<string, float> OnlinePlayersLastMeowTime = new Dictionary<string, float>();
        public Dictionary<string, Player> OnlinePlayersLookup = new Dictionary<string, Player>();
        public Dictionary<Player, string> OnlinePlayerToKeyLookup = new Dictionary<Player, string>();

        private Hook HandleJoinHook;
        private Hook HandleDisconnectHook;
        private Hook LeaveLobbyHook;
        private Hook UpdateHook;
        private Hook HandleMeowInputHook;
        private Hook DoMeowHook;
        private Hook FindMeowSoundIDHook;
        private Hook GetHostRemixSettingsHook;

        private bool isInit = false;

        public void Awake()
        {
            Instance = this;
            Logger = base.Logger;

            try
            {
                On.RainWorld.OnModsInit += OnModsInit;
                On.RainWorld.Update += RainWorld_Update;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Initialization error: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }

        private void OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            try
            {
                MeowRegistration.LoadAllActiveCustomMeows();

                if (isInit) return;
                isInit = true;

                Plugin.Logger.LogInfo("Build 1.3.1 (25/10/2026: optimización en la obtención de un SteamID por cada objeto Player). Si NO ves esta línea, se está cargando un DLL viejo.");

                InitializeHooks();

                PlayerMeowProfileManager.LoadProfiles();
                MeowRegistration.LoadAllCustomMeows();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in OnModsInit: {ex.Message}");
                Logger.LogError(ex.StackTrace);
            }
        }

        private void InitializeHooks()
        {
            try
            {
                On.SoundLoader.LoadSounds += SoundLoader_LoadSounds;

                On.RoomCamera.SpriteLeaser.ctor += SpriteLeaser_Ctor_Hook;

                MethodInfo handleJoinMethod = typeof(MatchmakingManager).GetMethod("HandleJoin", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo handleJoinHook = typeof(Plugin).GetMethod("MatchmakingManager_HandleJoin", BindingFlags.NonPublic | BindingFlags.Static);
                if (handleJoinMethod != null && handleJoinHook != null)
                    this.HandleJoinHook = new Hook(handleJoinMethod, handleJoinHook);

                MethodInfo handleDisconnectMethod = typeof(MatchmakingManager).GetMethod("HandleDisconnect", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo handleDisconnectHook = typeof(Plugin).GetMethod("MatchmakingManager_HandleDisconnect", BindingFlags.NonPublic | BindingFlags.Static);
                if (handleDisconnectMethod != null && handleDisconnectHook != null)
                    this.HandleDisconnectHook = new Hook(handleDisconnectMethod, handleDisconnectHook);


                MatchmakingManager.OnLobbyJoined += MatchmakingManager_OnLobbyJoined;

                MethodInfo leaveLobbyMethod = typeof(OnlineManager).GetMethod("LeaveLobby", BindingFlags.Public | BindingFlags.Static);
                MethodInfo leaveLobbyHook = typeof(Plugin).GetMethod("OnlineManager_LeaveLobby", BindingFlags.NonPublic | BindingFlags.Static);
                if (leaveLobbyMethod != null && leaveLobbyHook != null)
                    this.LeaveLobbyHook = new Hook(leaveLobbyMethod, leaveLobbyHook);

                MethodInfo updateMethod = typeof(PushToMeowMain).GetMethod("Update", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo updateHook = typeof(Plugin).GetMethod("PushToMeowMain_Update", BindingFlags.NonPublic | BindingFlags.Static);
                if (updateMethod != null && updateHook != null)
                    this.UpdateHook = new Hook(updateMethod, updateHook);

                MethodInfo handleMeowInputMethod = typeof(PushToMeowMain).GetMethod("HandleMeowInput", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo handleMeowInputHook = typeof(Plugin).GetMethod("PushToMeowMain_HandleMeowInput", BindingFlags.NonPublic | BindingFlags.Static);
                if (handleMeowInputMethod != null && handleMeowInputHook != null)
                    this.HandleMeowInputHook = new Hook(handleMeowInputMethod, handleMeowInputHook);

                MethodInfo doMeowMethod = typeof(PushToMeowMain).GetMethod("DoMeow", BindingFlags.Public | BindingFlags.Instance);
                MethodInfo doMeowHook = typeof(MeowSenderReceiver).GetMethod("PushToMeowMain_DoMeow", BindingFlags.NonPublic | BindingFlags.Static);
                if (doMeowMethod != null && doMeowHook != null)
                    this.DoMeowHook = new Hook(doMeowMethod, doMeowHook);

                MethodInfo findMeowSoundIDMethod = typeof(MeowUtils).GetMethod("FindMeowSoundID", BindingFlags.Public | BindingFlags.Static);
                MethodInfo findMeowSoundIDHook = typeof(PlayerMeowProfileManager).GetMethod("MeowUtils_FindMeowSoundID", BindingFlags.NonPublic | BindingFlags.Static);
                if (findMeowSoundIDMethod != null && findMeowSoundIDHook != null)
                    this.FindMeowSoundIDHook = new Hook(findMeowSoundIDMethod, findMeowSoundIDHook);

                MethodInfo getHostRemixSettingsMethod = typeof(OnlineGameMode).GetMethod("GetHostRemixSettings", BindingFlags.Public | BindingFlags.Static);
                MethodInfo getHostRemixSettingsHook = typeof(MeowxMeadowOptions).GetMethod("OnlineGameMode_GetHostRemixSettings", BindingFlags.NonPublic | BindingFlags.Static);
                if (getHostRemixSettingsMethod != null && getHostRemixSettingsHook != null)
                    this.GetHostRemixSettingsHook = new Hook(getHostRemixSettingsMethod, getHostRemixSettingsHook);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Hook application error: {ex.Message}");
            }
        }

        private static void SoundLoader_LoadSounds(On.SoundLoader.orig_LoadSounds orig, SoundLoader self)
        {
            orig(self);

            ActiveSoundLoader = self;

            if (self.allAudio != null && self.allAudio.Length > 0)
            {
                Logger.LogInfo("🔊 Guardada la referencia global a SoundLoader.");
                MeowRegistration.InjectInactiveLocalMeows(self);
            }
        }

        private static void SpriteLeaser_Ctor_Hook(On.RoomCamera.SpriteLeaser.orig_ctor orig, RoomCamera.SpriteLeaser sLeaser, IDrawable obj, RoomCamera rCam)
        {
            orig(sLeaser, obj, rCam);

            try
            {
                if (obj is not PlayerGraphics playerGraphics) return;
                if (playerGraphics?.player == null) return;

                var onlineEntity = playerGraphics.player.abstractCreature.GetOnlineObject();
                if (onlineEntity == null || !onlineEntity.isMine) return;

                if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable) return;

                string slugcatName = playerGraphics.player.slugcatStats.name.value;
                MeowSerializer.BroadcastHandshake(slugcatName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en Hook SpriteLeaser_ctor (Meow): {ex.Message}");
            }
        }

        private string LastEmittedLobbySlugcat = null;

        private static bool RandomSlugcatSkipLogged = false;

        internal static (string Slugcat, string Mode) ResolveLocalSlugcatName()
        {
            var currentMainLoop = RWCustom.Custom.rainWorld?.processManager?.currentMainLoop;

            if (currentMainLoop is RainWorldGame rainWorldGame)
            {
                string realized = rainWorldGame.FirstRealizedPlayer?.slugcatStats.name.value;
                return (realized, realized == null ? null : "partida");
            }

            if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable) return (null, null);

            if (OnlineManager.lobby.gameMode is ArenaOnlineGameMode)
            {
                var playingAs = ArenaHelpers.GetArenaClientSettings(OnlineManager.mePlayer)?.playingAs;
                if (playingAs == null || playingAs == RainMeadow.RainMeadow.Ext_SlugcatStatsName.OnlineRandomSlugcat || playingAs == RainMeadow.RainMeadow.Ext_SlugcatStatsName.OnlineOverseerSpectator)
                {
                    if (playingAs == RainMeadow.RainMeadow.Ext_SlugcatStatsName.OnlineRandomSlugcat && !RandomSlugcatSkipLogged)
                    {
                        RandomSlugcatSkipLogged = true;
                    }
                    return (null, "arena");
                }
                return (playingAs.value, "arena");
            }

            if (OnlineManager.lobby.gameMode is StoryGameMode storyGameMode)
            {
                string slugcat = storyGameMode.preferredSlug?.value ?? storyGameMode.currentCampaign?.value;
                return (slugcat, "historia");
            }

            return (null, null);
        }

        private static void MatchmakingManager_HandleJoin(
            Action<MatchmakingManager, OnlinePlayer> orig,
            MatchmakingManager self,
            OnlinePlayer player)
        {
            orig(self, player);

            try
            {
                if (player == null || player.isMe) return;
                if (OnlineManager.lobby == null || !OnlineManager.lobby.isAvailable) return;

                var (slugcatName, mode) = ResolveLocalSlugcatName();
                if (string.IsNullOrEmpty(slugcatName)) return;
                MeowSerializer.BroadcastHandshake(slugcatName);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en HandleJoin (Meow): {ex.Message}");
            }
        }

        private static void MatchmakingManager_HandleDisconnect(
            Action<MatchmakingManager, OnlinePlayer> orig,
            MatchmakingManager self,
            OnlinePlayer player)
        {
            orig(self, player);

            try
            {
                if (player == null) return;

                string steamId = MeowSerializer.GetPlayerSteamId(player);
                if (string.IsNullOrEmpty(steamId)) return;
                MeowSerializer.ForgetPlayer(steamId);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en HandleDisconnect (Meow): {ex.Message}");
            }
        }

        private static void MatchmakingManager_OnLobbyJoined(bool ok, string error)
        {
            if (!ok || OnlineManager.mePlayer == OnlineManager.lobby.owner)
            {
                return;
            }

            // Iniciar corrutina de espera para descargar y aplicar las reglas del Host
            Instance.StartCoroutine(TrySyncHostOptions());
        }

        /// <summary>
        /// Corrutina que intenta durante 5 segundos leer los datos comprimidos (bits) de configuración del Host.
        /// </summary>
        public static System.Collections.IEnumerator TrySyncHostOptions()
        {
            float timeout = 5f;
            float elapsed = 0f;
            bool synced = false;

            while (elapsed < timeout)
            {
                if (OnlineManager.lobby == null) yield break;

                var configurableInts = OnlineManager.lobby.configurableInts;

                if (configurableInts != null && configurableInts.TryGetValue("MeowxMeadowOptions", out int remoteOptionsBits))
                {
                    MeowxMeadowOptions.Instance.ApplyRemoteFlags((MeowRuleFlags)remoteOptionsBits);
                    Plugin.Logger.LogInfo($"Opciones del Host sincronizadas con éxito: {remoteOptionsBits}");
                    synced = true;
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!synced)
            {
                Plugin.Logger.LogWarning("El Host no envió configuraciones de MeowxMeadowOptions tras 5 segundos de espera.");
            }
        }

        /// <summary>
        /// Restaura la configuración Remix local del jugador al abandonar la sala multijugador.
        /// </summary>
        private static void OnlineManager_LeaveLobby(Action orig)
        {
            bool wasInLobby = OnlineManager.lobby != null;
            orig();

            if (!wasInLobby) return;

            try
            {
                MeowSerializer.ClearAll();
                MeowxMeadowOptions.Instance.RestoreLocalState();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en LeaveLobby (Meow): {ex.Message}");
            }
        }

        private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
        {
            orig(self);

            try
            {
                if (OnlineManager.lobby != null && OnlineManager.lobby.isAvailable)
                {
                    if (RWCustom.Custom.rainWorld?.processManager?.currentMainLoop is not RainWorldGame)
                    {
                        var (lobbySlugcat, _) = ResolveLocalSlugcatName();
                        if (lobbySlugcat != null)
                        {
                            if (lobbySlugcat != LastEmittedLobbySlugcat)
                            {
                                LastEmittedLobbySlugcat = lobbySlugcat;
                                MeowSerializer.BroadcastHandshake(lobbySlugcat);
                            }
                        }
                        else
                        {
                            LastEmittedLobbySlugcat = null;
                        }
                    }
                }
                else
                {
                    LastEmittedLobbySlugcat = null;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error en RainWorld_Update (Meow): {ex.Message}");
            }
        }

        private static void PushToMeowMain_Update(Action<PushToMeowMain> orig, PushToMeowMain self)
        {
            if (OnlineManager.lobby == null)
            {
                orig(self);
                return;
            }

            foreach (KeyValuePair<string, bool> item in Instance.OnlinePlayersMeowButtonLastState)
            {
                string steamId = item.Key;
                bool isPressed = item.Value;

                if (!Instance.OnlinePlayersLastMeowTime.TryGetValue(steamId, out float lastTime)) continue;
                if (!Instance.OnlinePlayersMeowingState.TryGetValue(steamId, out MeowState meowState)) continue;
                if (!Instance.OnlinePlayersLookup.TryGetValue(steamId, out Player player)) continue;

                float timeDiff = Time.time - lastTime;

                // Si lleva más de 0.14s presionado, emitir maullido largo
                if (isPressed && timeDiff > 0.14f && meowState == MeowState.NotMeowed)
                {
                    self.DoMeow(player);
                    Instance.OnlinePlayersMeowingState[steamId] = MeowState.MeowedLong;
                }
            }
        }

        private static void PushToMeowMain_HandleMeowInput(Action<PushToMeowMain, On.Player.orig_Update, Player, bool> orig, PushToMeowMain self, On.Player.orig_Update origUpdate, Player player, bool wtfIsThisBool)
        {
            if (OnlineManager.lobby == null || player == null || player.isNPC)
            {
                orig(self, origUpdate, player, wtfIsThisBool);
                return;
            }

            origUpdate(player, wtfIsThisBool);
            try
            {
                if (!OnlinePhysicalObject.map.TryGetValue(player.abstractPhysicalObject, out OnlinePhysicalObject onlinePhysicalObject))
                {
                    return;
                }

                OnlinePlayer onlinePlayer = onlinePhysicalObject.owner;
                if (onlinePlayer == null) return;

                if (!Instance.OnlinePlayerToKeyLookup.TryGetValue(player, out string steamId) || string.IsNullOrEmpty(steamId))
                {
                    steamId = MeowSerializer.GetPlayerSteamId(onlinePlayer);
                    if (string.IsNullOrEmpty(steamId)) return;
                }

                // Inicializar entradas del jugador si es la primera vez que se detecta en la sala
                if (!Instance.OnlinePlayersMeowButtonLastState.ContainsKey(steamId))
                {
                    Instance.OnlinePlayersMeowButtonLastState.Add(steamId, value: false);
                }

                if (!Instance.OnlinePlayersLastMeowTime.ContainsKey(steamId))
                {
                    Instance.OnlinePlayersLastMeowTime.Add(steamId, 0f);
                }

                if (!Instance.OnlinePlayersMeowingState.ContainsKey(steamId))
                {
                    Instance.OnlinePlayersMeowingState.Add(steamId, MeowState.NotMeowed);
                }

                if (Instance.OnlinePlayersLookup.TryGetValue(steamId, out Player oldPlayer) && oldPlayer != player)
                {
                    if (oldPlayer != null)
                    {
                        Instance.OnlinePlayerToKeyLookup.Remove(oldPlayer);
                    }
                }

                // Guardar mapeos en diccionarios
                Instance.OnlinePlayersLookup[steamId] = player;
                Instance.OnlinePlayerToKeyLookup[player] = steamId;

                bool isKeyPressed = false;

                if (onlinePlayer.isMe)
                {
                    isKeyPressed = PushToMeowMain.Meow.CheckRawPressed(player.playerState.playerNumber);
                }

                bool lastState = Instance.OnlinePlayersMeowButtonLastState[steamId];
                float lastMeowTime = Instance.OnlinePlayersLastMeowTime[steamId];
                MeowState currentState = Instance.OnlinePlayersMeowingState[steamId];

                // Detección de pulsación rápida vs mantenida
                if (isKeyPressed && !lastState)
                {
                    if (Time.time - lastMeowTime >= 0.24f)
                    {
                        Instance.OnlinePlayersMeowButtonLastState[steamId] = true;
                        Instance.OnlinePlayersLastMeowTime[steamId] = Time.time;
                        Instance.OnlinePlayersMeowingState[steamId] = MeowState.NotMeowed;
                    }
                }
                else if (!isKeyPressed && lastState)
                {
                    // Si se soltó en menos de 0.14s, emitir maullido corto
                    if (Time.time - lastMeowTime <= 0.14f && currentState == MeowState.NotMeowed)
                    {
                        self.DoMeow(player, isShortMeow: true);
                        Instance.OnlinePlayersMeowingState[steamId] = MeowState.MeowedShort;
                    }

                    Instance.OnlinePlayersMeowButtonLastState[steamId] = false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Error al procesar HandleMeowInput en Meadow: " + ex);
            }
        }

        public void OnDestroy()
        {
            On.RainWorld.OnModsInit -= OnModsInit;
            On.RainWorld.Update -= RainWorld_Update;
            On.SoundLoader.LoadSounds -= SoundLoader_LoadSounds;
            On.RoomCamera.SpriteLeaser.ctor -= SpriteLeaser_Ctor_Hook;
            MatchmakingManager.OnLobbyJoined -= MatchmakingManager_OnLobbyJoined;

            OnlinePlayersMeowButtonLastState.Clear();
            OnlinePlayersMeowingState.Clear();
            OnlinePlayersLastMeowTime.Clear();
            OnlinePlayersLookup.Clear();
            OnlinePlayerToKeyLookup.Clear();

            PlayerMeowProfileManager.ClearAll();
            MeowSerializer.ClearAll();

            HandleJoinHook?.Dispose();
            HandleDisconnectHook?.Dispose();
            LeaveLobbyHook?.Dispose();
            UpdateHook?.Dispose();
            HandleMeowInputHook?.Dispose();
            DoMeowHook?.Dispose();
            FindMeowSoundIDHook?.Dispose();
            GetHostRemixSettingsHook?.Dispose();
        }
    }
}
