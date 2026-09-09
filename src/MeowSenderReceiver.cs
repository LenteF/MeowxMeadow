using PushToMeowMod;
using RainMeadow;
using System;

namespace MeowxMeadow
{
    public static class MeowSenderReceiver
    {
        private delegate void MeowRpcDelegate(OnlinePhysicalObject player, bool isShortMeow);

        private static void PushToMeowMain_DoMeow(Action<PushToMeowMain, Player, bool> orig, PushToMeowMain self, Player player, bool isShortMeow)
        {
            orig(self, player, isShortMeow);

            if (OnlineManager.lobby == null || player == null)
            {
                return;
            }

            if (OnlinePhysicalObject.map.TryGetValue(player.abstractPhysicalObject, out OnlinePhysicalObject onlinePlayer))
            {
                if (onlinePlayer.isMine)
                {
                    BroadcastMeow(onlinePlayer, isShortMeow);
                }
            }
        }

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
