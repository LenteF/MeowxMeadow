using PushToMeowMod;
using RainMeadow;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace MeowxMeadow
{
    public static class MeowSenderReceiver
    {
        private static void PushToMeowMain_DoMeow(Action<PushToMeowMain, Player, bool> orig, PushToMeowMain self, Player player, bool isShortMeow)
        {
            Plugin.Logger.LogInfo($"ENTER DoMeow | Player: {player?.playerState?.playerNumber} | Short: {isShortMeow} | Lobby: {OnlineManager.lobby != null}");
            orig(self, player, isShortMeow);
            Plugin.Logger.LogInfo($"EXIT orig | Player: {player?.playerState?.playerNumber}");

            if (OnlineManager.lobby == null || player == null)
            {
                return;
            }

            // Validar que el objeto pertenece al jugador local antes de transmitir por red
            if (OnlinePhysicalObject.map.TryGetValue(player.abstractPhysicalObject, out OnlinePhysicalObject onlinePlayer))
            {
                Plugin.Logger.LogInfo($"Resolved OPO | Player: {player.playerState.playerNumber} | OPO null: {onlinePlayer == null} | IsMine: {onlinePlayer?.isMine}");

                if (onlinePlayer.isMine)
                {
                    Plugin.Logger.LogInfo("Broadcasting legacy RPC");
                    LegacyRpcBridge.SendMeowToEveryone(onlinePlayer, isShortMeow);
                }
            }
        }

        [SoftRPCMethod]
        private static void ReceiveRemoteMeow(OnlinePhysicalObject onlinePlayer, bool isShortMeow)
        {
            if (onlinePlayer?.apo?.realizedObject is Player targetPlayer)
            {
                try
                {
                    PushToMeowMain instance = UnityEngine.Object.FindObjectOfType<PushToMeowMain>();
                    if (instance != null)
                    {
                        instance.DoMeow(targetPlayer, isShortMeow);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error al procesar maullido remoto: {ex.Message}");
                }
            }
        }
    }

    public static class LegacyRpcBridge
    {
        private static readonly Guid LegacyModuleGuid = new Guid("2578841e-dac6-445f-801a-838805167784");
        private static RPCManager.SoftRPCDefinition _legacyDefinition;
        private const bool LogLegacyAssemblyGuidOnStartup = false;

        public static void Initialize()
        {
            MethodInfo receiverMethod = typeof(MeowSenderReceiver).GetMethod("ReceiveRemoteMeow", BindingFlags.NonPublic | BindingFlags.Static);

            if (!RPCManager.defsByMethod.TryGetValue(receiverMethod, out RPCManager.RPCDefinition baseDef))
            {
                Plugin.Logger.LogError("[LegacyRpcBridge] No se encontró la definición base de ReceiveRemoteMeow en RPCManager.");
                return;
            }

            if (RPCManager.softHandlerClients.TryGetValue(LegacyModuleGuid, out var existingHandlers) && existingHandlers.ContainsKey(0))
            {
                if (baseDef is RPCManager.SoftRPCDefinition nativeSoftDef)
                {
                    _legacyDefinition = nativeSoftDef;
                    Plugin.Logger.LogWarning($"[LegacyRpcBridge] El GUID {LegacyModuleGuid} (índice 0) ya estaba ocupado. Usando la definición nativa de ReceiveRemoteMeow (índice {nativeSoftDef.index}) como respaldo.");
                }
                return;
            }

            _legacyDefinition = new RPCManager.SoftRPCDefinition
            {
                key = LegacyModuleGuid,
                index = 0,
                method = receiverMethod,
                serialize = baseDef.serialize,
                eventArgIndex = baseDef.eventArgIndex,
                isStatic = true,
                runDeferred = baseDef.runDeferred,
                security = baseDef.security,
                summary = "LegacyBridge.Meow"
            };

            if (!RPCManager.softHandlerClients.ContainsKey(LegacyModuleGuid))
            {
                RPCManager.softHandlerClients[LegacyModuleGuid] = new Dictionary<ushort, RPCManager.SoftRPCDefinition>();
            }
            RPCManager.softHandlerClients[LegacyModuleGuid][0] = _legacyDefinition;
            Plugin.Logger.LogInfo("[LegacyRpcBridge] Puente de compatibilidad con meadowMeow inicializado en índice 0.");
        }


        public static void LogLegacyAssemblyGuidIfEnabled()
        {
            if (!LogLegacyAssemblyGuidOnStartup) return;

            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string assemblyName = assembly.GetName().Name;
                    if (!string.IsNullOrEmpty(assemblyName) &&
                        assemblyName.IndexOf("meadowMeow", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Plugin.Logger.LogInfo($"[LegacyRpcBridge] MVID de '{assemblyName}': {assembly.ManifestModule.ModuleVersionId}");
                        return;
                    }
                }

                Plugin.Logger.LogWarning("[LegacyRpcBridge] No se encontró ningún ensamblado 'meadowMeow' cargado en el AppDomain.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[LegacyRpcBridge] Error al obtener el MVID de meadowMeow: {ex.Message}");
            }
        }

        public static void SendMeowToEveryone(OnlinePhysicalObject onlinePlayer, bool isShortMeow)
        {
            if (_legacyDefinition == null) return;

            try
            {
                Delegate dummyDel = Delegate.CreateDelegate(typeof(Action<OnlinePhysicalObject, bool>), _legacyDefinition.method);

                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (!player.isMe)
                    {
                        var legacyEvent = new SoftRPCEvent(_legacyDefinition, dummyDel, new object[] { onlinePlayer, isShortMeow });
                        player.QueueEvent(legacyEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[LegacyRpcBridge] Error al enviar RPC meadowMeow: {ex.Message}");
            }
        }
    }
}
