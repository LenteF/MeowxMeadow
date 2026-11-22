using DMSxMeadow;
using DressMySlugcat;
using Menu;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using PushToMeowMod;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace MeowxMeadow
{
    // Integracion con el menu Get Fancy de DMS
    // Escribe la seleccion de maullido en meowcustom.json usando el perfil que dxm asigna al SteamID local.
    public static class MeowFancyMenuUI
    {
        private static Hook UpdateHook;
        private static Hook SignalHook;
        private static Hook ShutdownHook;
        private static Hook GetDmsCopiedCustomizationHook;
        private static Hook HandleMeadowCopyPasteDefaultsHook;
        private static ILHook LoadedMeowsIlHook;

        private static Dictionary<FancyMenu, MeowMenuPanel> _panels =
            new Dictionary<FancyMenu, MeowMenuPanel>();
        private static readonly Dictionary<string, List<MeowOption>> _optionsBySlugcat = new Dictionary<string, List<MeowOption>>(StringComparer.Ordinal);

        private static CustomMeowDto _copiedMeowDto;
        private static bool _hasCopiedMeowState = false;

        public static void Initialize()
        {
            try
            {
                Type fancyMenuType = typeof(FancyMenu);

                MethodInfo updateMethod = typeof(ProcessManager).GetMethod("Update");
                MethodInfo updateHookMethod = typeof(MeowFancyMenuUI).GetMethod("Update_Hook", BindingFlags.NonPublic | BindingFlags.Static);
                if (updateMethod != null && updateHookMethod != null)
                    UpdateHook = new Hook(updateMethod, updateHookMethod);

                MethodInfo signalMethod = fancyMenuType.GetMethod("Singal");
                MethodInfo signalHookMethod = typeof(MeowFancyMenuUI).GetMethod("Singal_Hook", BindingFlags.NonPublic | BindingFlags.Static);
                if (signalMethod != null && signalHookMethod != null)
                    SignalHook = new Hook(signalMethod, signalHookMethod);

                MethodInfo shutdownMethod = fancyMenuType.GetMethod("ShutDownProcess");
                MethodInfo shutdownHook = typeof(MeowFancyMenuUI).GetMethod("ShutDownProcess_Hook", BindingFlags.NonPublic | BindingFlags.Static);
                if (shutdownMethod != null && shutdownHook != null)
                    ShutdownHook = new Hook(shutdownMethod, shutdownHook);

                MethodInfo getDmsCopiedCustomizationMethod = typeof(FancyMenuHookHandler).GetMethod("GetDmsCopiedCustomization", BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo getDmsCopiedCustomizationHook = typeof(MeowFancyMenuUI).GetMethod("GetDmsCopiedCustomization_Hook", BindingFlags.NonPublic | BindingFlags.Static);
                if (getDmsCopiedCustomizationMethod != null && getDmsCopiedCustomizationHook != null)
                    GetDmsCopiedCustomizationHook = new Hook(getDmsCopiedCustomizationMethod, getDmsCopiedCustomizationHook);

                MethodInfo handleMeadowCopyPasteDefaultsMethod = typeof(FancyMenuHookHandler).GetMethod("HandleMeadowCopyPasteDefaults", BindingFlags.NonPublic | BindingFlags.Static);
                MethodInfo handleMeadowCopyPasteDefaultsHook = typeof(MeowFancyMenuUI).GetMethod("HandleMeadowCopyPasteDefaults_Hook", BindingFlags.NonPublic | BindingFlags.Static);
                if (handleMeadowCopyPasteDefaultsMethod != null && handleMeadowCopyPasteDefaultsHook != null)
                    HandleMeadowCopyPasteDefaultsHook = new Hook(handleMeadowCopyPasteDefaultsMethod, handleMeadowCopyPasteDefaultsHook);

                MethodInfo loadedMeowsMethod = typeof(MeowUtils).GetMethod("LoadCustomMeows", BindingFlags.Public | BindingFlags.Static);
                if (loadedMeowsMethod != null)
                    LoadedMeowsIlHook = new ILHook(loadedMeowsMethod, LoadCustomMeows_IL);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error instalando hooks: {ex.Message}");
            }

            MeowUtils.LoadCustomMeows();
        }

        public static void Dispose()
        {
            UpdateHook?.Dispose();
            SignalHook?.Dispose();
            ShutdownHook?.Dispose();
            GetDmsCopiedCustomizationHook?.Dispose();
            HandleMeadowCopyPasteDefaultsHook?.Dispose();
            LoadedMeowsIlHook?.Dispose();

            _panels.Clear();
            _optionsBySlugcat.Clear();

            _copiedMeowDto = null;
            _hasCopiedMeowState = false;
        }

        private static void ClearOptions()
        {
            _optionsBySlugcat.Clear();
        }

        private static void Update_Hook(Action<ProcessManager, float> orig, ProcessManager self, float deltaTime)
        {
            orig(self, deltaTime);

            try
            {
                if (self.currentMainLoop is FancyMenu mainMenu)
                    EnsurePanel(mainMenu);

                if (self.dialog is FancyMenu dialogMenu)
                    EnsurePanel(dialogMenu);

                foreach (var process in self.sideProcesses)
                {
                    if (process is FancyMenu sideMenu)
                        EnsurePanel(sideMenu);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error en Update: {ex.Message}");
            }
        }

        private static void EnsurePanel(FancyMenu menu)
        {
            if (!_panels.TryGetValue(menu, out var panel))
            {
                panel = new MeowMenuPanel(menu);
                panel.Initialize();
                _panels[menu] = panel;
            }

            panel.Tick();
        }

        private static void Singal_Hook(
            Action<FancyMenu, MenuObject, string> orig,
            FancyMenu menu,
            MenuObject sender,
            string message)
        {
            if (message == "MEOW_PREV" || message == "MEOW_NEXT" || message == "MEOW_PREVIEW")
            {
                if (_panels.TryGetValue(menu, out var panel))
                    panel.HandleSignal(message);
                return;
            }

            orig(menu, sender, message);
        }

        private static void ShutDownProcess_Hook(Action<DressMySlugcat.FancyMenu> orig, DressMySlugcat.FancyMenu self)
        {
            if (_panels.TryGetValue(self, out var panel))
                panel.CommitVolume();
            _panels.Remove(self);
            orig(self);
        }

        private static Customization GetDmsCopiedCustomization_Hook(Func<FancyMenu, Customization> orig, FancyMenu fancyMenu)
        {
            var result = orig(fancyMenu);
            _copiedMeowDto = new CustomMeowDto();
            _hasCopiedMeowState = true;
            return result;
        }

        private static void HandleMeadowCopyPasteDefaults_Hook(Action<FancyMenu, string> orig, FancyMenu fancyMenu, string message)
        {
            orig(fancyMenu, message);

            try
            {
                int profile = MeowProfileManager.GetCurrentProfileNumber();
                if (profile <= 0) return;

                string slugcat = fancyMenu.selectedSlugcat;

                if (message == "CUST_COPY")
                {
                    var dto = MeowProfileManager.GetProfileCustomMeow(profile, slugcat);
                    _copiedMeowDto = dto != null ? new CustomMeowDto
                    {
                        ShortMeowSoundId = dto.ShortMeowSoundId,
                        LongMeowSoundId = dto.LongMeowSoundId,
                        ShortMeowPupSoundId = dto.ShortMeowPupSoundId,
                        LongMeowPupSoundId = dto.LongMeowPupSoundId,
                        VolumeMultiplier = dto.VolumeMultiplier,
                        LastUpdated = DateTime.Now
                    } : null;

                    _hasCopiedMeowState = true;
                    return;
                }

                if (message == "CUST_PASTE")
                {
                    if (!_hasCopiedMeowState)
                        return;

                    var targetOptions = GetOptionsForSlugcat(slugcat);
                    MeowOption matchingOption = null;

                    if (_copiedMeowDto != null)
                    {
                        matchingOption = targetOptions.FirstOrDefault(opt =>
                            string.Equals(opt.ShortMeowSoundId ?? "", _copiedMeowDto.ShortMeowSoundId ?? "", StringComparison.Ordinal) &&
                            string.Equals(opt.LongMeowSoundId ?? "", _copiedMeowDto.LongMeowSoundId ?? "", StringComparison.Ordinal) &&
                            string.Equals(opt.ShortMeowPupSoundId ?? "", _copiedMeowDto.ShortMeowPupSoundId ?? "", StringComparison.Ordinal) &&
                            string.Equals(opt.LongMeowPupSoundId ?? "", _copiedMeowDto.LongMeowPupSoundId ?? "", StringComparison.Ordinal)
                        );
                    }

                    CustomMeowDto pastedDto;
                    if (matchingOption != null)
                    {
                        pastedDto = new CustomMeowDto
                        {
                            ShortMeowSoundId = matchingOption.ShortMeowSoundId,
                            LongMeowSoundId = matchingOption.LongMeowSoundId,
                            ShortMeowPupSoundId = matchingOption.ShortMeowPupSoundId,
                            LongMeowPupSoundId = matchingOption.LongMeowPupSoundId,
                            VolumeMultiplier = _copiedMeowDto.VolumeMultiplier,
                            LastUpdated = DateTime.Now
                        };
                    }
                    else
                    {
                        pastedDto = new CustomMeowDto();
                    }

                    MeowProfileManager.SaveProfileOption(profile, slugcat, pastedDto);
                }
                else if (message == "CUST_DEFAULTS")
                {
                    MeowProfileManager.SaveProfileOption(profile, slugcat, new CustomMeowDto());
                }

                if (_panels.TryGetValue(fancyMenu, out var panel))
                {
                    panel.RefreshDisplay();
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error en HandleMeadowCopyPasteDefaults_Hook ({message}): {ex.Message}");
            }
        }

        private static void LoadCustomMeows_IL(ILContext il)
        {
            ILCursor cursor = new ILCursor(il);

            cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, typeof(MeowFancyMenuUI).GetMethod(nameof(ClearOptions), BindingFlags.NonPublic | BindingFlags.Static));

            if (cursor.TryGotoNext(MoveType.After, x => x.MatchCallvirt(out var method) && method.Name == "Add" && method.DeclaringType.Name.Contains("Dictionary")))
            {
                cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldloc_S, (byte)10);
                cursor.Emit(Mono.Cecil.Cil.OpCodes.Ldloc_S, (byte)15);

                var registerMethod = typeof(MeowFancyMenuUI).GetMethod(nameof(RegisterCustomMeowFromHook), BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { typeof(CustomMeowWrapperJson), typeof(CustomMeowJson) }, null);
                if (registerMethod != null)
                {
                    cursor.Emit(Mono.Cecil.Cil.OpCodes.Call, registerMethod);
                }
            }
        }

        private static void RegisterCustomMeowFromHook(CustomMeowWrapperJson wrapper, CustomMeowJson json)
        {
            Plugin.Logger.LogInfo($"[MeowFancyUI] RegisterCustomMeowFromHook invocado. Archivo: '{wrapper._file}', JSON recibido: {(json != null ? "OK" : "NULL")}");

            if (json == null)
            {
                Plugin.Logger.LogWarning("[MeowFancyUI] -> Omitido: el objeto json es nulo.");
                return;
            }
            if (string.IsNullOrEmpty(json.slugcat_id))
            {
                Plugin.Logger.LogWarning("[MeowFancyUI] -> Omitido: slugcat_id está vacío o es nulo.");
                return;
            }

            string slugcatId = json.slugcat_id;
            string modName = ResolveModNameFromPath(wrapper._file);
            Plugin.Logger.LogInfo($"[MeowFancyUI] -> Procesando Slugcat ID: '{slugcatId}' | Mod resuelto: '{modName}'");

            if (!_optionsBySlugcat.TryGetValue(slugcatId, out var list))
            {
                Plugin.Logger.LogInfo($"[MeowFancyUI] -> El Slugcat '{slugcatId}' no existía en el diccionario. Creando lista con opción por defecto.");
                list = new List<MeowOption> { MeowOption.CreateDefault() };
                _optionsBySlugcat[slugcatId] = list;
            }

            bool isSlugpup = string.Equals(slugcatId, "Slugpup", StringComparison.Ordinal);

            bool exists = list.Any(o =>
                string.Equals(o.ShortMeowSoundId, json.short_meow_soundid ?? "", StringComparison.Ordinal) &&
                string.Equals(o.LongMeowSoundId, json.long_meow_soundid ?? "", StringComparison.Ordinal) &&
                (!isSlugpup || (
                    string.Equals(o.ShortMeowPupSoundId, json.short_meow_pup_soundid ?? "", StringComparison.Ordinal) &&
                    string.Equals(o.LongMeowPupSoundId, json.long_meow_pup_soundid ?? "", StringComparison.Ordinal)
                ))
            );

            if (!exists)
            {
                Plugin.Logger.LogInfo($"[MeowFancyUI] -> ¡Añadido con éxito! Maullido de '{modName}' registrado para '{slugcatId}'.");
                list.Add(new MeowOption
                {
                    Label = modName,
                    ShortMeowSoundId = json.short_meow_soundid ?? "",
                    LongMeowSoundId = json.long_meow_soundid ?? "",
                    ShortMeowPupSoundId = json.short_meow_pup_soundid ?? "",
                    LongMeowPupSoundId = json.long_meow_pup_soundid ?? "",
                    VolumeMultiplier = MeowVolumeManager.Normalize(json.volume_multiplier)
                });
            }
            else
            {
                Plugin.Logger.LogWarning($"[MeowFancyUI] -> Omitido: Ya existía una entrada idéntica para '{slugcatId}'.");
            }
        }

        private static string ResolveModNameFromPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return "Unkown Mod";

            try
            {
                string jsonDir = Path.GetDirectoryName(filePath);
                string modRootDir = jsonDir != null ? Path.GetDirectoryName(jsonDir) : null;

                if (!string.IsNullOrEmpty(modRootDir))
                {
                    string modInfoPath = Path.Combine(modRootDir, "modinfo.json");
                    if (File.Exists(modInfoPath))
                    {
                        string rawJson = File.ReadAllText(modInfoPath);
                        var match = System.Text.RegularExpressions.Regex.Match(
                            rawJson,
                            @"""name""\s*:\s*""([^""]+)""",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                        );

                        if (match.Success && match.Groups.Count > 1)
                        {
                            string modName = match.Groups[1].Value.Trim();
                            if (!string.IsNullOrEmpty(modName))
                                return modName;
                        }
                    }

                    return Path.GetFileName(modRootDir);
                }

                return "Custom Pack";
            }
            catch
            {
                return "Custom Pack";
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsMeadowModeOn()
        {
            return MeadowProfileManager.IsMeadowModeActive;
        }

        public static List<MeowOption> GetOptionsForSlugcat(string slugcatId)
        {
            if (string.IsNullOrEmpty(slugcatId))
                slugcatId = "White";

            if (!_optionsBySlugcat.TryGetValue(slugcatId, out var list))
            {
                list = new List<MeowOption> { MeowOption.CreateDefault() };
                _optionsBySlugcat[slugcatId] = list;
            }

            return list;
        }
    }

    public class MeowMenuPanel
    {
        private static readonly Slider.SliderID VolumeSliderId =
            new Slider.SliderID("MEOW_VOLUME", true);

        private sealed class VolumeSliderHost : MenuObject, Slider.ISliderOwner
        {
            private readonly MeowMenuPanel _panel;

            public VolumeSliderHost(FancyMenu menu, Page page, MeowMenuPanel panel) : base(menu, page)
            {
                _panel = panel;
            }

            public float ValueOfSlider(Slider slider)
            {
                return _panel.VolumeSliderValue();
            }

            public void SliderSetValue(Slider slider, float setValue)
            {
                _panel.SetVolumeSliderValue(setValue);
            }
        }

        private sealed class VolumeSlider : HorizontalSlider
        {
            public VolumeSlider(Menu.Menu menu, MenuObject owner, Vector2 pos, Vector2 size)
                : base(menu, owner, string.Empty, pos, size, VolumeSliderId, false) { }

            public override bool Selected
            {
                get { return !buttonBehav.greyedOut && (MouseOver || mouseDragged); }
            }
        }

        private FancyMenu _fancyMenu;
        private SimpleButton _prevButton;
        private SimpleButton _nextButton;
        private SimpleButton _previewButton;
        private MenuLabel _titleLabel;
        private MenuLabel _meowLabel;
        private MenuLabel _statusLabel;
        private VolumeSliderHost _volumeHost;
        private VolumeSlider _volumeSlider;
        private bool _lastSliderDragged;
        private float? _pendingVolume;

        private bool _uiAdded = false;
        private int _lastKnownProfile = -1;
        private string _lastKnownSteamId = "";
        private string _lastKnownSlugcat = "";
        private bool _lastMeadowState = false;
        private int _currentIndex = 0;

        public MeowMenuPanel(FancyMenu fancyMenu)
        {
            _fancyMenu = fancyMenu;
        }

        public void Initialize()
        {
            if (_uiAdded) return;

            try
            {
                var borderField = typeof(FancyMenu).GetField("textBoxBorder",
                    BindingFlags.Public | BindingFlags.Instance);
                var textBoxBorder = borderField?.GetValue(_fancyMenu) as RoundedRect;
                if (textBoxBorder == null) return;

                var playerButtonsField = typeof(FancyMenu).GetField("playerButtons",
                    BindingFlags.Public | BindingFlags.Instance);
                int playerCount = 4;
                if (playerButtonsField?.GetValue(_fancyMenu) is Array playerButtons && playerButtons.Length > 0)
                    playerCount = playerButtons.Length;

                float baseStartX = textBoxBorder.pos.x + (65f * playerCount) + 10f;
                float maxSafeX = textBoxBorder.pos.x + textBoxBorder.size.x - 260f;
                baseStartX = Mathf.Min(baseStartX, maxSafeX) - 10f;

                float rightEdge = textBoxBorder.pos.x + textBoxBorder.size.x;
                float baseYPos = textBoxBorder.pos.y - 75f;

                // Constantes de posicion
                float meowX = baseStartX + 240f;
                if (rightEdge - meowX < 340f)
                    meowX = Mathf.Max(baseStartX + 205f, rightEdge - 340f);

                float buttonsY = baseYPos + 35f;
                float displayY = baseYPos + 95f;
                float statusY = baseYPos + 130f;

                _titleLabel = new MenuLabel(_fancyMenu, _fancyMenu.pages[0], "MEOW",
                    new Vector2(meowX, displayY), new Vector2(50f, 20f), false);
                _fancyMenu.pages[0].subObjects.Add(_titleLabel);

                _prevButton = new SimpleButton(_fancyMenu, _fancyMenu.pages[0], "<", "MEOW_PREV",
                    new Vector2(meowX, buttonsY), new Vector2(40f, 30f));
                _fancyMenu.pages[0].subObjects.Add(_prevButton);

                _nextButton = new SimpleButton(_fancyMenu, _fancyMenu.pages[0], ">", "MEOW_NEXT",
                    new Vector2(meowX + 45f, buttonsY), new Vector2(40f, 30f));
                _fancyMenu.pages[0].subObjects.Add(_nextButton);

                float labelWidth = Mathf.Clamp((rightEdge - 220f) - (meowX + 90f), 80f, 400f);

                _meowLabel = new MenuLabel(_fancyMenu, _fancyMenu.pages[0], "",
                    new Vector2(meowX + 90f, displayY), new Vector2(labelWidth, 20f), false);
                _fancyMenu.pages[0].subObjects.Add(_meowLabel);

                _statusLabel = new MenuLabel(_fancyMenu, _fancyMenu.pages[0], "",
                    new Vector2(meowX, statusY), new Vector2(labelWidth, 20f), false);
                _fancyMenu.pages[0].subObjects.Add(_statusLabel);

                float sliderX = textBoxBorder.pos.x + 145f;
                _volumeHost = new VolumeSliderHost(_fancyMenu, _fancyMenu.pages[0], this);
                _fancyMenu.pages[0].subObjects.Add(_volumeHost);

                _volumeSlider = new VolumeSlider(_fancyMenu, _volumeHost,
                    new Vector2(sliderX, buttonsY), new Vector2(150f, 30f));
                _volumeHost.subObjects.Add(_volumeSlider);
                _fancyMenu.pages[0].selectables.Remove(_volumeSlider);

                _previewButton = new SimpleButton(_fancyMenu, _fancyMenu.pages[0], "PREVIEW", "MEOW_PREVIEW",
                    new Vector2(rightEdge - 140f, buttonsY + 100f), new Vector2(130f, 30f));
                _fancyMenu.pages[0].subObjects.Add(_previewButton);

                _uiAdded = true;
                _lastKnownSlugcat = _fancyMenu.selectedSlugcat;
                _lastMeadowState = MeowFancyMenuUI.IsMeadowModeOn();

                RefreshDisplay();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error inicializando panel: {ex.Message}");
            }
        }

        private void UpdateControlsAvailability(bool meadowOn)
        {
            if (_prevButton != null)
                _prevButton.inactive = !meadowOn;

            if (_nextButton != null)
                _nextButton.inactive = !meadowOn;

            if (_volumeSlider != null)
                _volumeSlider.buttonBehav.greyedOut = !meadowOn;
        }

        public void Tick()
        {
            if (!_uiAdded) return;

            try
            {
                bool meadowOn = MeowFancyMenuUI.IsMeadowModeOn();
                UpdateControlsAvailability(meadowOn);

                int currentProfile = MeowProfileManager.GetCurrentProfileNumber();
                string currentSteamId = currentProfile > 0 ? MeowProfileManager.GetSteamID(currentProfile) : "";

                if (meadowOn != _lastMeadowState ||
                    _fancyMenu.selectedSlugcat != _lastKnownSlugcat ||
                    currentProfile != _lastKnownProfile ||
                    currentSteamId != _lastKnownSteamId)
                {
                    _lastMeadowState = meadowOn;
                    _lastKnownSlugcat = _fancyMenu.selectedSlugcat;
                    _lastKnownProfile = currentProfile;
                    _lastKnownSteamId = currentSteamId;
                    RefreshDisplay();
                }

                if (_volumeSlider != null)
                {
                    bool dragging = _volumeSlider.mouseDragged;
                    if (_lastSliderDragged && !dragging)
                        CommitVolume();
                    _lastSliderDragged = dragging;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error en Tick: {ex.Message}");
            }
        }

        public void HandleSignal(string message)
        {
            if (message == "MEOW_PREVIEW")
            {
                PreviewMeow();
                return;
            }

            if (!MeowFancyMenuUI.IsMeadowModeOn())
            {
                _fancyMenu.PlaySound(SoundID.MENU_Greyed_Out_Button_Clicked);
                return;
            }

            string currentSlugcat = _fancyMenu.selectedSlugcat;
            List<MeowOption> currentOptions = MeowFancyMenuUI.GetOptionsForSlugcat(currentSlugcat);
            if (currentOptions == null || currentOptions.Count == 0) return;

            int dir = message == "MEOW_PREV" ? -1 : 1;

            _currentIndex = (_currentIndex + dir + currentOptions.Count) % currentOptions.Count;
            ApplySelectedOption();
        }

        public void RefreshDisplay()
        {
            try
            {
                _pendingVolume = null;
                int profile = MeowProfileManager.GetCurrentProfileNumber();
                if (profile <= 0)
                {
                    _meowLabel.text = "(default)";
                    _statusLabel.text = "Invalid profile";
                    _currentIndex = 0;
                    UpdateVolumeLabel();
                    return;
                }

                string currentSlugcat = _fancyMenu.selectedSlugcat;
                List<MeowOption> currentOptions = MeowFancyMenuUI.GetOptionsForSlugcat(currentSlugcat);
                var dto = MeowProfileManager.GetProfileCustomMeow(profile, currentSlugcat);

                string steamId = MeowProfileManager.GetSteamID(profile);
                _currentIndex = dto == null ? 0 : IndexOfOption(dto, currentOptions);
                _meowLabel.text = (_currentIndex >= 0 && _currentIndex < currentOptions.Count)
                    ? currentOptions[_currentIndex].Label
                    : "(custom)";

                if (!string.IsNullOrEmpty(steamId))
                    _statusLabel.text = $"Profile {profile} - {currentSlugcat}";
                else if (dto != null)
                    _statusLabel.text = $"Temp: {currentSlugcat} (set SteamID to save)";
                else
                    _statusLabel.text = $"Profile {profile} (unsaved - set SteamID)";

                UpdateVolumeLabel();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error refrescando display: {ex.Message}");
            }
        }

        private int IndexOfOption(CustomMeowDto dto, List<MeowOption> options)
        {
            if (options == null) return -1;

            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                if (string.Equals(opt.ShortMeowSoundId ?? "", dto.ShortMeowSoundId ?? "", StringComparison.Ordinal) &&
                    string.Equals(opt.LongMeowSoundId ?? "", dto.LongMeowSoundId ?? "", StringComparison.Ordinal) &&
                    string.Equals(opt.ShortMeowPupSoundId ?? "", dto.ShortMeowPupSoundId ?? "", StringComparison.Ordinal) &&
                    string.Equals(opt.LongMeowPupSoundId ?? "", dto.LongMeowPupSoundId ?? "", StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ApplySelectedOption()
        {
            try
            {
                int profile = MeowProfileManager.GetCurrentProfileNumber();
                if (profile <= 0)
                {
                    _statusLabel.text = "Invalid profile";
                    return;
                }

                string currentSlugcat = _fancyMenu.selectedSlugcat;
                List<MeowOption> currentOptions = MeowFancyMenuUI.GetOptionsForSlugcat(currentSlugcat);

                if (_currentIndex < 0 || _currentIndex >= currentOptions.Count)
                {
                    _currentIndex = 0;
                }

                var opt = currentOptions[_currentIndex];
                var dto = new CustomMeowDto
                {
                    ShortMeowSoundId = opt.ShortMeowSoundId,
                    LongMeowSoundId = opt.LongMeowSoundId,
                    ShortMeowPupSoundId = opt.ShortMeowPupSoundId,
                    LongMeowPupSoundId = opt.LongMeowPupSoundId,
                    VolumeMultiplier = opt.VolumeMultiplier,
                    LastUpdated = DateTime.Now
                };

                MeowProfileManager.SaveProfileOption(profile, currentSlugcat, dto);
                _pendingVolume = null;
                UpdateVolumeLabel();

                string steamId = MeowProfileManager.GetSteamID(profile);
                _meowLabel.text = opt.Label;
                _statusLabel.text = string.IsNullOrEmpty(steamId) ? $"Temp: {currentSlugcat} (set SteamID to save)" : $"Saved: {currentSlugcat} (profile {profile})";
                _fancyMenu.PlaySound(SoundID.MENY_Already_Selected_MultipleChoice_Clicked);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error guardando meow: {ex.Message}");
            }
        }

        internal float VolumeSliderValue()
        {
            return MeowVolumeManager.ToSliderValue(EffectiveVolume());
        }

        internal void SetVolumeSliderValue(float value)
        {
            _pendingVolume = MeowVolumeManager.FromSliderValue(value);
            UpdateVolumeLabel();
        }

        public void CommitVolume()
        {
            if (!_pendingVolume.HasValue) return;

            float volume = MeowVolumeManager.Normalize(_pendingVolume.Value);
            _pendingVolume = null;

            try
            {
                int profile = MeowProfileManager.GetCurrentProfileNumber();
                if (profile <= 0)
                {
                    UpdateVolumeLabel();
                    return;
                }

                string slugcat = _fancyMenu.selectedSlugcat;
                List<MeowOption> options = MeowFancyMenuUI.GetOptionsForSlugcat(slugcat);
                if (options == null || options.Count == 0)
                {
                    UpdateVolumeLabel();
                    return;
                }

                int index = Mathf.Clamp(_currentIndex, 0, options.Count - 1);
                MeowOption opt = options[index];
                CustomMeowDto dto = MeowProfileManager.GetProfileCustomMeow(profile, slugcat);

                bool matchesCurrentOption = dto != null &&
                    string.Equals(opt.ShortMeowSoundId ?? "", dto.ShortMeowSoundId ?? "", StringComparison.Ordinal) &&
                    string.Equals(opt.LongMeowSoundId ?? "", dto.LongMeowSoundId ?? "", StringComparison.Ordinal) &&
                    string.Equals(opt.ShortMeowPupSoundId ?? "", dto.ShortMeowPupSoundId ?? "", StringComparison.Ordinal) &&
                    string.Equals(opt.LongMeowPupSoundId ?? "", dto.LongMeowPupSoundId ?? "", StringComparison.Ordinal);

                if (!matchesCurrentOption)
                {
                    dto = new CustomMeowDto
                    {
                        ShortMeowSoundId = opt.ShortMeowSoundId,
                        LongMeowSoundId = opt.LongMeowSoundId,
                        ShortMeowPupSoundId = opt.ShortMeowPupSoundId,
                        LongMeowPupSoundId = opt.LongMeowPupSoundId,
                        VolumeMultiplier = opt.VolumeMultiplier,
                        LastUpdated = DateTime.Now
                    };
                }

                dto.VolumeMultiplier = volume;
                dto.LastUpdated = DateTime.Now;

                MeowProfileManager.SaveProfileOption(profile, slugcat, dto);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError($"[MeowFancyUI] Error guardando volumen: {ex.Message}");
            }

            UpdateVolumeLabel();
        }

        private float EffectiveVolume()
        {
            if (_pendingVolume.HasValue)
                return MeowVolumeManager.Normalize(_pendingVolume.Value);

            int profile = MeowProfileManager.GetCurrentProfileNumber();
            if (profile > 0)
            {
                return MeowVolumeManager.GetProfileVolume(profile, _fancyMenu.selectedSlugcat);
            }

            List<MeowOption> options = MeowFancyMenuUI.GetOptionsForSlugcat(_fancyMenu.selectedSlugcat);
            if (options != null && options.Count > 0)
            {
                int index = Mathf.Clamp(_currentIndex, 0, options.Count - 1);
                return MeowVolumeManager.Normalize(options[index].VolumeMultiplier);
            }

            return 1f;
        }

        private void UpdateVolumeLabel()
        {
            if (_volumeSlider?.menuLabel?.label == null) return;
            int percent = MeowVolumeManager.ToPercentage(EffectiveVolume());
            _volumeSlider.menuLabel.label.text = "VOLUME: " + percent + "%";
        }

        private void PreviewMeow()
        {
            List<SoundID> candidates = ResolvePreviewSound();
            if (candidates == null || candidates.Count == 0)
            {
                _fancyMenu.PlaySound(SoundID.MENU_Greyed_Out_Button_Clicked);
                return;
            }

            SoundID soundId = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (soundId == null)
            {
                _fancyMenu.PlaySound(SoundID.MENU_Greyed_Out_Button_Clicked);
                return;
            }

            float volume = MeowVolumeManager.GetPlaybackVolume(PushToMeowMain.ModSettings.MeowVolumeMultiplier.Value, EffectiveVolume()); ;
            _fancyMenu.PlaySound(soundId, 0f, volume, 1f);
        }

        private List<SoundID> ResolvePreviewSound()
        {
            var results = new List<SoundID>();
            string slugcatId = string.IsNullOrEmpty(_fancyMenu.selectedSlugcat) ? "White" : _fancyMenu.selectedSlugcat;
            bool isPup = string.Equals(slugcatId, "Slugpup", StringComparison.Ordinal);

            CustomMeow? customMeow = null;
            bool meadowMode = MeowFancyMenuUI.IsMeadowModeOn();
            if (meadowMode)
            {
                int profile = MeowProfileManager.GetCurrentProfileNumber();
                if (profile > 0)
                {
                    CustomMeowDto dto = MeowProfileManager.GetProfileCustomMeow(profile, slugcatId);
                    if (dto != null)
                    {
                        customMeow = MeowProfileManager.ConvertDtoToCustomMeow(dto);
                    }
                }
            }

            AddUniqueSound(results, ResolveSoundForVariant(customMeow, slugcatId, isPup, isShortMeow: false));
            AddUniqueSound(results, ResolveSoundForVariant(customMeow, slugcatId, isPup, isShortMeow: true));

            return results;
        }

        private static SoundID ResolveSoundForVariant(CustomMeow? customMeow, string slugcatId, bool isPup, bool isShortMeow)
        {
            SoundID meowType = null;

            if (customMeow.HasValue)
            {
                var meow = customMeow.Value;
                if (isPup)
                {
                    meowType = isShortMeow ? (meow.ShortMeowPupSoundID ?? meow.ShortMeowSoundID) : (meow.LongMeowPupSoundID ?? meow.LongMeowSoundID);
                }
                else
                {
                    meowType = isShortMeow ? meow.ShortMeowSoundID : meow.LongMeowSoundID;
                }
            }

            if (meowType == null)
            {
                meowType = ResolvePushToMeowDefaultSound(slugcatId, isPup, isShortMeow);
            }

            return meowType;
        }

        private static SoundID ResolvePushToMeowDefaultSound(string slugcatId, bool isPup, bool isShortMeow)
        {
            if (string.Equals(slugcatId, "Rivulet", StringComparison.Ordinal) && !MeowUtils.CustomMeows.ContainsKey(slugcatId))
            {
                bool alt = PushToMeowMain.ModSettings.AltRivuletSounds.Value;
                return isShortMeow ? (alt ? MeowUtils.SlugcatMeowRivuletBShort : MeowUtils.SlugcatMeowRivuletAShort) : (alt ? MeowUtils.SlugcatMeowRivuletB : MeowUtils.SlugcatMeowRivuletA);
            }

            if (!MeowUtils.CustomMeows.ContainsKey(slugcatId))
            {
                return isPup ? (isShortMeow ? MeowUtils.SlugcatMeowPupShort : MeowUtils.SlugcatMeowPup) : (isShortMeow ? MeowUtils.SlugcatMeowNormalShort : MeowUtils.SlugcatMeowNormal);
            }

            CustomMeow? customMeow = MeowUtils.CustomMeows[slugcatId];
            if (customMeow == null)
                return null;

            if (isPup)
            {
                return isShortMeow ? (customMeow.Value.ShortMeowPupSoundID ?? customMeow.Value.ShortMeowSoundID) : (customMeow.Value.LongMeowPupSoundID ?? customMeow.Value.LongMeowSoundID);
            }

            return isShortMeow ? customMeow.Value.ShortMeowSoundID : customMeow.Value.LongMeowSoundID;
        }

        private static void AddUniqueSound(List<SoundID> list, SoundID sound)
        {
            if (sound != null && !list.Contains(sound))
            {
                list.Add(sound);
            }
        }
    }

    public class MeowOption
    {
        public string Label;
        public string ShortMeowSoundId = "";
        public string LongMeowSoundId = "";
        public string ShortMeowPupSoundId = "";
        public string LongMeowPupSoundId = "";
        public float VolumeMultiplier = 1f;

        public static MeowOption CreateDefault()
        {
            return new MeowOption { Label = "(default)" };
        }
    }
}