using PushToMeowMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MeowxMeadow
{
    /// <summary>
    /// Administra la lectura, recarga en caliente y resolución en tiempo de ejecución de los maullidos asignados.
    /// Mapea la tupla compuesta (SteamID, SlugcatClass) con el ModID correspondiente en disco.
    /// </summary>
    public static class PlayerMeowProfileManager
    {
        private static readonly string MeowRootPath = $"{Application.persistentDataPath}{Path.DirectorySeparatorChar}meowxmeadow{Path.DirectorySeparatorChar}";
        private static readonly string ProfilesFile = MeowRootPath + "meowxmeadow.txt";
        private static DateTime _lastProfilesWriteTime;

        /// <summary>
        /// Diccionario principal de asignaciones. 
        /// Clave: (SteamID, SlugCatClass) -> Valor: ModID del paquete de audio.
        /// </summary>
        public static Dictionary<(string SteamId, string SlugCatClass), string> Profiles = new Dictionary<(string SteamId, string SlugCatClass), string>();
        public static Dictionary<(string SteamId, string SlugCatClass), string> RemoteProfiles = new Dictionary<(string SteamId, string SlugCatClass), string>();

        /// <summary>
        /// Lee el archivo `.txt` de configuración y registra cada perfil de jugador en el diccionario.
        /// </summary>
        public static void LoadProfiles()
        {
            Profiles.Clear();

            try
            {
                if (!Directory.Exists(MeowRootPath))
                {
                    Directory.CreateDirectory(MeowRootPath);
                    Plugin.Logger.LogInfo($"Directorio creado en: {MeowRootPath}");
                }

                if (!File.Exists(ProfilesFile))
                {
                    File.WriteAllText(ProfilesFile, null);
                    Plugin.Logger.LogInfo($"Archivo creado: {ProfilesFile}");
                    return;
                }

                foreach (string line in File.ReadAllLines(ProfilesFile))
                {
                    // Ignorar líneas vacías o comentarios manuales (#)
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    string[] parts = line.Split(':');
                    if (parts.Length == 3) // Formato obligatorio de 3 partes: SteamID:ModID:SlugCatClass
                    {
                        string steamId = parts[0].Trim();
                        string modId = parts[1].Trim();
                        string slugCatClass = parts[2].Trim();

                        if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(slugCatClass))
                            continue;

                        // Clave de tupla compuesta
                        Profiles[(SteamId: steamId, SlugCatClass: slugCatClass)] = modId;
                        Plugin.Logger.LogInfo($"Registrado SteamID: {steamId} | ModID: {modId} | Class: {slugCatClass}");
                    }
                }

                Plugin.Logger.LogInfo($"Perfiles cargados con éxito: {Profiles.Count}");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al cargar perfiles: {ex.Message}");
            }
        }

        /// <summary>
        /// Revisa si la fecha de modificación del archivo .txt cambió para recargar perfiles en caliente sin reiniciar el juego.
        /// </summary>
        public static void ReloadProfilesIfChanged()
        {
            if (File.Exists(ProfilesFile))
            {
                DateTime currentWriteTime = File.GetLastWriteTime(ProfilesFile);
                if (currentWriteTime != _lastProfilesWriteTime)
                {
                    _lastProfilesWriteTime = currentWriteTime;
                    LoadProfiles();
                    Plugin.Logger.LogInfo("Archivo meadowMeow.txt modificado. Perfiles recargados en caliente.");
                }
            }
        }

        /// <summary>
        /// Método Hook interceptado en MeowUtils.FindMeowSoundID. 
        /// Evalúa el perfil asignado para el Player actual y retorna el sonido, pitch y volumen personalizados.
        /// </summary>
        private static (SoundID meowSoundID, float pitch, float volume) MeowUtils_FindMeowSoundID(Func<Player, bool, (SoundID, float, float)> orig, Player self, bool isShortMeow)
        {
            if (self == null || self.SlugCatClass == null)
                return orig(self, isShortMeow);

            ReloadProfilesIfChanged();

            // 1. Obtener el SteamID mapeado en tiempo de entrada para este Player
            if (Plugin.Instance.OnlinePlayerToKeyLookup.TryGetValue(self, out string playerSteamId) && !string.IsNullOrEmpty(playerSteamId))
            {
                string slugCatClass = self.SlugCatClass.value;

                // 2. Buscar si el jugador tiene una asignación registrada para esta clase de Slugcat
                string assignedModId = GetEffectiveModId(playerSteamId, slugCatClass);
                if (!string.IsNullOrEmpty(assignedModId))
                {
                    // Formato clave global de PushToMeow: "ModID:SlugCatClass"
                    string targetRegistryKey = $"{assignedModId}:{slugCatClass}";

                    if (MeowRegistration.AllCustomMeows.TryGetValue(targetRegistryKey, out CustomMeow meow))
                    {
                        Plugin.Logger.LogDebug($"Maullido personalizado encontrado para {playerSteamId} ({slugCatClass}) -> Key: {targetRegistryKey}");

                        bool isPup = self.playerState.isPup;
                        float pitch = isPup ? 1.3f : 1f;
                        float volume = PushToMeowMain.ModSettings.MeowVolumeMultiplier.Value * meow.VolumeMultiplier;

                        // Determinar el SoundID correcto evaluando si es Pup o Adulto, y Corto o Largo
                        SoundID meowType;
                        if (isPup)
                        {
                            meowType = isShortMeow ? (meow.ShortMeowPupSoundID ?? meow.ShortMeowSoundID) : (meow.LongMeowPupSoundID ?? meow.LongMeowSoundID);

                            // Pups con audio personalizado propio restablecen el pitch a 1.0f
                            if (meowType == meow.ShortMeowPupSoundID || meowType == meow.LongMeowPupSoundID)
                            {
                                pitch = 1f;
                            }
                        }
                        else
                        {
                            meowType = isShortMeow ? meow.ShortMeowSoundID : meow.LongMeowSoundID;
                        }

                        // Fallback de seguridad: Si la variante no existe en el CustomMeow, recurrir al sonido original
                        if (meowType == null)
                        {
                            var fallback = orig(self, isShortMeow);
                            meowType = fallback.Item1;
                        }

                        if (self.grabbedBy.Count > 0 && !(self.grabbedBy[0].grabber is Player))
                        {
                            pitch += 0.2f + UnityEngine.Random.value * 0.15f;
                        }
                        if (self.submerged)
                        {
                            pitch -= 0.1f + UnityEngine.Random.value * 0.15f;
                        }

                        return (meowSoundID: meowType, pitch, volume);
                    }
                    else
                    {
                        Plugin.Logger.LogWarning($"Perfil asignado para {playerSteamId}, pero '{targetRegistryKey}' no está cargado en el Registry.");
                    }
                }
            }

            // Fallback general: Si no hay perfil registrado, ejecutar comportamiento original
            return orig(self, isShortMeow);
        }

        public static string GetEffectiveModId(string steamId, string slugcatClass)
        {
            var key = (SteamId: steamId, SlugCatClass: slugcatClass);

            // Prioridad 1: Override manual definido por el usuario local en la UI
            if (PlayerMeowProfileManager.Profiles.TryGetValue(key, out var manualModId))
            {
                return manualModId;
            }

            // Prioridad 2: El perfil remoto recibido dinámicamente por Handshake
            if (PlayerMeowProfileManager.RemoteProfiles.TryGetValue(key, out var remoteModId))
            {
                return remoteModId;
            }

            // Prioridad 3: Fallback nativo
            return null;
        }

        /// <summary>
        /// Asigna un ModID de audio a un SteamID y SlugcatClass específicos, actualizando memoria y disco.
        /// </summary>
        public static void SaveProfile(string steamId, string modId, string slugcatClass)
        {
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(slugcatClass) || string.IsNullOrEmpty(modId))
                return;

            var key = (SteamId: steamId.Trim(), SlugCatClass: slugcatClass.Trim());
            Profiles[key] = modId.Trim();

            FlushProfilesToDisk();
        }

        /// <summary>
        /// Elimina todas las entradas asociadas a un SteamID de la memoria local y actualiza el disco.
        /// </summary>
        public static void RemoveProfile(string steamId, string slugcatClass = null)
        {
            if (string.IsNullOrEmpty(steamId)) return;

            if (slugcatClass == null)
            {
                var keysToRemove = Profiles.Keys.Where(k => k.SteamId.Equals(steamId.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
                if (keysToRemove.Count == 0) return;

                foreach (var key in keysToRemove)
                {
                    Profiles.Remove(key);
                }
            }
            else
            {
                Profiles.Remove((steamId, slugcatClass));
            }

            FlushProfilesToDisk();
        }

        /// <summary>
        /// Reescribe el archivo meowxmeadow.txt preservando el formato obligatorio SteamID:ModID:SlugCatClass.
        /// </summary>
        private static void FlushProfilesToDisk()
        {
            try
            {
                if (!Directory.Exists(MeowRootPath))
                {
                    Directory.CreateDirectory(MeowRootPath);
                }

                List<string> lines = new List<string>
                {
                    "# MeowxMeadow Profile Assignments",
                    "# Format: SteamID:ModID:SlugCatClass"
                };

                foreach (var kvp in Profiles)
                {
                    lines.Add($"{kvp.Key.SteamId}:{kvp.Value}:{kvp.Key.SlugCatClass}");
                }

                File.WriteAllLines(ProfilesFile, lines);
                _lastProfilesWriteTime = File.GetLastWriteTime(ProfilesFile);
                Plugin.Logger.LogInfo($"meowxmeadow.txt actualizado correctamente con {Profiles.Count} entradas.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al guardar meowxmeadow.txt: {ex.Message}");
            }
        }

        public static void ClearAll()
        {
            Profiles.Clear();
            RemoteProfiles.Clear();
        }
    }
}
