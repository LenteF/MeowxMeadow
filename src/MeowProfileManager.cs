using Newtonsoft.Json;
using PushToMeowMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MeowxMeadow
{
    [Serializable]
    public class MeowDatabase
    {
        public Dictionary<int, MeowProfileData> Profiles = new Dictionary<int, MeowProfileData>();
        public int MaxProfiles = 99;
        public DateTime LastUpdated = DateTime.Now;
    }

    [Serializable]
    public class MeowProfileData
    {
        public int InternalProfileNumber;
        public Dictionary<string, CustomMeowDto> MeowsBySlugcat = new Dictionary<string, CustomMeowDto>(StringComparer.Ordinal);
        public DateTime LastUpdated = DateTime.Now;
    }

    [Serializable]
    public class CustomMeowDto
    {
        public string ShortMeowSoundId;
        public string LongMeowSoundId;
        public string ShortMeowPupSoundId;
        public string LongMeowPupSoundId;
        public float VolumeMultiplier = 1.0f;
        public DateTime LastUpdated = DateTime.Now;
    }

    public static class MeowProfileManager
    {
        // ============================================================
        // ARCHIVO 1: DATOS DE MAULLIDO
        // ============================================================
        private static string RootPath => $"{Application.persistentDataPath}{Path.DirectorySeparatorChar}meowxmeadow{Path.DirectorySeparatorChar}";
        private static string SaveFile => RootPath + "meowcustom.json";

        // ============================================================
        // ARCHIVO 2: ASIGNACIONES
        // ============================================================
        private static MeowDatabase _database;
        public static MeowDatabase Database => _database ??= Load();

        private const int PROFILE_OFFSET = 4;

        private static Dictionary<int, MeowProfileData> _unsavedProfiles = new Dictionary<int, MeowProfileData>();

        public static int GetInternalProfile(int displayNumber) => displayNumber + PROFILE_OFFSET;

        // ============================================================
        // CARGAR / GUARDAR PERFILES (Versión JSON legible para tests)
        // ============================================================
        public static MeowDatabase Load()
        {
            try
            {
                if (File.Exists(SaveFile))
                {
                    string jsonContent = File.ReadAllText(SaveFile);
                    var loaded = JsonConvert.DeserializeObject<MeowDatabase>(jsonContent);
                    if (loaded != null)
                    {
                        return loaded;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"Error loading meow profiles: {ex.Message}");
            }

            var newDb = new MeowDatabase();
            Save(newDb);
            return newDb;
        }

        public static void Save(MeowDatabase db = null)
        {
            try
            {
                var toSave = db ?? _database;
                if (toSave == null) return;

                toSave.LastUpdated = DateTime.Now;

                if (!Directory.Exists(RootPath))
                {
                    Directory.CreateDirectory(RootPath);
                }

                string jsonContent = JsonConvert.SerializeObject(toSave, Formatting.Indented);
                File.WriteAllText(SaveFile, jsonContent);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"Error saving meow profiles: {ex.Message}");
            }
        }

        // ============================================================
        // OPERACIONES CON PERFILES
        // ============================================================
        public static MeowProfileData GetOrCreateProfile(int displayNumber)
        {
            if (displayNumber < 1 || displayNumber > Database.MaxProfiles)
            {
                return null;
            }

            int internalNumber = GetInternalProfile(displayNumber);

            if (Database.Profiles == null)
                Database.Profiles = new Dictionary<int, MeowProfileData>();

            if (Database.Profiles.TryGetValue(internalNumber, out var persisted))
                return persisted;

            var newProfile = new MeowProfileData
            {
                InternalProfileNumber = internalNumber
            };

            Database.Profiles[internalNumber] = newProfile;
            return newProfile;
        }

        public static CustomMeowDto GetProfileCustomMeow(int displayNumber, string slugcatName)
        {
            var profile = GetOrCreateProfile(displayNumber);
            if (profile?.MeowsBySlugcat != null &&
                profile.MeowsBySlugcat.TryGetValue(slugcatName, out var customMeowDto))
            {
                return customMeowDto;
            }
            return null;
        }

        // ============================================================
        // OPERACIONES CON ASIGNACIONES SteamID
        // ============================================================
        public static CustomMeowDto GetCustomMeowDtoBySteamID(string steamId, string slugcatName)
        {
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(slugcatName))
                return null;

            if (slugcatName == "MeadowOnline")
                slugcatName = "White";

            int profileNumber = GetProfileBySteamID(steamId);
            if (profileNumber < 0)
                return null;

            return GetProfileCustomMeow(profileNumber, slugcatName);
        }

        // ============================================================
        // OPERACIONES CON ASIGNACIONES SteamID (DMSxMeadow)
        // ============================================================
        public static int GetProfileBySteamID(string steamID)
        {
            if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("dmsxmeadow") || string.IsNullOrEmpty(steamID))
                return -1;

            return GetProfileInternal(steamID);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetProfileInternal(string steamID)
        {
            return DMSxMeadow.MeadowProfileManager.GetProfileBySteamID(steamID);
        }

        // ============================================================
        // CONVERSIÓN Y RESOLUCIÓN DE SONIDOS
        // ============================================================
        private static CustomMeow ConvertDtoToCustomMeow(CustomMeowDto dto)
        {
            CustomMeow custom = new CustomMeow();

            custom.VolumeMultiplier = dto.VolumeMultiplier;
            custom.ShortMeowSoundID = ResolveSoundId(dto.ShortMeowSoundId);
            custom.LongMeowSoundID = ResolveSoundId(dto.LongMeowSoundId);
            custom.ShortMeowPupSoundID = ResolveSoundId(dto.ShortMeowPupSoundId);
            custom.LongMeowPupSoundID = ResolveSoundId(dto.LongMeowPupSoundId);

            return custom;
        }

        private static SoundID ResolveSoundId(string soundName)
        {
            if (string.IsNullOrEmpty(soundName))
                return null;

            if (ExtEnumBase.TryParse(typeof(SoundID), soundName, true, out ExtEnumBase parsed))
                return parsed as SoundID;

            return null;
        }

        private static CustomMeow? GetMeowPackageBySteamID(string steamId, string slugcatName)
        {
            CustomMeowDto dto = GetCustomMeowDtoBySteamID(steamId, slugcatName);
            if (dto == null) return null;

            return ConvertDtoToCustomMeow(dto);
        }
        private static (SoundID meowSoundID, float pitch, float volume) MeowUtils_FindMeowSoundID(Func<Player, bool, (SoundID, float, float)> orig, Player self, bool isShortMeow)
        {
            if (Plugin.Instance.OnlinePlayerToKeyLookup.TryGetValue(self, out string steamId) && !string.IsNullOrEmpty(steamId))
            {
                string slugcatName = self.SlugCatClass.value;

                CustomMeow? customMeow = GetMeowPackageBySteamID(steamId, slugcatName);

                if (customMeow.HasValue)
                {
                    var meow = customMeow.Value;
                    bool isPup = self.playerState.isPup;
                    float pitch = isPup ? 1.3f : 1f;
                    float volume = PushToMeowMain.ModSettings.MeowVolumeMultiplier.Value * meow.VolumeMultiplier;

                    SoundID meowType;
                    if (isPup)
                    {
                        meowType = isShortMeow ? (meow.ShortMeowPupSoundID ?? meow.ShortMeowSoundID) : (meow.LongMeowPupSoundID ?? meow.LongMeowSoundID);

                        if (meowType == meow.ShortMeowPupSoundID || meowType == meow.LongMeowPupSoundID)
                        {
                            pitch = 1f;
                        }
                    }
                    else
                    {
                        meowType = isShortMeow ? meow.ShortMeowSoundID : meow.LongMeowSoundID;
                    }

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
            }

            return orig(self, isShortMeow);
        }
    }
}
