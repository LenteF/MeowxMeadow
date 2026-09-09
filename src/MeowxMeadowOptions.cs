using PushToMeowMod;
using RainMeadow;
using System;
using System.Collections.Generic;

namespace MeowxMeadow
{
    [Flags]
    public enum MeowRuleFlags
    {
        None                = 0,
        AlertCreatures      = 1 << 0,
        DrainLungs          = 1 << 1,
        CanPanicMeow        = 1 << 2,
        DoOraclesReact      = 1 << 3,
        SlugpupPanic        = 1 << 4,
        SlugpupHungry       = 1 << 5,
        SlugpupAlert        = 1 << 6,
        SlugpupStunNone     = 1 << 7,
        SlugpupStunBlunt    = 1 << 8
    }

    public class MeowxMeadowOptions
    {
        public static MeowxMeadowOptions Instance { get; } = new MeowxMeadowOptions();
        private MeowRuleFlags _localCache = MeowRuleFlags.None;
        public bool IsUsingRemoteFlags { get; private set; } = false;

        private static (Dictionary<string, bool> hostBoolSettings, Dictionary<string, float> hostFloatSettings, Dictionary<string, int> hostIntSettings) OnlineGameMode_GetHostRemixSettings(Func<OnlineGameMode, (Dictionary<string, bool> hostBoolSettings, Dictionary<string, float> hostFloatSettings, Dictionary<string, int> hostIntSettings)> orig, OnlineGameMode mode)
        {
            var result = orig(mode);

            try
            {
                int hostOptionsBits = (int)Instance.CaptureLocalFlags();
                result.hostIntSettings["MeowxMeadowOptions"] = hostOptionsBits;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al inyectar las opciones de Remix del Host: {ex.Message}");
            }

            return result;
        }

        private MeowRuleFlags CaptureLocalFlags()
        {
            MeowRuleFlags state = MeowRuleFlags.None;

            if (PushToMeowMain.ModSettings.AlertCreatures.Value)        state |= MeowRuleFlags.AlertCreatures;
            if (PushToMeowMain.ModSettings.DrainLungs.Value)            state |= MeowRuleFlags.DrainLungs;
            if (PushToMeowMain.ModSettings.CanPanicMeow.Value)          state |= MeowRuleFlags.CanPanicMeow;
            if (PushToMeowMain.ModSettings.DoOraclesReact.Value)        state |= MeowRuleFlags.DoOraclesReact;
            if (PushToMeowMain.ModSettings.SlugpupPanicMeow.Value)      state |= MeowRuleFlags.SlugpupPanic;
            if (PushToMeowMain.ModSettings.SlugpupHungryMeow.Value)     state |= MeowRuleFlags.SlugpupHungry;
            if (PushToMeowMain.ModSettings.SlugpupAlertMeow.Value)      state |= MeowRuleFlags.SlugpupAlert;
            if (PushToMeowMain.ModSettings.SlugpupStunNoneMeow.Value)   state |= MeowRuleFlags.SlugpupStunNone;
            if (PushToMeowMain.ModSettings.SlugpupStunBluntMeow.Value)  state |= MeowRuleFlags.SlugpupStunBlunt;

            return state;
        }

        public void ApplyRemoteFlags(MeowRuleFlags remoteFlags)
        {
            _localCache = CaptureLocalFlags();

            ApplyFlagsToSettings(remoteFlags);
            IsUsingRemoteFlags = true;

            Plugin.Logger.LogInfo($"Configuración del Host aplicada correctamente: {remoteFlags}");
        }

        public void RestoreLocalState()
        {
            if (!IsUsingRemoteFlags) return;

            ApplyFlagsToSettings(_localCache);
            IsUsingRemoteFlags = false;

            Plugin.Logger.LogInfo("Configuración local restaurada.");
        }

        private void ApplyFlagsToSettings(MeowRuleFlags flags)
        {
            PushToMeowMain.ModSettings.AlertCreatures.Value         = flags.HasFlag(MeowRuleFlags.AlertCreatures);
            PushToMeowMain.ModSettings.DrainLungs.Value             = flags.HasFlag(MeowRuleFlags.DrainLungs);
            PushToMeowMain.ModSettings.CanPanicMeow.Value           = flags.HasFlag(MeowRuleFlags.CanPanicMeow);
            PushToMeowMain.ModSettings.DoOraclesReact.Value         = flags.HasFlag(MeowRuleFlags.DoOraclesReact);
            PushToMeowMain.ModSettings.SlugpupPanicMeow.Value       = flags.HasFlag(MeowRuleFlags.SlugpupPanic);
            PushToMeowMain.ModSettings.SlugpupHungryMeow.Value      = flags.HasFlag(MeowRuleFlags.SlugpupHungry);
            PushToMeowMain.ModSettings.SlugpupAlertMeow.Value       = flags.HasFlag(MeowRuleFlags.SlugpupAlert);
            PushToMeowMain.ModSettings.SlugpupStunNoneMeow.Value    = flags.HasFlag(MeowRuleFlags.SlugpupStunNone);
            PushToMeowMain.ModSettings.SlugpupStunBluntMeow.Value   = flags.HasFlag(MeowRuleFlags.SlugpupStunBlunt);
        }
    }
}
