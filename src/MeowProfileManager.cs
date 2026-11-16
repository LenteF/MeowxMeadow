using DMSxMeadow;
using MonoMod.RuntimeDetour;
using Newtonsoft.Json;
using PushToMeowMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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

        public CustomMeowDto Copy()
        {
            return new CustomMeowDto
            {
                ShortMeowSoundId = this.ShortMeowSoundId,
                LongMeowSoundId = this.LongMeowSoundId,
                ShortMeowPupSoundId = this.ShortMeowPupSoundId,
                LongMeowPupSoundId = this.LongMeowPupSoundId,
                VolumeMultiplier = this.VolumeMultiplier,
                LastUpdated = DateTime.Now
            };
        }
    }

    public static class MeowProfileManager
    {
        // ============================================================
        // ARCHIVO 1: DATOS DE MAULLIDO
        // ============================================================
        private static string RootPath => $"{Application.persistentDataPath}{Path.DirectorySeparatorChar}meowxmeadow{Path.DirectorySeparatorChar}";
        private static string SaveFile => RootPath + "meowcustom.dat";

        // ============================================================
        // ARCHIVO 2: ASIGNACIONES
        // ============================================================
        private static MeowDatabase _database;
        public static MeowDatabase Database => _database ??= Load();

        private const int PROFILE_OFFSET = 4;

        private static readonly Dictionary<int, MeowProfileData> _unsavedProfiles = new Dictionary<int, MeowProfileData>();

        public static int GetInternalProfile(int displayNumber) => displayNumber + PROFILE_OFFSET;
        public static int GetDisplayNumber(int internalNumber) => internalNumber - PROFILE_OFFSET;

        // ============================================================
        // INICIALIZACIÓN Y LIMPIEZA
        // ============================================================
        private static Hook DeleteProfileHook;
        private static Hook SetCurrentProfileHook;
        private static Hook SetSteamIdHook;
        private static Hook RemoveAssignmentHook;
        private static Hook DeleteOrphanProfilesHook;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Initialize()
        {
            try
            {
                MethodInfo deleteProfileMethod = typeof(MeadowProfileManager).GetMethod("DeleteProfile", BindingFlags.Public | BindingFlags.Static);
                MethodInfo deleteProfileHook = typeof(MeowProfileManager).GetMethod("MeadowProfileManager_DeleteProfile", BindingFlags.NonPublic | BindingFlags.Static);
                if (deleteProfileMethod != null && deleteProfileHook != null)
                    DeleteProfileHook = new Hook(deleteProfileMethod, deleteProfileHook);

                MethodInfo setCurrentProfileMethod = typeof(MeadowProfileManager).GetMethod("SetCurrentProfile", BindingFlags.Public | BindingFlags.Static);
                MethodInfo setCurrentProfileHook = typeof(MeowProfileManager).GetMethod("MeadowProfileManager_SetCurrentProfile", BindingFlags.NonPublic | BindingFlags.Static);
                if (setCurrentProfileMethod != null && setCurrentProfileHook != null)
                    SetCurrentProfileHook = new Hook(setCurrentProfileMethod, setCurrentProfileHook);

                MethodInfo setSteamIdMethod = typeof(MeadowProfileManager).GetMethod("SetSteamID", BindingFlags.Public | BindingFlags.Static);
                MethodInfo setSteamIdHook = typeof(MeowProfileManager).GetMethod("MeadowProfileManager_SetSteamID", BindingFlags.NonPublic | BindingFlags.Static);
                if (setSteamIdMethod != null && setSteamIdHook != null)
                    SetSteamIdHook = new Hook(setSteamIdMethod, setSteamIdHook);

                MethodInfo removeAssignmentMethod = typeof(MeadowProfileManager).GetMethod("RemoveAssignment", BindingFlags.Public | BindingFlags.Static);
                MethodInfo removeAssignmentHook = typeof(MeowProfileManager).GetMethod("MeadowProfileManager_RemoveAssignment", BindingFlags.NonPublic | BindingFlags.Static);
                if (removeAssignmentMethod != null && removeAssignmentHook != null)
                    RemoveAssignmentHook = new Hook(removeAssignmentMethod, removeAssignmentHook);

                MethodInfo deleteOrphanProfilesMethod = typeof(MeadowProfileManager).GetMethod("DeleteOrphanProfiles", BindingFlags.Public | BindingFlags.Static);
                MethodInfo deleteOrphanProfilesHook = typeof(MeowProfileManager).GetMethod("MeadowProfileManager_DeleteOrphanProfiles", BindingFlags.NonPublic | BindingFlags.Static);
                if (deleteOrphanProfilesMethod != null && deleteOrphanProfilesHook != null)
                    DeleteOrphanProfilesHook = new Hook(deleteOrphanProfilesMethod, deleteOrphanProfilesHook);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowProfileManager] Error instalando hooks: {ex.Message}");
            }

            Load();
            SyncProfilesWithDxm();
        }

        public static void Dispose()
        {
            DeleteProfileHook?.Dispose();
            SetCurrentProfileHook?.Dispose();
            SetSteamIdHook?.Dispose();
            RemoveAssignmentHook?.Dispose();
            DeleteOrphanProfilesHook?.Dispose();

            _unsavedProfiles.Clear();
        }

        // ============================================================
        // CARGAR / GUARDAR PERFILES (Versión JSON legible para tests)
        // ============================================================
        public static MeowDatabase Load()
        {
            try
            {
                if (File.Exists(SaveFile))
                {
                    using (var fs = new FileStream(SaveFile, FileMode.Open))
                    {
                        var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                        var loaded = (MeowDatabase)formatter.Deserialize(fs);
                        if (loaded != null)
                        {
                            return loaded;
                        }
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

                using (var fs = new FileStream(SaveFile, FileMode.Create))
                {
                    var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    formatter.Serialize(fs, toSave);
                }
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

            if (_unsavedProfiles.TryGetValue(displayNumber, out var scratch))
                return scratch;

            var newProfile = new MeowProfileData
            {
                InternalProfileNumber = internalNumber
            };

            _unsavedProfiles[displayNumber] = newProfile;
            return newProfile;
        }

        public static void SaveProfileOption(int displayNumber, string slugcatName, CustomMeowDto meowDto)
        {
            var profileData = GetOrCreateProfile(displayNumber);
            if (profileData == null) return;

            if (profileData.MeowsBySlugcat == null)
                profileData.MeowsBySlugcat = new Dictionary<string, CustomMeowDto>(StringComparer.Ordinal);

            profileData.MeowsBySlugcat[slugcatName] = meowDto;
            profileData.LastUpdated = DateTime.Now;

            string steamId = GetSteamID(displayNumber);
            if (!string.IsNullOrEmpty(steamId))
            {
                int internalNumber = GetInternalProfile(displayNumber);
                Database.Profiles[internalNumber] = profileData;
                _unsavedProfiles.Remove(displayNumber);
                Save();
            }
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

        private static void DiscardUnsavedIfOrphan(int displayNumber)
        {
            if (string.IsNullOrEmpty(GetSteamID(displayNumber)) && _unsavedProfiles.ContainsKey(displayNumber))
            {
                _unsavedProfiles.Remove(displayNumber);
            }
        }

        public static void SyncProfilesWithDxm()
        {
            if (!Plugin.IsDmsxMeadowActive || Database.Profiles == null || Database.Profiles.Count == 0)
                return;

            try
            {
                HashSet<int> validDisplayNumbers = GetAllProfileNumbersInternal();
                var toRemove = new List<int>();

                foreach (int internalNumber in Database.Profiles.Keys)
                {
                    int displayNum = GetDisplayNumber(internalNumber);

                    if (!validDisplayNumbers.Contains(displayNum))
                    {
                        toRemove.Add(internalNumber);
                    }
                }

                if (toRemove.Count > 0)
                {
                    foreach (int internalNumber in toRemove)
                    {
                        Database.Profiles.Remove(internalNumber);
                    }
                    Save();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowProfileManager] Error sincronizando perfiles con DxM: {ex.Message}");
            }
        }

        // ============================================================
        // OPERACIONES CON PERFILES (DMSxMeadow)
        // ============================================================
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static HashSet<int> GetAllProfileNumbersInternal()
        {
            var list = MeadowProfileManager.GetAllProfileNumbers();
            return list != null ? new HashSet<int>(list) : new HashSet<int>();
        }

        private static void MeadowProfileManager_DeleteProfile(Action<int> orig, int displayNumber)
        {
            try
            {
                int internalNumber = GetInternalProfile(displayNumber);
                if (Database.Profiles != null && Database.Profiles.TryGetValue(internalNumber, out var existing))
                {
                    _unsavedProfiles[displayNumber] = existing;
                    Database.Profiles.Remove(internalNumber);
                    Save();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowProfileManager] Error en hook DeleteProfile({displayNumber}): {ex.Message}");
            }

            orig(displayNumber);
        }

        private static void MeadowProfileManager_SetCurrentProfile(Action<int> orig, int displayNumber)
        {
            try
            {
                int previousProfile = GetCurrentProfileNumber();
                if (previousProfile != displayNumber)
                {
                    DiscardUnsavedIfOrphan(previousProfile);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowProfileManager] Error descartando borrador previo en SetCurrentProfile: {ex.Message}");
            }

            orig(displayNumber);
        }

        private static int MeadowProfileManager_DeleteOrphanProfiles(Func<int> orig)
        {
            int dxmDeleted = orig();

            try
            {
                SyncProfilesWithDxm();

                var toRemoveMemory = new List<int>();
                foreach (int displayNumber in _unsavedProfiles.Keys)
                {
                    if (string.IsNullOrEmpty(GetSteamID(displayNumber)))
                    {
                        toRemoveMemory.Add(displayNumber);
                    }
                }

                foreach (int displayNumber in toRemoveMemory)
                {
                    _unsavedProfiles.Remove(displayNumber);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowProfileManager] Error en hook DeleteOrphanProfiles: {ex.Message}");
            }

            return dxmDeleted;
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
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int GetProfileBySteamID(string steamID)
        {
            return MeadowProfileManager.GetProfileBySteamID(steamID);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetSteamID(int displayNumber)
        {
            return MeadowProfileManager.GetSteamID(displayNumber);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int GetCurrentProfileNumber()
        {
            return MeadowProfileManager.CurrentProfileNumber;
        }

        private static void MeadowProfileManager_SetSteamID(Action<int, string> orig, int displayNumber, string steamID)
        {
            orig(displayNumber, steamID);

            try
            {
                int internalNumber = GetInternalProfile(displayNumber);
                if (!string.IsNullOrEmpty(steamID))
                {
                    if (_unsavedProfiles.TryGetValue(displayNumber, out var pending))
                    {
                        Database.Profiles[internalNumber] = pending;
                        _unsavedProfiles.Remove(displayNumber);
                        Save();
                    }
                }
                else
                {
                    if (Database.Profiles != null && Database.Profiles.TryGetValue(internalNumber, out var existing))
                    {
                        _unsavedProfiles[displayNumber] = existing;
                        Database.Profiles.Remove(internalNumber);
                        Save();
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowProfileManager] Error en hook SetSteamID({displayNumber}): {ex.Message}");
            }
        }

        private static void MeadowProfileManager_RemoveAssignment(Action<int> orig, int displayNumber)
        {
            orig(displayNumber);

            try
            {
                DiscardUnsavedIfOrphan(displayNumber);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowProfileManager] Error en hook RemoveAssignment({displayNumber}): {ex.Message}");
            }
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

                CustomMeow? customMeow = null;
                if (Plugin.IsDmsxMeadowActive)
                    customMeow = GetMeowPackageBySteamID(steamId, slugcatName);

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
