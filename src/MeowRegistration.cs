using Newtonsoft.Json;
using PushToMeowMod;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MeowxMeadow
{
    /// <summary>
    /// Escanea las carpetas de mods instalados en el juego para registrar globalmente todos los archivos `custom_meows.json`.
    /// </summary>
    public static class MeowRegistration
    {
        /// <summary>
        /// Registro global de maullidos personalizados cargados en memoria.
        /// Clave: "ModID:SlugcatID" -> Valor: Instancia de <see cref="CustomMeow"/>.
        /// </summary>
        public static readonly Dictionary<string, CustomMeow> AllCustomMeows = new Dictionary<string, CustomMeow>(StringComparer.Ordinal);
        public static readonly Dictionary<string, CustomMeow> AllActiveCustomMeows = new Dictionary<string, CustomMeow>(StringComparer.Ordinal);

        public static void LoadAllCustomMeows()
        {
            AllCustomMeows.Clear();

            try
            {
                List<string> jsonFiles = FindAllPushToMeowJsonFilesOnDisk();

                foreach (string file in jsonFiles)
                {
                    DirectoryInfo pushToMeowDir = new DirectoryInfo(Path.GetDirectoryName(file));
                    DirectoryInfo modFolder = pushToMeowDir?.Parent;

                    if (modFolder == null) continue;

                    string modId = GetModId(modFolder.FullName);
                    if (string.IsNullOrEmpty(modId)) continue;

                    string jsonContent = File.ReadAllText(file);
                    CustomMeowWrapperJson meowfile = JsonConvert.DeserializeObject<CustomMeowWrapperJson>(jsonContent);
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
                        Plugin.Logger.LogInfo($"[Global] Registrado maullido custom: {fullKey}");
                    }
                }

                Plugin.Logger.LogInfo($"Carga global finalizada con éxito. Total en disco: {AllCustomMeows.Count} entradas.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al procesar maullidos globales en disco: {ex}");
            }
        }

        private static List<string> FindAllPushToMeowJsonFilesOnDisk()
        {
            List<string> foundFiles = new List<string>();
            HashSet<string> searchRoots = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                foreach (var mod in ModManager.InstalledMods)
                {
                    if (mod != null && !string.IsNullOrEmpty(mod.path) && Directory.Exists(mod.path))
                    {
                        searchRoots.Add(mod.path);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"No se pudo leer ModManager.InstalledMods: {ex.Message}");
            }

            try
            {
                DirectoryInfo dataDir = new DirectoryInfo(Application.dataPath);
                DirectoryInfo steamAppsDir = dataDir?.Parent.Parent;

                if (steamAppsDir != null && steamAppsDir.Exists)
                {
                    string workshopPath = Path.Combine(steamAppsDir.FullName, "workshop", "content", "312520");
                    if (Directory.Exists(workshopPath))
                    {
                        searchRoots.Add(workshopPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"No se pudo resolver la ruta directa de Workshop: {ex.Message}");
            }

            foreach (string rootFolder in searchRoots)
            {
                try
                {
                    if (!Directory.Exists(rootFolder)) continue;

                    string jsonPath = Path.Combine(rootFolder, "pushtomeow", "custom_meows.json");
                    if (File.Exists(jsonPath) && !foundFiles.Contains(jsonPath))
                    {
                        Plugin.Logger.LogInfo($"custom_meows.json encontrado con ÉXITO en {jsonPath}");
                        foundFiles.Add(jsonPath);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"Error buscando en la raíz '{rootFolder}': {ex.Message}");
                }
            }

            return foundFiles;
        }

        /// <summary>
        /// Busca recursivamente en los directorios de mods activos los archivos JSON de audio y los mapea en <see cref="AllActiveCustomMeows"/>.
        /// </summary>
        public static void LoadAllActiveCustomMeows()
        {
            AllActiveCustomMeows.Clear();

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

                        AllActiveCustomMeows[fullKey] = customMeow;
                        Plugin.Logger.LogInfo($"Registrado maullido custom: {fullKey}");
                    }
                }
                Plugin.Logger.LogInfo($"Carga extendida finalizada con éxito. Total: {AllActiveCustomMeows.Count} entradas.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error al procesar perfiles extendidos: {ex}");
            }
        }

        /// <summary>
        /// Obtiene el identificador "id" del mod leyendo directamente su archivo `modinfo.json`.
        /// </summary>
        public static string GetModId(string modFolderPath)
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

        public static void InjectInactiveLocalMeows(SoundLoader soundLoader)
        {
            if (soundLoader == null)
            {
                Plugin.Logger.LogWarning("[InjectInactiveLocalMeows] Instancia de SoundLoader nula.");
                return;
            }

            if (AllCustomMeows.Count == 0) LoadAllCustomMeows();
            if (AllActiveCustomMeows.Count == 0) LoadAllActiveCustomMeows();

            HashSet<string> processedModFolders = new HashSet<string>(StringComparer.Ordinal);

            int injectedModsCount = 0;

            foreach (var kvp in AllCustomMeows)
            {
                string fullKey = kvp.Key;
                CustomMeow customMeow = kvp.Value;

                if (AllActiveCustomMeows.ContainsKey(fullKey))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(customMeow.FilePath) || !File.Exists(customMeow.FilePath)) continue;

                DirectoryInfo pushToMeowDir = new DirectoryInfo(Path.GetDirectoryName(customMeow.FilePath));
                DirectoryInfo modFolder = pushToMeowDir?.Parent;

                if (modFolder == null || !modFolder.Exists) continue;

                if (processedModFolders.Contains(modFolder.FullName)) continue;
                processedModFolders.Add(modFolder.FullName);

                string soundsTxtPath = Path.Combine(modFolder.FullName, "modify", "soundeffects", "sounds.txt");
                string soundEffectsFolder = Path.Combine(modFolder.FullName, "soundeffects");

                if (File.Exists(soundsTxtPath) && Directory.Exists(soundEffectsFolder))
                {
                    Plugin.Logger.LogInfo($"📦 Inyectando maullido inactivo detectado: '{fullKey.Split(':')[0]}' desde {modFolder.FullName}");
                    LoadAndInjectFromSoundsTxt(soundLoader, soundsTxtPath, soundEffectsFolder);
                    injectedModsCount++;
                }
                else
                {
                    Plugin.Logger.LogWarning($"⚠️ Mod inactivo '{fullKey}' no posee sounds.txt o carpeta soundeffects en la estructura esperada.");
                }
            }

            Plugin.Logger.LogInfo($"✅ Inyección de maullidos inactivos finalizada. Total de mods inactivos procesados: {injectedModsCount}");
        }

        private static void LoadAndInjectFromSoundsTxt(SoundLoader soundLoader, string soundsTxtPath, string audioFolderPath)
        {
            if (!File.Exists(soundsTxtPath) || !Directory.Exists(audioFolderPath)) return;

            const string prefix = "[ADD]";
            string[] lines = File.ReadAllLines(soundsTxtPath);

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || !line.StartsWith(prefix) || !line.Contains(":")) continue;

                string[] parts = line.Substring(prefix.Length).Split(':');
                string[] headerParts = parts[0].Trim().Split('/');

                string soundIdValue = headerParts[0].Trim();
                SoundID soundId = new SoundID(soundIdValue, register: true);

                string[] samplesRaw = parts[1].Split(',');
                List<string> validFilePaths = new List<string>();
                Dictionary<string, string[]> sampleParamsDict = new Dictionary<string, string[]>();

                foreach (string sampleRaw in samplesRaw)
                {
                    string cleanSample = sampleRaw.Trim();
                    if (string.IsNullOrEmpty(cleanSample)) continue;

                    string[] sampleConfig = cleanSample.Split('/');
                    string sampleName = sampleConfig[0].Trim();

                    string filePath = Path.Combine(audioFolderPath, sampleName + ".wav");
                    if (!File.Exists(filePath))
                    {
                        filePath = Path.Combine(audioFolderPath, sampleName + ".ogg");
                        if (!File.Exists(filePath)) continue;
                    }

                    validFilePaths.Add(filePath);
                    sampleParamsDict[sampleName] = sampleConfig;
                }

                if (validFilePaths.Count > 0)
                {
                    InjectAudioPackage(soundLoader, soundId, validFilePaths.ToArray(), headerParts, sampleParamsDict);
                }
            }
        }

        private static bool InjectAudioPackage(SoundLoader soundLoader, SoundID soundId, string[] audioFilePaths, string[] headerParams = null, Dictionary<string, string[]> sampleParamsDict = null)
        {
            if (soundLoader == null || soundId == null || audioFilePaths == null || audioFilePaths.Length == 0)
            {
                Plugin.Logger.LogWarning($"[InjectDynamicAudio] Parámetros o lista de archivos inválidos para '{soundId?.value}'.");
                return false;
            }

            try
            {
                List<SoundLoader.SoundPlayInstruction> instructions = new List<SoundLoader.SoundPlayInstruction>();

                // -------------------------------------------------------------------------
                // PASO 1: Inyectar en lote en allAudio[]
                // -------------------------------------------------------------------------
                Dictionary<string, int> existingClipsByName = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < soundLoader.allAudio.Length; i++)
                {
                    SoundLoader.ClipLoadData audioData = soundLoader.allAudio[i];
                    if (!string.IsNullOrEmpty(audioData.name) && !existingClipsByName.ContainsKey(audioData.name))
                    {
                        existingClipsByName[audioData.name] = i;
                    }
                }

                List<SoundLoader.ClipLoadData> newClipsToInject = new List<SoundLoader.ClipLoadData>();
                int startIndexForNewClips = soundLoader.allAudio.Length;

                foreach (string filePath in audioFilePaths)
                {
                    if (!File.Exists(filePath)) continue;

                    string sampleName = Path.GetFileNameWithoutExtension(filePath);

                    string[] sampleInit;
                    if (sampleParamsDict != null && sampleParamsDict.TryGetValue(sampleName, out string[] customParams))
                    {
                        sampleInit = customParams;
                    }
                    else
                    {
                        sampleInit = new string[] { sampleName, "vol=1", "pitch=1" };
                    }

                    int clipIndex;

                    // Comprobar si la muestra individual ya existe en allAudio
                    if (existingClipsByName.TryGetValue(sampleName, out int existingIndex))
                    {
                        clipIndex = existingIndex;
                    }
                    else
                    {
                        AudioType audioType = filePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)
                            ? AudioType.OGGVORBIS
                            : AudioType.WAV;

                        AudioClip loadedClip = AssetManager.SafeWWWAudioClip("file://" + filePath, threeD: false, stream: true, audioType);

                        SoundLoader.ClipLoadData newClipData = new SoundLoader.ClipLoadData
                        {
                            name = sampleName,
                            audioClipThroughUnity = true,
                            unityAudioCached = true,
                            audio = new AudioClip[] { loadedClip }
                        };

                        clipIndex = startIndexForNewClips + newClipsToInject.Count;

                        existingClipsByName[sampleName] = clipIndex;
                        newClipsToInject.Add(newClipData);
                    }

                    instructions.Add(new SoundLoader.SoundPlayInstruction(clipIndex, sampleInit));
                }

                if (instructions.Count == 0)
                {
                    Plugin.Logger.LogError($"[InjectDynamicAudio] No se pudo cargar ningún archivo de audio válido para '{soundId.value}'.");
                    return false;
                }

                if (newClipsToInject.Count > 0)
                {
                    int oldLength = soundLoader.allAudio.Length;
                    int newLength = oldLength + newClipsToInject.Count;

                    Array.Resize(ref soundLoader.allAudio, newLength);

                    for (int i = 0; i < newClipsToInject.Count; i++)
                    {
                        soundLoader.allAudio[oldLength + i] = newClipsToInject[i];
                    }

                    Plugin.Logger.LogInfo($"🔊 Inyectados {newClipsToInject.Count} clips en allAudio[] (Tamaño total: {newLength}).");
                }

                // -------------------------------------------------------------------------
                // PASO 2: Construir el SoundTrigger con todas las variaciones cargadas
                // -------------------------------------------------------------------------
                int soundIndex = soundId.Index;
                if (soundIndex < 0)
                {
                    Plugin.Logger.LogError($"[InjectDynamicAudio] SoundID '{soundId.value}' tiene un índice inválido (-1).");
                    return false;
                }

                if (soundIndex >= soundLoader.soundTriggers.Length)
                {
                    Array.Resize(ref soundLoader.soundTriggers, soundIndex + 1);
                }

                string[] triggerInit = headerParams ?? new string[] { soundId.value, "vol=1", "pitch=1" };
                soundLoader.soundTriggers[soundIndex] = new SoundLoader.SoundTrigger(
                    soundID: soundId,
                    sounds: instructions.ToArray(),
                    gVol: 1f,
                    soundLoader: soundLoader,
                    initString: triggerInit
                    );

                // -------------------------------------------------------------------------
                // PASO 3: Habilitar workingTriggers
                // -------------------------------------------------------------------------
                if (soundIndex >= soundLoader.workingTriggers.Length)
                {
                    Array.Resize(ref soundLoader.workingTriggers, soundIndex + 1);
                }

                soundLoader.workingTriggers[soundIndex] = true;

                Plugin.Logger.LogInfo($"✅ Paquete de sonido '{soundId.value}' (Índice {soundIndex}) registrado con éxito ({instructions.Count} variaciones).");
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[InjectDynamicAudio] Excepción inyectando paquete para '{soundId?.value}': {ex}");
                return false;
            }
        }

        public static bool HasAudioPackLoadedLocally(string modId, string slugcatClass)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(slugcatClass)) return false;
            string lookupKey = $"{modId}:{slugcatClass}";
            return AllCustomMeows.ContainsKey(lookupKey);
        }
    }
}
