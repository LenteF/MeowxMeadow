using PushToMeowMod;
using RainMeadow;
using System;

namespace MeowxMeadow
{
    /// <summary>
    /// Gestiona la transmisión (RPC) y recepción de eventos de maullidos a través de la red de Rain Meadow.
    /// </summary>
    public static class MeowSenderReceiver
    {
        private delegate void MeowRpcDelegate(OnlinePhysicalObject player, bool isShortMeow);

        /// <summary>
        /// Intercepta la llamada local de DoMeow. Si la entidad nos pertenece, emite la orden por RPC a los demás jugadores.
        /// </summary>
        private static void PushToMeowMain_DoMeow(Action<PushToMeowMain, Player, bool> orig, PushToMeowMain self, Player player, bool isShortMeow)
        {
            orig(self, player, isShortMeow);

            if (OnlineManager.lobby == null || player == null)
            {
                return;
            }

            // Validar que el objeto pertenece al jugador local antes de transmitir por red
            if (OnlinePhysicalObject.map.TryGetValue(player.abstractPhysicalObject, out OnlinePhysicalObject onlinePlayer))
            {
                if (onlinePlayer.isMine)
                {
                    BroadcastMeow(onlinePlayer, isShortMeow);
                }
            }
        }

        /// <summary>
        /// Envía una llamada RPC (InvokeOnceRPC) a todos los jugadores de la sala excepto a nosotros mismos.
        /// </summary>
        private static void BroadcastMeow(OnlinePhysicalObject onlinePlayer, bool isShortMeow)
        {
            try
            {
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (!player.isMe)
                    {
                        player.InvokeOnceRPC(new MeowRpcDelegate(ReceiveRemoteMeow), onlinePlayer, isShortMeow);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al transmitir RPC de maullido: {ex.Message}");
            }
        }

        /// <summary>
        /// Método RPC receptor invocado de forma remota cuando otro jugador maulla en la sala.
        /// </summary>
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
}
