using Newtonsoft.Json;
using PushToMeowMod;
using System;
using System.Collections.Generic;
using System.IO;

namespace MeowxMeadow
{
    /// <summary>
    /// Escanea las carpetas de mods instalados en el juego para registrar globalmente todos los archivos `custom_meows.json`.
    /// </summary>
    public static class CustomMeowRegistry
    {
        /// <summary>
        /// Registro global de maullidos personalizados cargados en memoria.
        /// Clave: "ModID:SlugcatID" -> Valor: Instancia de <see cref="CustomMeow"/>.
        /// </summary>
        public static Dictionary<string, CustomMeow> AllCustomMeows = new Dictionary<string, CustomMeow>();

        /// <summary>
        /// Busca recursivamente en los directorios de mods activos los archivos JSON de audio y los mapea en <see cref="AllCustomMeows"/>.
        /// </summary>
        public static void LoadAllCustomMeows()
        {
            AllCustomMeows.Clear();

            try
            {
                // Utiliza la utilidad nativa de PushToMeow para explorar las rutas de mods
                string[] pushToMeowFiles = MeowUtils.ListDirectory("pushtomeow", directories: false, includeAll: true);

                foreach (string file in pushToMeowFiles)
                {
                    if (Path.GetFileName(file) != "custom_meows.json") continue;

                    DirectoryInfo pushToMeowDir = new DirectoryInfo(Path.GetDirectoryName(file));
                    DirectoryInfo modFolder = pushToMeowDir?.Parent;

                    if (modFolder == null) continue;

                    string modId = GetModId(modFolder.FullName);

                    if (string.IsNullOrEmpty(modId))
                    {
                        Plugin.Logger.LogWarning($"No se encontró modinfo.json en {modFolder.FullName}.");
                        continue;
                    }

                    // Deserializar JSON a objeto estructurado
                    CustomMeowWrapperJson meowfile = JsonConvert.DeserializeObject<CustomMeowWrapperJson>(File.ReadAllText(file));
                    if (meowfile.custom_meows == null) continue;
                    foreach (CustomMeowJson slugcat in meowfile.custom_meows)
                    {
                        string fullKey = $"{modId}:{slugcat.slugcat_id}";

                        CustomMeow customMeow = new CustomMeow
                        {
                            SlugcatID = slugcat.slugcat_id,
                            FilePath = file,
                            VolumeMultiplier = slugcat.volume_multiplier,
                            ShortMeowSoundID = slugcat.short_meow_soundid != null ? new SoundID(slugcat.short_meow_soundid, true) : null,
                            LongMeowSoundID = slugcat.long_meow_soundid != null ? new SoundID(slugcat.long_meow_soundid, true) : null,
                            ShortMeowPupSoundID = slugcat.short_meow_pup_soundid != null ? new SoundID(slugcat.short_meow_pup_soundid, true) : null,
                            LongMeowPupSoundID = slugcat.long_meow_pup_soundid != null ? new SoundID(slugcat.long_meow_pup_soundid, true) : null
                        };

                        AllCustomMeows[fullKey] = customMeow;
                        Plugin.Logger.LogInfo($"Registrado maullido custom: {fullKey}");
                    }
                }
                Plugin.Logger.LogInfo($"Carga extendida finalizada con éxito. Total: {AllCustomMeows.Count} entradas.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al procesar perfiles extendidos: {ex}");
            }
        }

        /// <summary>
        /// Obtiene el identificador "id" del mod leyendo directamente su archivo `modinfo.json`.
        /// </summary>
        private static string GetModId(string modFolderPath)
        {
            string jsonPath = Path.Combine(modFolderPath, "modinfo.json");
            if (File.Exists(jsonPath))
            {
                try
                {
                    string jsonText = File.ReadAllText(jsonPath);

                    foreach (string line in jsonText.Split('\n'))
                    {
                        if (line.Contains("\"id\""))
                        {
                            string[] parts = line.Split(':');
                            if (parts.Length >= 2)
                            {
                                string modId = parts[1].Trim('"', ' ', ',', '\r', '\n', '\t');
                                return modId;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError($"Error al leer {jsonPath}: {ex.Message}");
                }
            }
            return null;
        }
    }
}
