using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MeowxMeadow
{
    public static class MeowRegistration
    {
        private static readonly Regex ValidSoundIdentifierRegex =
            new Regex("^[a-zA-Z0-9_.'-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsValidSoundIdentifier(string value) => !string.IsNullOrEmpty(value) && ValidSoundIdentifierRegex.IsMatch(value);

        // ===================================================================
        // CACHÉ EN MEMORIA POR EMISOR (PENDIENTE DE TRANSFERENCIA DE ARCHIVOS)
        // ===================================================================

        // TODO: Añadir caché en memoria de archivos de sonido recibidos por emisor (senderSteamId -> soundId -> archivos)
        // TODO: Añadir helpers CacheSoundInMemory y HasSoundInMemory cuando se implemente MeowTransfer

        // ===================================================================
        // RENOMBRADO DEL IDY REMAPEO POR EMISOR
        // ===================================================================
        private const string MeowxmSoundPrefix = "meowxm";

        private static readonly Dictionary<string, Dictionary<string, string>> MemoryRemapBySender =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        private static string SanitizeSteamId(string steamId)
        {
            if (string.IsNullOrEmpty(steamId)) return "unknown";

            var chars = new List<char>(steamId.Length);
            foreach (char c in steamId)
            {
                if (char.IsLetterOrDigit(c)) chars.Add(c);
            }

            if (chars.Count == 0) return "unknown";

            int maxLen = Math.Min(chars.Count, 32);
            return new string(chars.GetRange(0, maxLen).ToArray());
        }

        public static string ComputeRenamedSoundId(string senderSteamId, string soundId) => $"{MeowxmSoundPrefix}_{SanitizeSteamId(senderSteamId)}_{soundId}";

        public static string GetActiveRemap(string senderSteamId, string soundId)
        {
            if (string.IsNullOrEmpty(senderSteamId) || string.IsNullOrEmpty(soundId)) return null;

            if (MemoryRemapBySender.TryGetValue(senderSteamId, out var remaps) &&
                remaps.TryGetValue(soundId, out string renamedId))
            {
                return renamedId;
            }

            return null;
        }

        public static string GetOrCreateRemap(string senderSteamId, string soundId)
        {
            if (string.IsNullOrEmpty(senderSteamId) || string.IsNullOrEmpty(soundId))
            {
                return soundId;
            }

            if (!MemoryRemapBySender.TryGetValue(senderSteamId, out var remaps))
            {
                remaps = new Dictionary<string, string>(StringComparer.Ordinal);
                MemoryRemapBySender[senderSteamId] = remaps;
            }

            if (remaps.TryGetValue(soundId, out string existing)) return existing;

            string renamedId = ComputeRenamedSoundId(senderSteamId, soundId);
            remaps[soundId] = renamedId;
            return renamedId;
        }

        // ===================================================================
        // REGISTRO EN MEMORIA DEL SOUNDID / AUDIO (FUTURO)
        // ===================================================================

        // TODO: Llevar registro de SoundIDs/clips creados en memoria
        // TODO: Implementar RegisterReceivedSound(...) que cree/registre el SoundID (ExtEnum) con renamedId y enlace su AudioClip cuando llegue la transferencia de archivos

        public static int WipeAllMemory()
        {
            int clearedRemaps = 0;
            foreach (var bySender in MemoryRemapBySender.Values)
            {
                clearedRemaps += bySender.Count;
            }

            MemoryRemapBySender.Clear();

            // TODO: Descargar AudioClips / SoundIDs registrados en memoria y limpiar caché de archivos

            return clearedRemaps;
        }

        public static int ForgetSender(string senderSteamId)
        {
            if (string.IsNullOrEmpty(senderSteamId)) return 0;

            int removed = 0;
            if (MemoryRemapBySender.TryGetValue(senderSteamId, out var remaps))
            {
                removed += remaps.Count;
                MemoryRemapBySender.Remove(senderSteamId);
            }

            // TODO: Limpiar archivos en caché y descargar recursos de audio asociados a senderSteamId

            return removed;
        }

        // ===================================================================
        // LADO EMISOR (FUTURO)
        // ===================================================================

        // TODO: Implementar exportación de archivos de sonido para envío P2P cuando esté lista la transferencia
    }
}
