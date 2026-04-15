using System.Collections.Generic;
using BreathCasino.Core;
using BreathCasino.Systems;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BreathCasino.Gameplay
{
    public class BCStartMenuController : MonoBehaviour
    {
        private const string PrefLanguageIndex = "bc.menu.language";
        private const string PrefResolutionIndex = "bc.menu.resolution";
        private const string PrefFullscreenMode = "bc.menu.fullscreen";
        private const string PrefDynamicLighting = "bc.menu.dynamic_lighting";
        private const string PrefGraphicsApi = "bc.menu.graphics_api";
        private const string PrefUpscalingMode = "bc.menu.upscaling";
        private const string PrefShadingStyle = "bc.menu.shading_style";

        private enum MenuSection
        {
            Overview,
            Settings,
            Credits,
            ExitConfirm
        }

        private enum GraphicsApiPreference
        {
            Auto,
            DX11,
            DX12,
            Vulkan
        }

        private enum UpscalingMode
        {
            Native,
            Bilinear,
            FsrUltraQuality,
            FsrQuality,
            FsrBalanced,
            FsrPerformance,
            TemporalStp
        }

        private enum ShadingStyle
        {
            Smooth,
            FlatShade
        }

        [SerializeField] private Canvas menuCanvas;
        [SerializeField] private Image backdropImage;
        [SerializeField] private RectTransform sidebarPanel;
        [SerializeField] private RectTransform overviewPanel;
        [SerializeField] private RectTransform settingsPanel;
        [SerializeField] private ScrollRect settingsScrollRect;
        [SerializeField] private RectTransform settingsScrollContent;
        [SerializeField] private RectTransform settingsAdvancedPanel;
        [SerializeField] private RectTransform creditsPanel;
        [SerializeField] private RectTransform exitConfirmPanel;
        [SerializeField] private RectTransform languagePanel;
        [SerializeField] private Button languageButton;
        [SerializeField] private Button settingsAdvancedToggleButton;
        [SerializeField] private Text languageButtonText;
        [SerializeField] private Text settingsAdvancedToggleText;
        [SerializeField] private Text resolutionValueText;
        [SerializeField] private Text fullscreenValueText;
        [SerializeField] private Text graphicsApiValueText;
        [SerializeField] private Text graphicsApiInfoText;
        [SerializeField] private Text upscalingValueText;
        [SerializeField] private Text upscalingInfoText;
        [SerializeField] private Text shadingStyleValueText;
        [SerializeField] private Text shadingStyleInfoText;
        [SerializeField] private Text settingsStatusText;
        [SerializeField] private Toggle dynamicLightingToggle;

        private static readonly (string Label, string Code)[] SupportedLanguages =
        {
            ("English", "EN"),
            ("Русский", "RU"),
            ("Українська", "UK"),
            ("Portuguese", "PT"),
            ("Polish", "PL")
        };

        private static readonly (string Key, FullScreenMode Mode)[] FullscreenOptions =
        {
            ("screen.fullscreen_window", FullScreenMode.FullScreenWindow),
            ("screen.windowed", FullScreenMode.Windowed),
            ("screen.exclusive_fullscreen", FullScreenMode.ExclusiveFullScreen)
        };

        private static readonly Vector2Int[] CommonResolutions =
        {
            new(1280, 720),
            new(1366, 768),
            new(1600, 900),
            new(1920, 1080),
            new(2560, 1440),
            new(3840, 2160)
        };

        private SceneBootstrap _bootstrap;
        private readonly List<GameObject> _sectionObjects = new();
        private readonly List<Resolution> _availableResolutions = new();
        private int _currentLanguageIndex;
        private int _selectedResolutionIndex;
        private int _selectedFullscreenIndex;
        private int _selectedGraphicsApiIndex;
        private int _selectedUpscalingIndex;
        private int _selectedShadingStyleIndex;
        private int _appliedResolutionIndex;
        private int _appliedFullscreenIndex;
        private int _appliedGraphicsApiIndex;
        private int _appliedUpscalingIndex;
        private int _appliedShadingStyleIndex;
        private bool _appliedDynamicLightingEnabled = true;
        private bool _dynamicLightingEnabled = true;
        private bool _settingsDirty;
        private bool _advancedSettingsExpanded;
        private MenuSection _activeSection = MenuSection.Overview;

        public void Initialize(SceneBootstrap bootstrap)
        {
            _bootstrap = bootstrap;
            LoadPreferences();
            EnsureUi();
            InitializeGraphicsSettings();
            SetMenuVisible(true);
            ShowOverview();
        }

        private void Awake()
        {
            EnsureEventSystem();
            LoadPreferences();
            EnsureUi();
        }

        private void EnsureUi()
        {
            EnsureTooltipDisplay();

            if (menuCanvas == null)
            {
                GameObject canvasObject = new GameObject("StartMenuCanvas");
                menuCanvas = canvasObject.AddComponent<Canvas>();
                menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                menuCanvas.sortingOrder = 6000;
                CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            menuCanvas.transform.SetParent(null, false);
            menuCanvas.transform.localScale = Vector3.one;
            menuCanvas.transform.localPosition = Vector3.zero;
            menuCanvas.transform.localRotation = Quaternion.identity;

            RebuildRuntimeUi();
        }

        private void RebuildRuntimeUi()
        {
            ClearCanvasChildren();

            _sectionObjects.Clear();
            backdropImage = null;
            sidebarPanel = null;
            overviewPanel = null;
            settingsPanel = null;
            settingsScrollRect = null;
            settingsScrollContent = null;
            settingsAdvancedPanel = null;
            creditsPanel = null;
            exitConfirmPanel = null;
            languagePanel = null;
            languageButton = null;
            settingsAdvancedToggleButton = null;
            languageButtonText = null;
            settingsAdvancedToggleText = null;
            resolutionValueText = null;
            fullscreenValueText = null;
            graphicsApiValueText = null;
            graphicsApiInfoText = null;
            upscalingValueText = null;
            upscalingInfoText = null;
            shadingStyleValueText = null;
            shadingStyleInfoText = null;
            settingsStatusText = null;
            dynamicLightingToggle = null;

            GameObject backdropObject = new GameObject("MenuBackdrop");
            backdropObject.transform.SetParent(menuCanvas.transform, false);
            RectTransform backdropRect = StretchFullScreen(backdropObject);
            backdropRect.SetAsFirstSibling();
            backdropImage = backdropObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 1f);

            sidebarPanel = CreateLeftColumnPanel("SidebarPanel", menuCanvas.transform, 460f, new Color(0.03f, 0.04f, 0.055f, 0.98f));
            VerticalLayoutGroup sidebarLayout = sidebarPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            sidebarLayout.childAlignment = TextAnchor.UpperLeft;
            sidebarLayout.spacing = 14f;
            sidebarLayout.padding = new RectOffset(40, 40, 56, 40);
            sidebarLayout.childControlWidth = true;
            sidebarLayout.childForceExpandWidth = true;

            CreateHeadingText(sidebarPanel, BCLocalization.Get("menu.title"), 42, TextAnchor.MiddleLeft);
            CreateText(sidebarPanel, Translate(
                "A finished scene target with a live mechanical duel loop.",
                "Активная игровая сцена с механическим дуэльным циклом.",
                "Активна ігрова сцена з механічною дуельною петлею.",
                "Cena alvo ativa com um ciclo de duelo mecânico.",
                "Aktywna scena docelowa z mechaniczną pętlą pojedynku."), 18, FontStyle.Normal, new Color(0.77f, 0.83f, 0.90f), TextAnchor.MiddleLeft);
            CreateSpacer(sidebarPanel, 20f);

            CreateButton(sidebarPanel, BCLocalization.Get("menu.start"), StartGame);
            CreateButton(sidebarPanel, BCLocalization.Get("menu.overview"), ShowOverview);
            CreateButton(sidebarPanel, BCLocalization.Get("menu.settings"), ShowSettings);
            CreateButton(sidebarPanel, BCLocalization.Get("menu.credits"), ShowCredits);
            CreateButton(sidebarPanel, BCLocalization.Get("menu.exit"), ShowExitConfirmation);

            overviewPanel = CreateContentPanel("OverviewPanel");
            CreateHeadingText(overviewPanel, BCLocalization.Get("menu.overview.title"), 30, TextAnchor.MiddleLeft);
            CreateText(overviewPanel, BuildOverviewBody(), 20, FontStyle.Normal, null, TextAnchor.UpperLeft);

            settingsPanel = CreateContentPanel("SettingsPanel");
            CreateText(settingsPanel, BCLocalization.Get("menu.settings.title"), 30, FontStyle.Bold, null, TextAnchor.MiddleLeft);
            dynamicLightingToggle = CreateToggle(settingsPanel, BCLocalization.Get("menu.settings.dynamic"), true, SetDynamicLighting);
            resolutionValueText = CreateSelector(settingsPanel, BCLocalization.Get("menu.settings.resolution"), () => CycleResolution(-1), () => CycleResolution(1));
            fullscreenValueText = CreateSelector(settingsPanel, BCLocalization.Get("menu.settings.display"), () => CycleFullscreen(-1), () => CycleFullscreen(1));
            graphicsApiValueText = CreateSelector(
                settingsPanel,
                Translate("Renderer API", "Рендер API", "Рендер API", "API de render", "API renderera"),
                () => CycleGraphicsApi(-1),
                () => CycleGraphicsApi(1));
            AttachTooltip(graphicsApiValueText, BuildGraphicsApiTooltip());
            graphicsApiInfoText = CreateInfoText(settingsPanel, string.Empty);

            upscalingValueText = CreateSelector(
                settingsPanel,
                Translate("Upscaling / FSR", "Апскейл / FSR", "Апскейл / FSR", "Upscaling / FSR", "Upscaling / FSR"),
                () => CycleUpscaling(-1),
                () => CycleUpscaling(1));
            AttachTooltip(upscalingValueText, BuildUpscalingTooltip());
            upscalingInfoText = CreateInfoText(settingsPanel, string.Empty);

            shadingStyleValueText = CreateSelector(
                settingsPanel,
                Translate("Light Style", "Ð¡Ñ‚Ð¸Ð»ÑŒ ÑÐ²ÐµÑ‚Ð°", "Ð¡Ñ‚Ð¸Ð»ÑŒ ÑÐ²Ñ–Ñ‚Ð»Ð°", "Estilo de luz", "Styl swiatla"),
                () => CycleShadingStyle(-1),
                () => CycleShadingStyle(1));
            AttachTooltip(shadingStyleValueText, BuildShadingTooltip());
            shadingStyleInfoText = CreateInfoText(settingsPanel, string.Empty);

            RectTransform settingsButtons = CreateHorizontalRow(settingsPanel, "SettingsButtons", 18f);
            CreateButton(settingsButtons, Translate("Apply", "ÐŸÑ€Ð¸Ð½ÑÑ‚ÑŒ", "Ð—Ð°ÑÑ‚Ð¾ÑÑƒÐ²Ð°Ñ‚Ð¸", "Aplicar", "Zastosuj"), ApplySettings, 180f);
            CreateButton(settingsButtons, Translate("Reset", "Ð¡Ð±Ñ€Ð¾Ñ", "Ð¡ÐºÐ¸Ð½ÑƒÑ‚Ð¸", "Repor", "Reset"), ResetSettings, 180f);
            settingsStatusText = CreateInfoText(settingsPanel, string.Empty);
            RebuildSettingsPanelContent();

            creditsPanel = CreateContentPanel("CreditsPanel");
            ClearPanelChildren(creditsPanel);
            CreateHeadingText(creditsPanel, BCLocalization.Get("menu.credits.title"), 30, TextAnchor.MiddleLeft);
            CreateText(creditsPanel, BuildCreditsBody(), 20, FontStyle.Normal, null, TextAnchor.UpperLeft);

            exitConfirmPanel = CreateCenteredDialog("ExitConfirmPanel", new Vector2(520f, 280f));
            CreateText(exitConfirmPanel, BCLocalization.Get("menu.exit.title"), 30, FontStyle.Bold, null, TextAnchor.MiddleCenter);
            CreateText(exitConfirmPanel, BCLocalization.Get("menu.exit.confirm"), 22, FontStyle.Normal, null, TextAnchor.MiddleCenter);
            RectTransform buttonRow = CreateHorizontalRow(exitConfirmPanel, "ExitButtons", 20f);
            CreateButton(buttonRow, BCLocalization.Get("menu.yes"), ConfirmExit, 180f);
            CreateButton(buttonRow, BCLocalization.Get("menu.no"), ShowOverview, 180f);

            RectTransform languageRoot = CreateCornerButtonRoot("LanguageButtonRoot");
            languageButton = CreateButton(languageRoot, string.Empty, ToggleLanguageMenu, 112f);
            languageButtonText = languageButton.GetComponentInChildren<Text>();
            UpdateLanguageButtonLabel();
            languagePanel = CreateLanguagePanel(languageRoot);

            RegisterSection(overviewPanel);
            RegisterSection(settingsPanel);
            RegisterSection(creditsPanel);
            RegisterSection(exitConfirmPanel);
            ShowCurrentSection();
        }

        private void InitializeGraphicsSettings()
        {
            BuildResolutionOptions();
            BuildFullscreenOptions();
            BuildGraphicsApiOptions();
            BuildUpscalingOptions();
            BuildShadingOptions();

            if (dynamicLightingToggle != null)
            {
                dynamicLightingToggle.SetIsOnWithoutNotify(_dynamicLightingEnabled);
            }

            SyncAppliedStateToSelection();
            ApplySettingsToRuntime(savePreferences: false);
            UpdateSettingsStatusLabel();
        }

        private void BuildResolutionOptions()
        {
            _availableResolutions.Clear();

            HashSet<string> seen = new();
            void AddResolution(int width, int height, RefreshRate refreshRate)
            {
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                string key = $"{width}x{height}";
                if (!seen.Add(key))
                {
                    return;
                }

                _availableResolutions.Add(new Resolution
                {
                    width = width,
                    height = height,
                    refreshRateRatio = refreshRate
                });
            }

            Resolution[] allResolutions = Screen.resolutions;
            for (int i = 0; i < allResolutions.Length; i++)
            {
                AddResolution(allResolutions[i].width, allResolutions[i].height, allResolutions[i].refreshRateRatio);
            }

            AddResolution(Screen.currentResolution.width, Screen.currentResolution.height, Screen.currentResolution.refreshRateRatio);
            AddResolution(Screen.width, Screen.height, Screen.currentResolution.refreshRateRatio);

            for (int i = 0; i < CommonResolutions.Length; i++)
            {
                AddResolution(CommonResolutions[i].x, CommonResolutions[i].y, Screen.currentResolution.refreshRateRatio);
            }

            _availableResolutions.Sort((a, b) =>
            {
                int areaCompare = (b.width * b.height).CompareTo(a.width * a.height);
                return areaCompare != 0 ? areaCompare : b.width.CompareTo(a.width);
            });

            int currentIndex = 0;
            for (int i = 0; i < _availableResolutions.Count; i++)
            {
                Resolution resolution = _availableResolutions[i];
                if (resolution.width == Screen.width && resolution.height == Screen.height)
                {
                    currentIndex = i;
                }
            }

            _selectedResolutionIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefResolutionIndex, currentIndex), 0, Mathf.Max(_availableResolutions.Count - 1, 0));
            UpdateResolutionSelectorLabel();
        }

        private void BuildFullscreenOptions()
        {
            _selectedFullscreenIndex = Mathf.Clamp(
                FindFullscreenIndex((FullScreenMode)PlayerPrefs.GetInt(PrefFullscreenMode, (int)Screen.fullScreenMode)),
                0,
                FullscreenOptions.Length - 1);
            UpdateFullscreenSelectorLabel();
        }

        private void BuildGraphicsApiOptions()
        {
            _selectedGraphicsApiIndex = Mathf.Clamp(
                PlayerPrefs.GetInt(PrefGraphicsApi, (int)GraphicsApiPreference.Auto),
                0,
                (int)GraphicsApiPreference.Vulkan);
            UpdateGraphicsApiSelectorLabel();
            UpdateGraphicsApiInfoLabel();
        }

        private void BuildUpscalingOptions()
        {
            _selectedUpscalingIndex = Mathf.Clamp(
                PlayerPrefs.GetInt(PrefUpscalingMode, (int)UpscalingMode.Native),
                0,
                (int)UpscalingMode.TemporalStp);
            UpdateUpscalingSelectorLabel();
            UpdateUpscalingInfoLabel();
        }

        private void BuildShadingOptions()
        {
            _selectedShadingStyleIndex = Mathf.Clamp(
                PlayerPrefs.GetInt(PrefShadingStyle, (int)ShadingStyle.FlatShade),
                0,
                (int)ShadingStyle.FlatShade);
            UpdateShadingSelectorLabel();
            UpdateShadingInfoLabel();
        }

        private void SetMenuVisible(bool visible)
        {
            if (menuCanvas != null)
            {
                menuCanvas.gameObject.SetActive(visible);
            }

            _bootstrap?.SetCameraMenuLock(visible);
            _bootstrap?.SetGameplayPresentationVisible(!visible);
        }

        private void StartGame()
        {
            SetMenuVisible(false);
            _bootstrap?.BeginGameFromMenu();
        }

        private void ShowOverview()
        {
            _activeSection = MenuSection.Overview;
            ShowCurrentSection();
        }

        private void ShowSettings()
        {
            _activeSection = MenuSection.Settings;
            ShowCurrentSection();
        }

        private void ShowCredits()
        {
            _activeSection = MenuSection.Credits;
            ShowCurrentSection();
        }

        private void ShowExitConfirmation()
        {
            _activeSection = MenuSection.ExitConfirm;
            ShowCurrentSection();
        }

        private void ShowCurrentSection()
        {
            HideAllSections();
            HideLanguageMenu();
            SetMenuVisible(true);

            RectTransform panel = _activeSection switch
            {
                MenuSection.Settings => settingsPanel,
                MenuSection.Credits => creditsPanel,
                MenuSection.ExitConfirm => exitConfirmPanel,
                _ => overviewPanel
            };

            if (panel != null)
            {
                panel.gameObject.SetActive(true);
            }
        }

        private void HideAllSections()
        {
            for (int i = 0; i < _sectionObjects.Count; i++)
            {
                if (_sectionObjects[i] != null)
                {
                    _sectionObjects[i].SetActive(false);
                }
            }
        }

        private void SetDynamicLighting(bool enabled)
        {
            _dynamicLightingEnabled = enabled;
            MarkSettingsDirty();
        }

        private void CycleResolution(int direction)
        {
            if (_availableResolutions.Count == 0)
            {
                return;
            }

            _selectedResolutionIndex = WrapIndex(_selectedResolutionIndex + direction, _availableResolutions.Count);
            UpdateResolutionSelectorLabel();
            MarkSettingsDirty();
        }

        private void CycleFullscreen(int direction)
        {
            _selectedFullscreenIndex = WrapIndex(_selectedFullscreenIndex + direction, FullscreenOptions.Length);
            UpdateFullscreenSelectorLabel();
            MarkSettingsDirty();
        }

        private void CycleGraphicsApi(int direction)
        {
            int count = (int)GraphicsApiPreference.Vulkan + 1;
            _selectedGraphicsApiIndex = WrapIndex(_selectedGraphicsApiIndex + direction, count);
            UpdateGraphicsApiSelectorLabel();
            UpdateGraphicsApiInfoLabel();
            MarkSettingsDirty();
        }

        private void CycleUpscaling(int direction)
        {
            int count = (int)UpscalingMode.TemporalStp + 1;
            _selectedUpscalingIndex = WrapIndex(_selectedUpscalingIndex + direction, count);
            UpdateUpscalingSelectorLabel();
            UpdateUpscalingInfoLabel();
            MarkSettingsDirty();
        }

        private void CycleShadingStyle(int direction)
        {
            int count = (int)ShadingStyle.FlatShade + 1;
            _selectedShadingStyleIndex = WrapIndex(_selectedShadingStyleIndex + direction, count);
            UpdateShadingSelectorLabel();
            UpdateShadingInfoLabel();
            MarkSettingsDirty();
        }

        private void ToggleLanguageMenu()
        {
            if (languagePanel == null)
            {
                return;
            }

            languagePanel.gameObject.SetActive(!languagePanel.gameObject.activeSelf);
        }

        private void HideLanguageMenu()
        {
            if (languagePanel != null)
            {
                languagePanel.gameObject.SetActive(false);
            }
        }

        private void SelectLanguage(int index)
        {
            if (index < 0 || index >= SupportedLanguages.Length)
            {
                return;
            }

            _currentLanguageIndex = index;
            BCLocalization.SetLanguage(SupportedLanguages[_currentLanguageIndex].Code);
            PlayerPrefs.SetInt(PrefLanguageIndex, _currentLanguageIndex);
            PlayerPrefs.Save();

            RebuildRuntimeUi();
            InitializeGraphicsSettings();
        }

        private void UpdateLanguageButtonLabel()
        {
            if (languageButtonText != null)
            {
                languageButtonText.text = SupportedLanguages[_currentLanguageIndex].Code + " v";
            }
        }

        private void LoadPreferences()
        {
            _currentLanguageIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefLanguageIndex, 0), 0, SupportedLanguages.Length - 1);
            _dynamicLightingEnabled = PlayerPrefs.GetInt(PrefDynamicLighting, 1) == 1;
            BCLocalization.SetLanguage(SupportedLanguages[_currentLanguageIndex].Code);
        }

        private static int FindFullscreenIndex(FullScreenMode mode)
        {
            for (int i = 0; i < FullscreenOptions.Length; i++)
            {
                if (FullscreenOptions[i].Mode == mode)
                {
                    return i;
                }
            }

            return 0;
        }

        private Resolution GetCurrentSelectedResolution()
        {
            if (_availableResolutions.Count == 0)
            {
                return Screen.currentResolution;
            }

            int index = Mathf.Clamp(_selectedResolutionIndex, 0, _availableResolutions.Count - 1);
            return _availableResolutions[index];
        }

        private FullScreenMode GetCurrentFullscreenMode()
        {
            int index = Mathf.Clamp(_selectedFullscreenIndex, 0, FullscreenOptions.Length - 1);
            return FullscreenOptions[index].Mode;
        }

        private void UpdateResolutionSelectorLabel()
        {
            if (resolutionValueText == null)
            {
                return;
            }

            if (_availableResolutions.Count == 0)
            {
                resolutionValueText.text = $"{Screen.width} x {Screen.height}";
                return;
            }

            Resolution resolution = GetCurrentSelectedResolution();
            resolutionValueText.text = $"{resolution.width} x {resolution.height}";
        }

        private void UpdateFullscreenSelectorLabel()
        {
            if (fullscreenValueText == null)
            {
                return;
            }

            int index = Mathf.Clamp(_selectedFullscreenIndex, 0, FullscreenOptions.Length - 1);
            fullscreenValueText.text = BCLocalization.Get(FullscreenOptions[index].Key);
        }

        private void UpdateGraphicsApiSelectorLabel()
        {
            if (graphicsApiValueText == null)
            {
                return;
            }

            GraphicsApiPreference preference = (GraphicsApiPreference)Mathf.Clamp(_selectedGraphicsApiIndex, 0, (int)GraphicsApiPreference.Vulkan);
            graphicsApiValueText.text = GetGraphicsApiPreferenceLabel(preference);
        }

        private void UpdateGraphicsApiInfoLabel()
        {
            if (graphicsApiInfoText == null)
            {
                return;
            }

            GraphicsApiPreference preference = (GraphicsApiPreference)Mathf.Clamp(_selectedGraphicsApiIndex, 0, (int)GraphicsApiPreference.Vulkan);
            string currentApi = GetGraphicsDeviceTypeLabel(SystemInfo.graphicsDeviceType);
            string note = preference == GraphicsApiPreference.Auto
                ? Translate(
                    "Auto follows the build/player configuration. Changing API needs a restart and a build that includes that backend.",
                    "Auto следует конфигурации билда/плеера. Смена API требует рестарта и билда, где этот backend включен.",
                    "Auto слідує конфігурації білда/плеєра. Зміна API потребує перезапуску й білда, де цей backend увімкнений.",
                    "Auto segue a configuracao do build/player. Trocar a API exige reinicio e um build com esse backend incluído.",
                    "Auto korzysta z konfiguracji buildu/playera. Zmiana API wymaga restartu i buildu z danym backendem.")
                : Translate(
                    "The selected preference takes effect only after Apply, restart, and only if the standalone build supports that renderer.",
                    "Выбранное предпочтение сработает только после Принять, рестарта и только если standalone-билд поддерживает этот рендер.",
                    "Вибране налаштування спрацює лише після Застосувати, перезапуску і лише якщо standalone-білд підтримує цей рендер.",
                    "A preferencia escolhida so entra em vigor depois de Aplicar, reiniciar, e apenas se o build standalone suportar esse render.",
                    "Wybrana preferencja zacznie dzialac dopiero po Zastosuj, restarcie i tylko wtedy, gdy build standalone obsluguje ten renderer.");

            graphicsApiInfoText.text =
                $"{Translate("Current", "Текущий", "Поточний", "Atual", "Aktualny")}: {currentApi}\n{note}";
        }

        private void UpdateUpscalingSelectorLabel()
        {
            if (upscalingValueText == null)
            {
                return;
            }

            UpscalingMode mode = (UpscalingMode)Mathf.Clamp(_selectedUpscalingIndex, 0, (int)UpscalingMode.TemporalStp);
            upscalingValueText.text = GetUpscalingLabel(mode);
        }

        private void UpdateUpscalingInfoLabel()
        {
            if (upscalingInfoText == null)
            {
                return;
            }

            string note = Translate(
                "Available global modes: Native, Bilinear, FSR 1.0 presets, and STP temporal. This setup does not expose real FSR 2 or FSR 3 switching.",
                "Доступные глобальные режимы: Native, Bilinear, пресеты FSR 1.0 и temporal STP. Настоящего переключения FSR 2 или FSR 3 в этой сборке нет.",
                "Доступні глобальні режими: Native, Bilinear, пресети FSR 1.0 і temporal STP. Справжнього перемикання FSR 2 або FSR 3 у цій збірці немає.",
                "Modos globais disponiveis: Native, Bilinear, presets de FSR 1.0 e STP temporal. Esta configuracao nao expoe a troca real para FSR 2 ou FSR 3.",
                "Dostepne tryby globalne: Native, Bilinear, presety FSR 1.0 oraz temporalne STP. Ta konfiguracja nie udostepnia prawdziwego przelaczania FSR 2 lub FSR 3.");

            upscalingInfoText.text = note;
        }

        private void UpdateShadingSelectorLabel()
        {
            if (shadingStyleValueText == null)
            {
                return;
            }

            ShadingStyle style = (ShadingStyle)Mathf.Clamp(_selectedShadingStyleIndex, 0, (int)ShadingStyle.FlatShade);
            shadingStyleValueText.text = style == ShadingStyle.FlatShade
                ? Translate("Flat Shade", "Flat Shade", "Flat Shade", "Flat Shade", "Flat Shade")
                : Translate("Smooth", "Плавный", "Плавний", "Suave", "Plynny");
        }

        private void UpdateShadingInfoLabel()
        {
            if (shadingStyleInfoText == null)
            {
                return;
            }

            ShadingStyle style = (ShadingStyle)Mathf.Clamp(_selectedShadingStyleIndex, 0, (int)ShadingStyle.FlatShade);
            shadingStyleInfoText.text = style == ShadingStyle.FlatShade
                ? Translate(
                    "Global hard light with posterized shading and hard main shadows.",
                    "Глобальный жёсткий свет со ступенчатым шейдингом и жёсткими главными тенями.",
                    "Глобальне жорстке світло зі ступінчастим шейдингом і жорсткими головними тінями.",
                    "Luz global dura com shading posterizado e sombras principais duras.",
                    "Globalne twarde swiatlo z posterized shading i twardymi cieniami glownego swiatla.")
                : Translate(
                    "Default smoother lighting and softer scene fill.",
                    "Стандартный более плавный свет и мягкое заполнение сцены.",
                    "Стандартне плавніше світло і м'якше заповнення сцени.",
                    "Iluminacao padrao mais suave e preenchimento mais macio da cena.",
                    "Domyslne plynniejsze oswietlenie i lagodniejsze wypelnienie sceny.");
        }

        private void ApplySettings()
        {
            ApplySettingsToRuntime(savePreferences: true);
            SyncAppliedStateToSelection();
            UpdateSettingsStatusLabel();
        }

        private void ResetSettings()
        {
            int defaultResolutionIndex = 0;
            for (int i = 0; i < _availableResolutions.Count; i++)
            {
                Resolution resolution = _availableResolutions[i];
                if (resolution.width == Screen.currentResolution.width && resolution.height == Screen.currentResolution.height)
                {
                    defaultResolutionIndex = i;
                    break;
                }
            }

            _selectedResolutionIndex = Mathf.Clamp(defaultResolutionIndex, 0, Mathf.Max(_availableResolutions.Count - 1, 0));
            _selectedFullscreenIndex = FindFullscreenIndex(FullScreenMode.FullScreenWindow);
            _selectedGraphicsApiIndex = (int)GraphicsApiPreference.Auto;
            _selectedUpscalingIndex = (int)UpscalingMode.FsrQuality;
            _selectedShadingStyleIndex = (int)ShadingStyle.FlatShade;
            _dynamicLightingEnabled = true;

            if (dynamicLightingToggle != null)
            {
                dynamicLightingToggle.SetIsOnWithoutNotify(_dynamicLightingEnabled);
            }

            UpdateResolutionSelectorLabel();
            UpdateFullscreenSelectorLabel();
            UpdateGraphicsApiSelectorLabel();
            UpdateGraphicsApiInfoLabel();
            UpdateUpscalingSelectorLabel();
            UpdateUpscalingInfoLabel();
            UpdateShadingSelectorLabel();
            UpdateShadingInfoLabel();
            MarkSettingsDirty();
        }

        private void ApplySettingsToRuntime(bool savePreferences)
        {
            Resolution resolution = GetCurrentSelectedResolution();
            FullScreenMode mode = GetCurrentFullscreenMode();
            Screen.SetResolution(resolution.width, resolution.height, mode, resolution.refreshRateRatio);

            _bootstrap?.SetDynamicLightingEnabled(_dynamicLightingEnabled);

            ShadingStyle shadingStyle = (ShadingStyle)Mathf.Clamp(_selectedShadingStyleIndex, 0, (int)ShadingStyle.FlatShade);
            _bootstrap?.SetFlatShadeLightingEnabled(shadingStyle == ShadingStyle.FlatShade);

            ApplyUpscalingSetting();

            if (savePreferences)
            {
                PlayerPrefs.SetInt(PrefResolutionIndex, _selectedResolutionIndex);
                PlayerPrefs.SetInt(PrefFullscreenMode, (int)mode);
                PlayerPrefs.SetInt(PrefGraphicsApi, _selectedGraphicsApiIndex);
                PlayerPrefs.SetInt(PrefUpscalingMode, _selectedUpscalingIndex);
                PlayerPrefs.SetInt(PrefShadingStyle, _selectedShadingStyleIndex);
                PlayerPrefs.SetInt(PrefDynamicLighting, _dynamicLightingEnabled ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void SyncAppliedStateToSelection()
        {
            _appliedResolutionIndex = _selectedResolutionIndex;
            _appliedFullscreenIndex = _selectedFullscreenIndex;
            _appliedGraphicsApiIndex = _selectedGraphicsApiIndex;
            _appliedUpscalingIndex = _selectedUpscalingIndex;
            _appliedShadingStyleIndex = _selectedShadingStyleIndex;
            _appliedDynamicLightingEnabled = _dynamicLightingEnabled;
            _settingsDirty = false;
        }

        private void MarkSettingsDirty()
        {
            _settingsDirty =
                _selectedResolutionIndex != _appliedResolutionIndex ||
                _selectedFullscreenIndex != _appliedFullscreenIndex ||
                _selectedGraphicsApiIndex != _appliedGraphicsApiIndex ||
                _selectedUpscalingIndex != _appliedUpscalingIndex ||
                _selectedShadingStyleIndex != _appliedShadingStyleIndex ||
                _dynamicLightingEnabled != _appliedDynamicLightingEnabled;

            UpdateSettingsStatusLabel();
        }

        private void UpdateSettingsStatusLabel()
        {
            if (settingsStatusText == null)
            {
                return;
            }

            settingsStatusText.text = _settingsDirty
                ? Translate(
                    "Pending changes. Apply to save and activate them.",
                    "Есть несохранённые изменения. Нажми Принять, чтобы сохранить и включить их.",
                    "Є незбережені зміни. Натисни Застосувати, щоб зберегти і ввімкнути їх.",
                    "Existem alteracoes pendentes. Usa Aplicar para guardar e ativar.",
                    "Sa niezapisane zmiany. Uzyj Zastosuj, aby je zapisac i aktywowac.")
                : Translate(
                    "Settings are applied.",
                    "Настройки применены.",
                    "Налаштування застосовані.",
                    "As definicoes foram aplicadas.",
                    "Ustawienia sa zastosowane.");
        }

        private static int WrapIndex(int value, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            int wrapped = value % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private void ApplyUpscalingSetting()
        {
            UniversalRenderPipelineAsset urpAsset = GetActiveUrpAsset();
            if (urpAsset == null)
            {
                return;
            }

            UpscalingMode mode = (UpscalingMode)Mathf.Clamp(_selectedUpscalingIndex, 0, (int)UpscalingMode.TemporalStp);
            urpAsset.fsrOverrideSharpness = true;

            switch (mode)
            {
                case UpscalingMode.Bilinear:
                    urpAsset.renderScale = 0.8f;
                    urpAsset.upscalingFilter = UpscalingFilterSelection.Linear;
                    urpAsset.fsrSharpness = 0f;
                    urpAsset.fsrOverrideSharpness = false;
                    break;
                case UpscalingMode.FsrUltraQuality:
                    urpAsset.renderScale = 0.88f;
                    urpAsset.upscalingFilter = UpscalingFilterSelection.FSR;
                    urpAsset.fsrSharpness = 0.2f;
                    break;
                case UpscalingMode.FsrQuality:
                    urpAsset.renderScale = 0.77f;
                    urpAsset.upscalingFilter = UpscalingFilterSelection.FSR;
                    urpAsset.fsrSharpness = 0.25f;
                    break;
                case UpscalingMode.FsrBalanced:
                    urpAsset.renderScale = 0.67f;
                    urpAsset.upscalingFilter = UpscalingFilterSelection.FSR;
                    urpAsset.fsrSharpness = 0.3f;
                    break;
                case UpscalingMode.FsrPerformance:
                    urpAsset.renderScale = 0.59f;
                    urpAsset.upscalingFilter = UpscalingFilterSelection.FSR;
                    urpAsset.fsrSharpness = 0.38f;
                    break;
                case UpscalingMode.TemporalStp:
                    urpAsset.renderScale = 0.77f;
                    urpAsset.upscalingFilter = UpscalingFilterSelection.STP;
                    urpAsset.fsrSharpness = 0f;
                    urpAsset.fsrOverrideSharpness = false;
                    break;
                default:
                    urpAsset.renderScale = 1f;
                    urpAsset.upscalingFilter = UpscalingFilterSelection.Auto;
                    urpAsset.fsrSharpness = 0f;
                    urpAsset.fsrOverrideSharpness = false;
                    break;
            }
        }

        private static UniversalRenderPipelineAsset GetActiveUrpAsset()
        {
            return GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset
                ?? QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
        }

        private static void AttachTooltip(Text valueText, string tooltip)
        {
            if (valueText == null || string.IsNullOrWhiteSpace(tooltip))
            {
                return;
            }

            Transform row = valueText.transform.parent != null ? valueText.transform.parent.parent : null;
            GameObject target = row != null ? row.gameObject : valueText.gameObject;
            BCMenuTooltipTarget tooltipTarget = target.GetComponent<BCMenuTooltipTarget>();
            if (tooltipTarget == null)
            {
                tooltipTarget = target.AddComponent<BCMenuTooltipTarget>();
            }

            tooltipTarget.SetContent(tooltip);
        }

        private static Text CreateInfoText(Transform parent, string content)
        {
            Text text = CreateText(parent, content, 15, FontStyle.Normal, new Color(0.66f, 0.74f, 0.8f), TextAnchor.UpperLeft);
            LayoutElement layout = text.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.minHeight = 48f;
            }

            return text;
        }

        private static string Translate(string english, string russian, string ukrainian = null, string portuguese = null, string polish = null)
        {
            return BCLocalization.CurrentLanguageCode switch
            {
                "RU" => string.IsNullOrWhiteSpace(russian) ? english : russian,
                "UK" => string.IsNullOrWhiteSpace(ukrainian) ? (string.IsNullOrWhiteSpace(russian) ? english : russian) : ukrainian,
                "PT" => string.IsNullOrWhiteSpace(portuguese) ? english : portuguese,
                "PL" => string.IsNullOrWhiteSpace(polish) ? english : polish,
                _ => english
            };
        }

        private static string GetGraphicsApiPreferenceLabel(GraphicsApiPreference preference)
        {
            return preference switch
            {
                GraphicsApiPreference.DX11 => "DX11",
                GraphicsApiPreference.DX12 => "DX12",
                GraphicsApiPreference.Vulkan => "Vulkan",
                _ => Translate("Auto", "Auto")
            };
        }

        private static string GetGraphicsDeviceTypeLabel(GraphicsDeviceType deviceType)
        {
            return deviceType switch
            {
                GraphicsDeviceType.Direct3D11 => "DX11",
                GraphicsDeviceType.Direct3D12 => "DX12",
                GraphicsDeviceType.Vulkan => "Vulkan",
                _ => deviceType.ToString()
            };
        }

        private static string GetUpscalingLabel(UpscalingMode mode)
        {
            return mode switch
            {
                UpscalingMode.Bilinear => Translate("Bilinear", "Bilinear", "Bilinear", "Bilinear", "Bilinear"),
                UpscalingMode.FsrUltraQuality => "FSR Ultra",
                UpscalingMode.FsrQuality => "FSR Quality",
                UpscalingMode.FsrBalanced => "FSR Balanced",
                UpscalingMode.FsrPerformance => "FSR Performance",
                UpscalingMode.TemporalStp => "STP Temporal",
                _ => Translate("Native", "Нативно", "Нативно", "Nativo", "Natywnie")
            };
        }

        private static string BuildGraphicsApiTooltip()
        {
            return Translate(
                "Choose which graphics backend you prefer for the standalone build. DX11 is usually the safest, DX12 can be faster on newer hardware, and Vulkan is possible only if the Windows build includes it. This setting is saved as a preference and generally needs a restart to take effect.",
                "Выбор предпочитаемого графического backend для standalone-билда. DX11 обычно самый безопасный, DX12 может быть быстрее на новом железе, а Vulkan возможен только если он включен в Windows-билд. Настройка сохраняется как предпочтение и обычно требует рестарта.",
                "Вибір бажаного графічного backend для standalone-білда. DX11 зазвичай найбезпечніший, DX12 може бути швидшим на новому залізі, а Vulkan можливий лише якщо він увімкнений у Windows-білді. Налаштування зберігається як уподобання і зазвичай потребує перезапуску.",
                "Escolhe o backend grafico preferido para o build standalone. DX11 costuma ser o mais seguro, DX12 pode ser mais rapido em hardware recente, e Vulkan so e possivel se o build de Windows o incluir. A definicao fica guardada como preferencia e normalmente exige reinicio.",
                "Wybierz preferowany backend graficzny dla buildu standalone. DX11 jest zwykle najbezpieczniejszy, DX12 bywa szybszy na nowszym sprzęcie, a Vulkan jest możliwy tylko wtedy, gdy build Windows go zawiera. To ustawienie zapisuje preferencję i zazwyczaj wymaga restartu.");
        }

        private static string BuildUpscalingTooltip()
        {
            return Translate(
                "Global upscaling for the whole game. URP in this project currently exposes Bilinear, FSR 1.0 presets, and STP temporal upscaling. Real FSR version switching is not built into this setup.",
                "Глобальный апскейл для всей игры. URP в этом проекте сейчас даёт Bilinear, пресеты FSR 1.0 и temporal STP. Настоящего переключения версий FSR в этой конфигурации нет.",
                "Глобальний апскейл для всієї гри. URP у цьому проекті зараз дає Bilinear, пресети FSR 1.0 і temporal STP. Справжнього перемикання версій FSR у цій конфігурації немає.",
                "Upscaling global para todo o jogo. O URP neste projeto disponibiliza Bilinear, presets de FSR 1.0 e STP temporal. A troca real de versoes do FSR nao faz parte desta configuracao.",
                "Globalny upscaling dla calej gry. URP w tym projekcie udostepnia Bilinear, presety FSR 1.0 oraz temporalne STP. Prawdziwe przelaczanie wersji FSR nie jest czescia tej konfiguracji.");
        }

        private static string BuildShadingTooltip()
        {
            return Translate(
                "Switch between smoother default lighting and a harsher flat-shaded look. Flat Shade uses hard main shadows and global posterized lighting to remove soft gradients.",
                "Переключение между более плавным стандартным светом и жёстким flat-shade видом. Flat Shade использует жёсткие главные тени и глобальное ступенчатое освещение без мягких градиентов.",
                "Перемикання між плавнішим стандартним світлом і жорстким flat-shade виглядом. Flat Shade використовує жорсткі головні тіні й глобальне ступінчасте освітлення без м'яких градієнтів.",
                "Alterna entre a iluminacao padrao mais suave e um aspeto flat shade mais duro. Flat Shade usa sombras principais duras e iluminacao global posterizada para remover gradientes suaves.",
                "Przelaczanie miedzy plynniejszym domyslnym oswietleniem a ostrzejszym wygladem flat shade. Flat Shade korzysta z twardych cieni glownego swiatla i globalnego posterized lighting bez miekkich gradientow.");
        }

        private void ConfirmExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void RebuildSettingsPanelContent()
        {
            if (settingsPanel == null)
            {
                return;
            }

            ClearPanelChildren(settingsPanel);
            BuildSettingsPanel();
        }

        private void BuildSettingsPanel()
        {
            if (settingsPanel == null)
            {
                return;
            }

            CreateHeadingText(settingsPanel, BCLocalization.Get("menu.settings.title"), 30, TextAnchor.MiddleLeft);

            settingsScrollRect = CreateScrollArea(settingsPanel, out settingsScrollContent);
            dynamicLightingToggle = CreateToggle(settingsScrollContent, BCLocalization.Get("menu.settings.dynamic"), _dynamicLightingEnabled, SetDynamicLighting);
            resolutionValueText = CreateSelector(settingsScrollContent, BCLocalization.Get("menu.settings.resolution"), () => CycleResolution(-1), () => CycleResolution(1));
            fullscreenValueText = CreateSelector(settingsScrollContent, BCLocalization.Get("menu.settings.display"), () => CycleFullscreen(-1), () => CycleFullscreen(1));

            settingsAdvancedToggleButton = CreateButton(
                settingsScrollContent,
                string.Empty,
                ToggleAdvancedOptions,
                0f);
            settingsAdvancedToggleText = settingsAdvancedToggleButton.GetComponentInChildren<Text>();

            GameObject advancedObject = new GameObject("AdvancedOptions");
            advancedObject.transform.SetParent(settingsScrollContent, false);
            settingsAdvancedPanel = advancedObject.AddComponent<RectTransform>();
            VerticalLayoutGroup advancedLayout = advancedObject.AddComponent<VerticalLayoutGroup>();
            advancedLayout.childAlignment = TextAnchor.UpperLeft;
            advancedLayout.spacing = 14f;
            advancedLayout.padding = new RectOffset(0, 0, 10, 0);
            ContentSizeFitter advancedFitter = advancedObject.AddComponent<ContentSizeFitter>();
            advancedFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            graphicsApiValueText = CreateSelector(
                settingsAdvancedPanel,
                Translate("Renderer API", "Рендер API", "Рендер API", "API de render", "API renderera"),
                () => CycleGraphicsApi(-1),
                () => CycleGraphicsApi(1));
            AttachTooltip(graphicsApiValueText, BuildGraphicsApiTooltip());
            graphicsApiInfoText = CreateInfoText(settingsAdvancedPanel, string.Empty);

            upscalingValueText = CreateSelector(
                settingsAdvancedPanel,
                Translate("Upscaling / FSR", "Апскейл / FSR", "Апскейл / FSR", "Upscaling / FSR", "Upscaling / FSR"),
                () => CycleUpscaling(-1),
                () => CycleUpscaling(1));
            AttachTooltip(upscalingValueText, BuildUpscalingTooltip());
            upscalingInfoText = CreateInfoText(settingsAdvancedPanel, string.Empty);

            shadingStyleValueText = CreateSelector(
                settingsAdvancedPanel,
                Translate("Light Style", "Стиль света", "Стиль світла", "Estilo de luz", "Styl swiatla"),
                () => CycleShadingStyle(-1),
                () => CycleShadingStyle(1));
            AttachTooltip(shadingStyleValueText, BuildShadingTooltip());
            shadingStyleInfoText = CreateInfoText(settingsAdvancedPanel, string.Empty);

            RectTransform settingsButtons = CreateHorizontalRow(settingsScrollContent, "SettingsButtons", 18f);
            CreateButton(settingsButtons, Translate("Apply", "Принять", "Застосувати", "Aplicar", "Zastosuj"), ApplySettings, 180f);
            CreateButton(settingsButtons, Translate("Reset", "Сброс", "Скинути", "Repor", "Reset"), ResetSettings, 180f);
            settingsStatusText = CreateInfoText(settingsScrollContent, string.Empty);

            SetAdvancedOptionsExpanded(_advancedSettingsExpanded, updateScrollPosition: false);
        }

        private void ToggleAdvancedOptions()
        {
            SetAdvancedOptionsExpanded(!_advancedSettingsExpanded, updateScrollPosition: true);
        }

        private void SetAdvancedOptionsExpanded(bool expanded, bool updateScrollPosition)
        {
            _advancedSettingsExpanded = expanded;

            if (settingsAdvancedPanel != null)
            {
                settingsAdvancedPanel.gameObject.SetActive(expanded);
            }

            if (settingsAdvancedToggleText != null)
            {
                settingsAdvancedToggleText.text = expanded
                    ? Translate("Advanced Options v", "Расширенные настройки v", "Розширені налаштування v", "Opcoes avancadas v", "Opcje zaawansowane v")
                    : Translate("Advanced Options >", "Расширенные настройки >", "Розширені налаштування >", "Opcoes avancadas >", "Opcje zaawansowane >");
            }

            if (updateScrollPosition && settingsScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                settingsScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private static ScrollRect CreateScrollArea(Transform parent, out RectTransform content)
        {
            GameObject rootObject = new GameObject("SettingsScrollView");
            rootObject.transform.SetParent(parent, false);
            RectTransform rootRect = rootObject.AddComponent<RectTransform>();
            LayoutElement rootLayout = rootObject.AddComponent<LayoutElement>();
            rootLayout.flexibleHeight = 1f;
            rootLayout.minHeight = 520f;

            GameObject viewportObject = new GameObject("Viewport");
            viewportObject.transform.SetParent(rootObject.transform, false);
            RectTransform viewportRect = StretchFullScreen(viewportObject);
            Image viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.color = new Color(0.07f, 0.08f, 0.10f, 0.78f);
            viewportImage.raycastTarget = true;
            Mask viewportMask = viewportObject.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;

            GameObject contentObject = new GameObject("Content");
            contentObject.transform.SetParent(viewportObject.transform, false);
            content = contentObject.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 18f;
            layout.padding = new RectOffset(28, 22, 26, 26);
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = rootObject.AddComponent<ScrollRect>();
            scrollRect.viewport = viewportRect;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 28f;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            return scrollRect;
        }

        private string BuildOverviewBody()
        {
            return Translate(
                "A central lift exposes both shelves, deals cards and tickets through the same side issue slot, and keeps the docked gun synchronized with the mechanism.\n\nThe current goal is to finish the atmosphere, stabilize the duel flow, and present each round like a complete scene rather than a temporary mockup.",
                "Центральный лифт открывает обе полки, выдает карты и билеты через один боковой слот выдачи и держит пистолет синхронизированным с механизмом.\n\nТекущая цель — довести атмосферу, стабилизировать дуэльный цикл и подать каждый раунд как цельную готовую сцену.",
                "Центральний ліфт відкриває обидві полиці, видає карти й квитки через один боковий слот видачі та тримає пістолет синхронізованим з механізмом.\n\nПоточна ціль — довести атмосферу, стабілізувати дуельний цикл і подати кожен раунд як цілісну готову сцену.",
                "Um elevador central expõe as duas prateleiras, entrega cartas e bilhetes pelo mesmo slot lateral e mantém a arma sincronizada com o mecanismo.\n\nO objetivo atual é fechar a atmosfera, estabilizar o fluxo do duelo e apresentar cada ronda como uma cena completa em vez de um mockup temporario.",
                "Centralny podnosnik wysuwa obie polki, wydaje karty i bilety przez ten sam boczny slot i utrzymuje bron zsynchronizowana z mechanizmem.\n\nObecny cel to dopracowac atmosfere, ustabilizowac petle pojedynku i pokazac kazda runde jako kompletna gotowa scene.");
        }

        private string BuildCreditsBody()
        {
            return Translate(
                "Breath Casino\nDesign, code, atmosphere, and ongoing development by Andrii Kharlai.\n\nThis version focuses on turning the prototype table into a finished mechanical confrontation with stronger scene presentation, sound staging, and menu flow.",
                "Breath Casino\nДизайн, код, атмосфера и текущее развитие: Andrii Kharlai.\n\nЭта версия сосредоточена на том, чтобы превратить прототип стола в законченную механическую конфронтацию с более сильной подачей сцены, звука и меню.",
                "Breath Casino\nДизайн, код, атмосфера та поточний розвиток: Andrii Kharlai.\n\nЦя версія зосереджена на тому, щоб перетворити прототип столу на завершену механічну конфронтацію з сильнішою подачею сцени, звуку та меню.",
                "Breath Casino\nDesign, codigo, atmosfera e desenvolvimento continuo por Andrii Kharlai.\n\nEsta versao concentra-se em transformar a mesa prototipo numa confrontacao mecanica finalizada, com melhor apresentacao de cena, som e fluxo de menu.",
                "Breath Casino\nProjekt, kod, atmosfera i dalszy rozwoj: Andrii Kharlai.\n\nTa wersja skupia sie na zamianie prototypowego stolu w dopracowana mechaniczna konfrontacje z mocniejsza prezentacja sceny, dzwieku i menu.");
        }

        private static void ClearPanelChildren(Transform panel)
        {
            if (panel == null)
            {
                return;
            }

            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Transform child = panel.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void RegisterSection(RectTransform panel)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(false);
                _sectionObjects.Add(panel.gameObject);
            }
        }

        private RectTransform CreateContentPanel(string name)
        {
            RectTransform panel = CreateRightPanel(name, menuCanvas.transform, new Color(0.035f, 0.045f, 0.06f, 0.94f));
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = 18f;
            layout.padding = new RectOffset(38, 38, 44, 36);
            return panel;
        }

        private RectTransform CreateCenteredDialog(string name, Vector2 size)
        {
            GameObject panelObject = new GameObject(name);
            panelObject.transform.SetParent(menuCanvas.transform, false);
            RectTransform rect = panelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            Image image = panelObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.06f, 0.08f, 0.98f);

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 18f;
            layout.padding = new RectOffset(36, 36, 36, 36);
            return rect;
        }

        private static RectTransform CreateLeftColumnPanel(string name, Transform parent, float width, Color color)
        {
            GameObject panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);

            Image image = panelObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform CreateRightPanel(string name, Transform parent, Color color)
        {
            GameObject panelObject = new GameObject(name);
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.08f);
            rect.anchorMax = new Vector2(0f, 0.92f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(520f, 0f);
            rect.sizeDelta = new Vector2(820f, 0f);

            Image image = panelObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static RectTransform StretchFullScreen(GameObject target)
        {
            RectTransform rect = target.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateHorizontalRow(Transform parent, string name, float spacing)
        {
            GameObject rowObject = new GameObject(name);
            rowObject.transform.SetParent(parent, false);
            RectTransform rect = rowObject.AddComponent<RectTransform>();
            HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            rowObject.AddComponent<LayoutElement>().preferredHeight = 68f;
            return rect;
        }

        private RectTransform CreateCornerButtonRoot(string name)
        {
            GameObject rootObject = new GameObject(name);
            rootObject.transform.SetParent(menuCanvas.transform, false);
            RectTransform rect = rootObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-32f, -24f);
            rect.sizeDelta = new Vector2(112f, 48f);
            return rect;
        }

        private RectTransform CreateLanguagePanel(Transform parent)
        {
            GameObject panelObject = new GameObject("LanguagePanel");
            panelObject.transform.SetParent(menuCanvas.transform, false);
            RectTransform rect = panelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-32f, -78f);
            rect.sizeDelta = new Vector2(180f, 64f + (SupportedLanguages.Length * 52f));

            Image image = panelObject.AddComponent<Image>();
            image.color = new Color(0.05f, 0.06f, 0.08f, 0.98f);

            VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < SupportedLanguages.Length; i++)
            {
                int languageIndex = i;
                CreateButton(panelObject.transform, SupportedLanguages[i].Label, () => SelectLanguage(languageIndex), 0f);
            }

            panelObject.SetActive(false);
            return rect;
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent, false);
            LayoutElement layout = spacer.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
        }

        private static Text CreateHeadingText(Transform parent, string content, int fontSize, TextAnchor alignment)
        {
            Text text = CreateText(parent, content, fontSize, FontStyle.Bold, null, alignment);
            text.font = BCRuntimeFontProvider.GetTitleFont(fontSize);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            return text;
        }

        private static Text CreateText(Transform parent, string content, int fontSize, FontStyle fontStyle, Color? colorOverride = null, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.font = BCRuntimeFontProvider.Get(18);
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = colorOverride ?? new Color(0.95f, 0.95f, 0.93f);
            text.raycastTarget = false;
            text.text = content;
            LayoutElement layout = textObject.AddComponent<LayoutElement>();
            layout.minHeight = fontSize + 18f;
            return text;
        }

        private static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, float preferredWidth = -1f)
        {
            GameObject buttonObject = new GameObject(label);
            buttonObject.transform.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            image.color = new Color(0.16f, 0.19f, 0.23f, 0.98f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => BCAudioManager.Instance?.PlayUIClick());
            button.onClick.AddListener(onClick);
            buttonObject.AddComponent<BCMenuSoundHooks>();

            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(preferredWidth > 0f ? preferredWidth : 0f, 60f);
            LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 60f;
            if (preferredWidth > 0f)
            {
                layout.preferredWidth = preferredWidth;
            }
            else
            {
                layout.minWidth = 160f;
            }

            Text text = CreateText(buttonObject.transform, label, 22, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            return button;
        }

        private static Toggle CreateToggle(Transform parent, string label, bool initialValue, UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            GameObject rootObject = new GameObject($"{label}Toggle");
            rootObject.transform.SetParent(parent, false);
            rootObject.AddComponent<BCMenuSoundHooks>();
            VerticalLayoutGroup rootLayout = rootObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 8f;
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootObject.AddComponent<LayoutElement>().preferredHeight = 82f;

            CreateText(rootObject.transform, label, 18, FontStyle.Normal, new Color(0.82f, 0.87f, 0.91f), TextAnchor.MiddleLeft);

            GameObject toggleObject = new GameObject("ToggleRow");
            toggleObject.transform.SetParent(rootObject.transform, false);
            HorizontalLayoutGroup layout = toggleObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;

            GameObject backgroundObject = new GameObject("Background");
            backgroundObject.transform.SetParent(toggleObject.transform, false);
            Image background = backgroundObject.AddComponent<Image>();
            background.color = new Color(0.20f, 0.20f, 0.22f, 1f);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.sizeDelta = new Vector2(30f, 30f);

            GameObject checkmarkObject = new GameObject("Checkmark");
            checkmarkObject.transform.SetParent(backgroundObject.transform, false);
            Image checkmark = checkmarkObject.AddComponent<Image>();
            checkmark.color = new Color(0.87f, 0.93f, 0.96f, 1f);
            RectTransform checkmarkRect = checkmark.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
            checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
            checkmarkRect.offsetMin = Vector2.zero;
            checkmarkRect.offsetMax = Vector2.zero;

            Toggle toggle = toggleObject.AddComponent<Toggle>();
            toggle.graphic = checkmark;
            toggle.targetGraphic = background;
            toggle.isOn = initialValue;
            toggle.onValueChanged.AddListener(_ => BCAudioManager.Instance?.PlayUIClick());
            toggle.onValueChanged.AddListener(onValueChanged);

            Text stateLabel = CreateText(toggleObject.transform, initialValue ? BCLocalization.Get("toggle.enabled") : BCLocalization.Get("toggle.disabled"), 18, FontStyle.Normal, null, TextAnchor.MiddleLeft);
            stateLabel.alignment = TextAnchor.MiddleLeft;
            toggle.onValueChanged.AddListener(value =>
            {
                if (stateLabel != null)
                {
                    stateLabel.text = value ? BCLocalization.Get("toggle.enabled") : BCLocalization.Get("toggle.disabled");
                }
            });
            return toggle;
        }

        private static Text CreateSelector(Transform parent, string label, UnityEngine.Events.UnityAction onPrevious, UnityEngine.Events.UnityAction onNext)
        {
            GameObject rootObject = new GameObject($"{label}Selector");
            rootObject.transform.SetParent(parent, false);
            rootObject.AddComponent<BCMenuSoundHooks>();
            VerticalLayoutGroup rootLayout = rootObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 8f;
            rootLayout.childAlignment = TextAnchor.UpperLeft;
            rootObject.AddComponent<LayoutElement>().preferredHeight = 106f;

            CreateText(rootObject.transform, label, 18, FontStyle.Normal, new Color(0.82f, 0.87f, 0.91f), TextAnchor.MiddleLeft);

            GameObject rowObject = new GameObject("SelectorRow");
            rowObject.transform.SetParent(rootObject.transform, false);
            Image rowImage = rowObject.AddComponent<Image>();
            rowImage.color = new Color(0.16f, 0.20f, 0.24f, 0.98f);
            HorizontalLayoutGroup rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.padding = new RectOffset(10, 10, 8, 8);
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            rowObject.AddComponent<LayoutElement>().preferredHeight = 56f;

            CreateButton(rowObject.transform, "<", onPrevious, 56f);

            GameObject valueObject = new GameObject("Value");
            valueObject.transform.SetParent(rowObject.transform, false);
            valueObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Text valueText = CreateText(valueObject.transform, "-", 20, FontStyle.Bold, null, TextAnchor.MiddleCenter);
            RectTransform valueRect = valueText.GetComponent<RectTransform>();
            valueRect.anchorMin = Vector2.zero;
            valueRect.anchorMax = Vector2.one;
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            CreateButton(rowObject.transform, ">", onNext, 56f);
            return valueText;
        }
        private void ClearCanvasChildren()
        {
            if (menuCanvas == null)
            {
                return;
            }

            List<GameObject> toDestroy = new();
            for (int i = 0; i < menuCanvas.transform.childCount; i++)
            {
                Transform child = menuCanvas.transform.GetChild(i);
                if (child != null)
                {
                    toDestroy.Add(child.gameObject);
                }
            }

            for (int i = 0; i < toDestroy.Count; i++)
            {
                if (Application.isPlaying)
                {
                    Destroy(toDestroy[i]);
                }
                else
                {
                    DestroyImmediate(toDestroy[i]);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static void EnsureTooltipDisplay()
        {
            if (BCTooltipDisplay.Instance != null)
            {
                return;
            }

            GameObject tooltipObject = new GameObject("TooltipDisplay");
            tooltipObject.AddComponent<BCTooltipDisplay>();
        }
    }
}

namespace BreathCasino.Gameplay
{
    public class BCMenuTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea(2, 6)]
        [SerializeField] private string tooltipContent;

        public void SetContent(string content)
        {
            tooltipContent = content;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrWhiteSpace(tooltipContent))
            {
                BCTooltipDisplay.Show(tooltipContent);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            BCTooltipDisplay.Hide();
        }
    }
}
