using System.Collections;
using System.Collections.Generic;
using BreathCasino.Gameplay;
using BreathCasino.Rendering;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace BreathCasino.Core
{
    public class SceneBootstrap : MonoBehaviour
    {
        private bool _dynamicLightingEnabled = true;
        private bool _flatShadeLightingEnabled = true;
        private readonly Dictionary<Light, float> _baseLightIntensities = new();
        private readonly List<Light> _sceneLights = new();
        private float _roomBlackoutWeight;

        [Header("Scene Roots")]
        public Transform managersRoot;
        public Transform tableRoot;
        public Transform playerRoot;
        public Transform enemyRoot;
        public Transform gunRoot;
        public Transform uiRoot;

        [Header("Scene References")]
        public Camera mainCamera;
        public Light directionalLight;
        public Canvas hudCanvas;
        public Text statusText;
        public Text debugActionsText;

        [Header("Table")]
        public Transform playerMainSlot;
        public Transform playerSpecialSlot;
        public Transform enemyMainSlot;
        public Transform enemySpecialSlot;
        public Transform gunSpot;
        public Transform bulletSpot;
        public Transform ticketStack;
        public Transform tableCenterPlayerSpawn;
        public Transform tableCenterEnemySpawn;
        public Transform ticketAcceptSpot;
        public Transform playerLeverRoot;
        public Transform enemyLeverRoot;

        [Header("Player")]
        public Transform playerWeaponHolder;
        public Transform playerHandHolder;
        public Transform playerSpecialHolder;

        [Header("Enemy")]
        public Transform enemyWeaponHolder;
        public Transform enemyHandHolder;
        public Transform enemySpecialHolder;

        [Header("Managers")]
        public GameManager gameManager;
        public TicketManager ticketManager;
        public BCCardManager cardManager;
        public SceneValidator validator;
        public BCHandAnimator handAnimator;

        [Header("Intro")]
        public Transform introCameraStart;
        public Transform introCameraSeat;

        [Header("Atmosphere")]
        [SerializeField] private bool applyWarmAtmosphereOnStart = true;
        [SerializeField] private Color warmDirectionalColor = new(1f, 0.82f, 0.56f, 1f);
        [SerializeField] private Color warmFogColor = new(0.78f, 0.63f, 0.31f, 1f);
        [SerializeField] private Color ambientSkyColor = new(0.63f, 0.52f, 0.26f, 1f);
        [SerializeField] private Color ambientEquatorColor = new(0.55f, 0.44f, 0.21f, 1f);
        [SerializeField] private Color ambientGroundColor = new(0.33f, 0.25f, 0.12f, 1f);
        [SerializeField] private float warmFogDensity = 0.021f;
        [SerializeField, Range(0.01f, 0.4f)] private float blackoutLightMultiplier = 0.04f;
        [SerializeField, Range(0f, 0.25f)] private float blackoutAmbientMultiplier = 0f;
        [SerializeField, Range(0f, 0.35f)] private float blackoutFogDensityMultiplier = 0.02f;
        [SerializeField, Range(0.2f, 1f)] private float slotScaleMultiplier = 0.5f;
        [SerializeField] private bool enableDustParticles = true;
        [SerializeField] private Vector3 dustVolumeSize = new(10f, 3.6f, 10f);
        [SerializeField] private Vector3 dustLocalOffset = new(0f, 1.1f, 0f);
        [SerializeField] private int dustMaxParticles = 180;
        [SerializeField] private Color dustColorA = new(0.95f, 0.82f, 0.54f, 0.09f);
        [SerializeField] private Color dustColorB = new(0.87f, 0.68f, 0.34f, 0.03f);

        private BCDealMechanism _dealMechanism;
        private BCTicketStack _playerDealTicketStack;
        private BCTicketStack _enemyDealTicketStack;
        private BCLeverMechanism _playerLever;
        private BCLeverMechanism _enemyLever;
        private BCEnemyDialogueController _enemyDialogueController;
        private BCStartMenuController _startMenuController;
        private BCIntroSequenceController _introSequenceController;
        private ParticleSystem _dustParticles;
        private bool _pendingIntroConsumption;
        private Color _baseFogColor;
        private float _baseFogDensity;
        private Color _baseAmbientSkyColor;
        private Color _baseAmbientEquatorColor;
        private Color _baseAmbientGroundColor;
        private float _baseAmbientIntensity;

        public BCDealMechanism DealMechanism => _dealMechanism;
        public BCLeverMechanism PlayerLever => _playerLever;
        public BCLeverMechanism EnemyLever => _enemyLever;
        public BCEnemyDialogueController EnemyDialogueController => _enemyDialogueController;
        public Transform IntroCameraStart => introCameraStart;
        public Transform IntroCameraSeat => introCameraSeat;
        public bool IsIntroLeverHoldActive => _introSequenceController != null && _introSequenceController.IsAwaitingLeverHold;

        private void Start()
        {
            EnsureDirectionalLight();
            CacheSceneLightState();
            NormalizeCombatSlotScales();
            if (applyWarmAtmosphereOnStart)
            {
                ApplyWarmAtmosphere();
            }

            EnsureDustParticles();

            if (statusText == null)
            {
                statusText = GetComponentInChildren<Text>(true);
                if (statusText == null)
                {
                    Text[] allTexts = FindObjectsByType<Text>(FindObjectsSortMode.None);
                    if (allTexts.Length > 0)
                    {
                        statusText = allTexts[0];
                    }
                }

                if (statusText != null)
                {
                    Debug.Log("[Bootstrap] statusText auto-found: " + statusText.gameObject.name);
                }
            }

            EnsureEnemyFacesTable();
            EnsureEventSystem();
            EnsureTooltipSystem();
            EnsureDebugActionsOverlay();
            EnsureSlotInteractables();
            EnsureTicketStack();
            EnsureDealMechanism();
            EnsureLevers();
            EnsureEnemyDialogueSystem();
            EnsureStartMenu();
            EnsureIntroSequence();

            if (validator != null)
            {
                validator.ValidateAndReport(this);
            }

            if (handAnimator != null)
            {
                handAnimator.SetHolders(playerHandHolder, playerSpecialHolder, enemyHandHolder, enemySpecialHolder);
            }

            if (gameManager != null)
            {
                gameManager.Initialize(this, ticketManager, cardManager, validator, handAnimator);
            }

            if (ticketManager != null)
            {
                ticketManager.Initialize(gameManager, _playerDealTicketStack, _enemyDealTicketStack, playerHandHolder, cardManager);
            }

            if (_startMenuController != null)
            {
                _startMenuController.Initialize(this);
                _introSequenceController?.Initialize(this);
            }
            else if (gameManager != null)
            {
                gameManager.BeginGame();
            }
        }

        private void EnsureEnemyFacesTable()
        {
            if (enemyRoot == null)
            {
                return;
            }

            if (handAnimator != null && enemyHandHolder != null)
            {
                Vector3 currentLow = enemyHandHolder.localPosition;
                Vector3 currentHigh = currentLow + new Vector3(0f, -0.03f, 0.22f);
                handAnimator.SetEnemyHandPositionsForFacingTable(currentLow, currentHigh);
            }

            if (enemyRoot.localEulerAngles.y < 170f || enemyRoot.localEulerAngles.y > 190f)
            {
                enemyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.Log("[Bootstrap] Enemy orientation fixed — cards in front.");
            }
        }

        private void EnsureTicketStack()
        {
            if (ticketStack == null)
            {
                return;
            }

            BCTicketStack stack = ticketStack.GetComponent<BCTicketStack>();
            if (stack == null)
            {
                ticketStack.gameObject.AddComponent<BCTicketStack>();
                Debug.Log("[Bootstrap] Added BCTicketStack component");
            }
        }

        private void EnsureDealMechanism()
        {
            if (tableRoot == null && managersRoot == null)
            {
                return;
            }

            _dealMechanism = FindBestDealMechanism();
            if (_dealMechanism == null)
            {
                Transform host = tableRoot != null ? tableRoot : managersRoot;
                _dealMechanism = host.gameObject.AddComponent<BCDealMechanism>();
            }

            Debug.Log($"[Bootstrap] Using deal mechanism: {_dealMechanism.gameObject.name} (score {_dealMechanism.ConfigurationScore})");

            _dealMechanism.EnsureScaffold(tableRoot, tableCenterPlayerSpawn, tableCenterEnemySpawn, gunSpot, ticketAcceptSpot);
            _dealMechanism.Initialize(tableCenterPlayerSpawn, tableCenterEnemySpawn, gunSpot);
            _dealMechanism.Snap(_dealMechanism.StartRaisedOnPlay);

            string mechanismIssues = _dealMechanism.DescribeHierarchyIssues();
            if (!string.IsNullOrWhiteSpace(mechanismIssues))
            {
                Debug.LogWarning("[Bootstrap] Deal mechanism hierarchy issues: " + mechanismIssues);
            }

            if (ticketAcceptSpot == null)
            {
                ticketAcceptSpot = _dealMechanism.PlayerTicketAcceptSocket != null
                    ? _dealMechanism.PlayerTicketAcceptSocket
                    : _dealMechanism.TicketAcceptSocket;
            }

            BCCardSupplyStack playerSupplyStack = null;
            BCCardSupplyStack enemySupplyStack = null;
            _playerDealTicketStack = null;
            _enemyDealTicketStack = null;

            Transform playerSupplyAnchor = _dealMechanism.PlayerCardSocket != null ? _dealMechanism.PlayerCardSocket : tableCenterPlayerSpawn;
            Transform enemySupplyAnchor = _dealMechanism.EnemyCardSocket != null ? _dealMechanism.EnemyCardSocket : tableCenterEnemySpawn;

            if (playerSupplyAnchor != null)
            {
                playerSupplyStack = playerSupplyAnchor.GetComponent<BCCardSupplyStack>();
                if (playerSupplyStack == null)
                {
                    playerSupplyStack = playerSupplyAnchor.gameObject.AddComponent<BCCardSupplyStack>();
                }

                playerSupplyStack.Configure(Side.Player);
            }

            if (enemySupplyAnchor != null)
            {
                enemySupplyStack = enemySupplyAnchor.GetComponent<BCCardSupplyStack>();
                if (enemySupplyStack == null)
                {
                    enemySupplyStack = enemySupplyAnchor.gameObject.AddComponent<BCCardSupplyStack>();
                }

                enemySupplyStack.Configure(Side.Enemy);
            }

            Transform playerTicketAnchor = _dealMechanism.PlayerTicketSocket != null
                ? _dealMechanism.PlayerTicketSocket
                : playerSupplyAnchor;

            Transform enemyTicketAnchor = _dealMechanism.EnemyTicketSocket != null
                ? _dealMechanism.EnemyTicketSocket
                : enemySupplyAnchor;

            if (playerTicketAnchor != null)
            {
                _playerDealTicketStack = playerTicketAnchor.GetComponent<BCTicketStack>();
                if (_playerDealTicketStack == null)
                {
                    _playerDealTicketStack = playerTicketAnchor.gameObject.AddComponent<BCTicketStack>();
                }

                if (playerTicketAnchor.GetComponent<BCTicketStackInteractable>() == null)
                {
                    playerTicketAnchor.gameObject.AddComponent<BCTicketStackInteractable>();
                }
            }

            if (enemyTicketAnchor != null)
            {
                _enemyDealTicketStack = enemyTicketAnchor.GetComponent<BCTicketStack>();
                if (_enemyDealTicketStack == null)
                {
                    _enemyDealTicketStack = enemyTicketAnchor.gameObject.AddComponent<BCTicketStack>();
                }
            }

            cardManager?.ConfigureDealStacks(playerSupplyStack, enemySupplyStack);
        }

        private void EnsureLevers()
        {
            if (tableRoot == null)
            {
                return;
            }

            playerLeverRoot = EnsureLeverRoot(playerLeverRoot, "PlayerLeverRoot", new Vector3(0.58f, 0.18f, -0.34f));
            enemyLeverRoot = EnsureLeverRoot(enemyLeverRoot, "EnemyLeverRoot", new Vector3(-0.58f, 0.18f, 0.34f));

            _playerLever = EnsureLeverComponents(playerLeverRoot, Side.Player, true);
            _enemyLever = EnsureLeverComponents(enemyLeverRoot, Side.Enemy, false);
            _playerLever?.AutoConfigurePullAxisTowards(playerRoot != null ? playerRoot : mainCamera != null ? mainCamera.transform : tableRoot);
            _enemyLever?.AutoConfigurePullAxisTowards(enemyRoot != null ? enemyRoot : tableRoot);
        }

        private void EnsureEnemyDialogueSystem()
        {
            if (enemyRoot == null)
            {
                return;
            }

            _enemyDialogueController = enemyRoot.GetComponentInChildren<BCEnemyDialogueController>(true);
            if (_enemyDialogueController == null)
            {
                GameObject dialogueObject = new GameObject("EnemyDialogueController");
                dialogueObject.transform.SetParent(enemyRoot, false);
                _enemyDialogueController = dialogueObject.AddComponent<BCEnemyDialogueController>();
            }

            _enemyDialogueController.Initialize(enemyRoot, mainCamera);
        }

        private void EnsureStartMenu()
        {
            _startMenuController = FindFirstObjectByType<BCStartMenuController>(FindObjectsInactive.Include);
            if (_startMenuController == null)
            {
                GameObject menuObject = new GameObject("StartMenuController");
                menuObject.transform.SetParent(null, false);
                menuObject.transform.localPosition = Vector3.zero;
                menuObject.transform.localRotation = Quaternion.identity;
                menuObject.transform.localScale = Vector3.one;
                _startMenuController = menuObject.AddComponent<BCStartMenuController>();
            }
        }

        private void EnsureIntroSequence()
        {
            EnsureIntroAnchors();

            _introSequenceController = FindFirstObjectByType<BCIntroSequenceController>(FindObjectsInactive.Include);
            if (_introSequenceController == null)
            {
                GameObject introObject = new GameObject("IntroSequenceController");
                introObject.transform.SetParent(null, false);
                introObject.transform.localPosition = Vector3.zero;
                introObject.transform.localRotation = Quaternion.identity;
                introObject.transform.localScale = Vector3.one;
                _introSequenceController = introObject.AddComponent<BCIntroSequenceController>();
            }
        }

        private void EnsureIntroAnchors()
        {
            if (playerRoot == null || mainCamera == null)
            {
                return;
            }

            introCameraSeat = EnsureIntroAnchor(
                introCameraSeat,
                "IntroCameraSeat",
                playerRoot,
                playerRoot.InverseTransformPoint(mainCamera.transform.position),
                Quaternion.Inverse(playerRoot.rotation) * mainCamera.transform.rotation);

            if (introCameraStart == null)
            {
                Vector3 sidePosition = new Vector3(-0.68f, 0.28f, -0.16f);
                Quaternion sideRotation = ComputeIntroLookRotation(playerRoot, sidePosition);
                introCameraStart = EnsureIntroAnchor(
                    null,
                    "IntroCameraStart",
                    playerRoot,
                    sidePosition,
                    sideRotation);
            }
        }

        private void EnsureDirectionalLight()
        {
            if (directionalLight != null)
            {
                return;
            }

            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light != null && light.type == LightType.Directional)
                {
                    directionalLight = light;
                    Debug.Log("[Bootstrap] Directional Light auto-found: " + light.gameObject.name);
                    return;
                }
            }
        }

        private void EnsureTooltipSystem()
        {
            BCTooltipDisplay existing = FindFirstObjectByType<BCTooltipDisplay>();
            if (existing == null)
            {
                GameObject tooltipObj = new GameObject("TooltipSystem");
                tooltipObj.transform.SetParent(uiRoot != null ? uiRoot : transform, false);
                tooltipObj.AddComponent<BCTooltipDisplay>();
                Debug.Log("[Bootstrap] Created TooltipSystem");
            }
        }

        private void EnsureDebugActionsOverlay()
        {
            if (debugActionsText != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("DebugActionsCanvas");
            canvasObject.transform.SetParent(null, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1400;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject panelObject = new GameObject("DebugActionsPanel");
            panelObject.transform.SetParent(canvas.transform, false);
            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(-24f, -24f);
            panelRect.sizeDelta = new Vector2(460f, 420f);

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(0.03f, 0.04f, 0.05f, 0.82f);

            GameObject textObject = new GameObject("DebugActionsText");
            textObject.transform.SetParent(panelObject.transform, false);
            debugActionsText = textObject.AddComponent<Text>();
            debugActionsText.font = BCRuntimeFontProvider.Get(18);
            debugActionsText.fontSize = 18;
            debugActionsText.alignment = TextAnchor.UpperLeft;
            debugActionsText.color = new Color(0.92f, 0.94f, 0.97f, 1f);
            debugActionsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            debugActionsText.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform textRect = debugActionsText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(18f, 18f);
            textRect.offsetMax = new Vector2(-18f, -18f);
        }

        private void EnsureSlotInteractables()
        {
            EnsureSlot(playerMainSlot, true);
            EnsureSlot(playerSpecialSlot, false);
        }

        private static void EnsureSlot(Transform slot, bool isMain)
        {
            if (slot == null)
            {
                return;
            }

            BCSlotInteractable interactable = slot.GetComponent<BCSlotInteractable>();
            if (interactable == null)
            {
                interactable = slot.gameObject.AddComponent<BCSlotInteractable>();
                interactable.SetMainSlot(isMain);
            }

            if (slot.GetComponent<Collider>() == null)
            {
                BoxCollider collider = slot.gameObject.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                collider.size = new Vector3(0.3f, 0.02f, 0.4f);
            }
        }

        public void SetStatus(string text)
        {
            if (statusText != null)
            {
                statusText.text = text;
            }
        }

        public void SetDebugActions(string text)
        {
            if (debugActionsText != null)
            {
                debugActionsText.text = text;
            }
        }

        public void BeginGameFromMenu()
        {
            SetGameplayPresentationVisible(false);

            if (_introSequenceController != null)
            {
                _pendingIntroConsumption = true;
                _introSequenceController.PlayIntro(StartGameplayAfterIntro);
                return;
            }

            _pendingIntroConsumption = false;
            StartGameplayAfterIntro();
        }

        private void StartGameplayAfterIntro()
        {
            SetCameraMenuLock(false);
            SetGameplayPresentationVisible(true);
            gameManager?.SetIntroPresentationConsumed(_pendingIntroConsumption);
            _pendingIntroConsumption = false;
            gameManager?.BeginGame();
        }

        public void CompleteIntroLeverHold()
        {
            _introSequenceController?.CompleteLeverHold();
        }

        public void SetCameraMenuLock(bool isLocked)
        {
            if (mainCamera == null)
            {
                return;
            }

            BCCameraController controller = mainCamera.GetComponent<BCCameraController>();
            controller?.SetInputLocked(isLocked);
        }

        public void SetGameplayPresentationVisible(bool visible)
        {
            if (hudCanvas != null)
            {
                hudCanvas.enabled = visible;
            }

            if (statusText != null)
            {
                statusText.enabled = visible;
            }

            if (debugActionsText != null)
            {
                debugActionsText.enabled = visible;
            }
        }

        public void SetDynamicLightingEnabled(bool enabled)
        {
            _dynamicLightingEnabled = enabled;

            CacheSceneLightState();
            for (int i = 0; i < _sceneLights.Count; i++)
            {
                Light light = _sceneLights[i];
                if (light == null)
                {
                    continue;
                }

                if (light == directionalLight)
                {
                    light.shadows = enabled
                        ? (_flatShadeLightingEnabled ? LightShadows.Hard : LightShadows.Soft)
                        : LightShadows.None;
                }
                else
                {
                    light.enabled = enabled;
                    light.shadows = LightShadows.None;
                }
            }

            ApplyBlackoutToLights();
        }

        public void SetFlatShadeLightingEnabled(bool enabled)
        {
            _flatShadeLightingEnabled = enabled;
            BCCameraGrainFeature.SetFlatShadeProfile(enabled);

            if (directionalLight != null)
            {
                directionalLight.shadows = _dynamicLightingEnabled
                    ? (enabled ? LightShadows.Hard : LightShadows.Soft)
                    : LightShadows.None;
            }

            ApplyWarmAtmosphere();
        }

        public IEnumerator AnimateRoomBlackout(bool darkened, float duration)
        {
            CacheSceneLightState();

            float target = darkened ? 1f : 0f;
            float start = _roomBlackoutWeight;
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / safeDuration);
                _roomBlackoutWeight = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
                ApplyBlackoutToLights();
                yield return null;
            }

            _roomBlackoutWeight = target;
            ApplyBlackoutToLights();
        }

        private void CacheSceneLightState()
        {
            _sceneLights.Clear();
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null)
                {
                    continue;
                }

                _sceneLights.Add(light);
                if (!_baseLightIntensities.ContainsKey(light))
                {
                    _baseLightIntensities.Add(light, light.intensity);
                }
            }
        }

        private void NormalizeCombatSlotScales()
        {
            Transform[] slots = { playerMainSlot, playerSpecialSlot, enemyMainSlot, enemySpecialSlot };
            Vector3 maxScale = Vector3.zero;

            for (int i = 0; i < slots.Length; i++)
            {
                Transform slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                Vector3 scale = slot.localScale;
                maxScale.x = Mathf.Max(maxScale.x, Mathf.Abs(scale.x));
                maxScale.y = Mathf.Max(maxScale.y, Mathf.Abs(scale.y));
                maxScale.z = Mathf.Max(maxScale.z, Mathf.Abs(scale.z));
            }

            if (maxScale == Vector3.zero)
            {
                return;
            }

            Vector3 targetScale = new Vector3(
                maxScale.x * slotScaleMultiplier,
                Mathf.Max(0.02f, maxScale.y),
                maxScale.z * slotScaleMultiplier);

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    slots[i].localScale = targetScale;
                }
            }
        }

        private void ApplyWarmAtmosphere()
        {
            float fogDensity = _flatShadeLightingEnabled ? warmFogDensity * 0.82f : warmFogDensity;
            float directionalBoost = _flatShadeLightingEnabled ? 0.18f : 0f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = warmFogColor;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSkyColor;
            RenderSettings.ambientEquatorColor = ambientEquatorColor;
            RenderSettings.ambientGroundColor = ambientGroundColor;
            RenderSettings.ambientIntensity = 1f;

            _baseFogDensity = fogDensity;
            _baseFogColor = warmFogColor;
            _baseAmbientSkyColor = ambientSkyColor;
            _baseAmbientEquatorColor = ambientEquatorColor;
            _baseAmbientGroundColor = ambientGroundColor;
            _baseAmbientIntensity = RenderSettings.ambientIntensity;

            if (directionalLight != null)
            {
                directionalLight.color = warmDirectionalColor;
                directionalLight.intensity = Mathf.Max(_baseLightIntensities.TryGetValue(directionalLight, out float baseDirectionalIntensity) ? baseDirectionalIntensity : directionalLight.intensity, 1f) + directionalBoost;
            }

            CacheSceneLightState();
            for (int i = 0; i < _sceneLights.Count; i++)
            {
                Light light = _sceneLights[i];
                if (light == null)
                {
                    continue;
                }

                if (light.type != LightType.Directional)
                {
                    light.color = Color.Lerp(light.color, new Color(1f, 0.78f, 0.42f, 1f), 0.65f);
                    _baseLightIntensities[light] = light.intensity;
                }
            }

            ApplyBlackoutToLights();
        }

        private void EnsureDustParticles()
        {
            if (!enableDustParticles)
            {
                if (_dustParticles != null)
                {
                    _dustParticles.gameObject.SetActive(false);
                }

                return;
            }

            Transform parent = tableRoot != null ? tableRoot : transform;
            Transform existing = parent.Find("DustParticles");
            if (existing == null)
            {
                GameObject dustObject = new GameObject("DustParticles");
                existing = dustObject.transform;
                existing.SetParent(parent, false);
            }

            existing.localPosition = dustLocalOffset;
            existing.localRotation = Quaternion.identity;

            _dustParticles = existing.GetComponent<ParticleSystem>();
            if (_dustParticles == null)
            {
                _dustParticles = existing.gameObject.AddComponent<ParticleSystem>();
            }

            ParticleSystemRenderer renderer = existing.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.minParticleSize = 0.002f;
                renderer.maxParticleSize = 0.018f;
            }

            var main = _dustParticles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = Mathf.Max(32, dustMaxParticles);
            main.startLifetime = new ParticleSystem.MinMaxCurve(7.5f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.038f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f);
            main.startColor = new ParticleSystem.MinMaxGradient(dustColorA, dustColorB);

            var emission = _dustParticles.emission;
            emission.enabled = true;
            emission.rateOverTime = 11f;

            var shape = _dustParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = dustVolumeSize;

            var noise = _dustParticles.noise;
            noise.enabled = true;
            noise.strength = 0.08f;
            noise.frequency = 0.24f;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;

            var velocityOverLifetime = _dustParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(-0.025f, 0.025f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.005f, 0.02f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

            var colorOverLifetime = _dustParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.96f, 0.84f, 0.58f), 0f),
                    new GradientColorKey(new Color(0.89f, 0.70f, 0.36f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.18f, 0.18f),
                    new GradientAlphaKey(0.12f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

            if (!_dustParticles.isPlaying)
            {
                _dustParticles.Play(true);
            }
        }

        private void ApplyBlackoutToLights()
        {
            CacheSceneLightState();
            float multiplier = Mathf.Lerp(1f, blackoutLightMultiplier, _roomBlackoutWeight);
            float ambientMultiplier = Mathf.Lerp(1f, blackoutAmbientMultiplier, _roomBlackoutWeight);
            float fogDensity = Mathf.Lerp(_baseFogDensity, _baseFogDensity * blackoutFogDensityMultiplier, _roomBlackoutWeight);
            Color fogColor = Color.Lerp(_baseFogColor, Color.black, _roomBlackoutWeight);

            for (int i = 0; i < _sceneLights.Count; i++)
            {
                Light light = _sceneLights[i];
                if (light == null || !_baseLightIntensities.TryGetValue(light, out float baseIntensity))
                {
                    continue;
                }

                if (!_dynamicLightingEnabled && light != directionalLight)
                {
                    continue;
                }

                light.intensity = baseIntensity * multiplier;
            }

            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientSkyColor = _baseAmbientSkyColor * ambientMultiplier;
            RenderSettings.ambientEquatorColor = _baseAmbientEquatorColor * ambientMultiplier;
            RenderSettings.ambientGroundColor = _baseAmbientGroundColor * ambientMultiplier;
            RenderSettings.ambientIntensity = Mathf.Lerp(_baseAmbientIntensity, 0f, _roomBlackoutWeight);
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

        private Transform EnsureLeverRoot(Transform currentRoot, string name, Vector3 localOffset)
        {
            if (currentRoot != null)
            {
                return currentRoot;
            }

            GameObject leverObject = new GameObject(name);
            Transform leverTransform = leverObject.transform;
            leverTransform.SetParent(tableRoot, false);
            leverTransform.localPosition = localOffset;
            leverTransform.localRotation = Quaternion.identity;
            return leverTransform;
        }

        private static BCLeverMechanism EnsureLeverComponents(Transform root, Side side, bool addInteractable)
        {
            if (root == null)
            {
                return null;
            }

            BCLeverMechanism mechanism = root.GetComponent<BCLeverMechanism>();
            if (mechanism == null)
            {
                mechanism = root.gameObject.AddComponent<BCLeverMechanism>();
            }

            if (root.GetComponent<Collider>() == null)
            {
                BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.16f, 0.22f, 0.16f);
                collider.center = new Vector3(0f, 0.08f, 0f);
            }

            BCLeverInteractable interactable = root.GetComponent<BCLeverInteractable>();
            if (addInteractable)
            {
                if (interactable == null)
                {
                    interactable = root.gameObject.AddComponent<BCLeverInteractable>();
                }

                interactable.Configure(side, mechanism);
            }
            else if (interactable != null)
            {
                Object.Destroy(interactable);
            }

            return mechanism;
        }

        private BCDealMechanism FindBestDealMechanism()
        {
            BCDealMechanism[] allMechanisms = FindObjectsByType<BCDealMechanism>(FindObjectsSortMode.None);
            BCDealMechanism best = null;
            int bestScore = int.MinValue;

            for (int i = 0; i < allMechanisms.Length; i++)
            {
                BCDealMechanism candidate = allMechanisms[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = candidate.ConfigurationScore;
                if (tableRoot != null && candidate.transform.IsChildOf(tableRoot))
                {
                    score += 3;
                }

                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static Transform EnsureIntroAnchor(Transform currentAnchor, string name, Transform parent, Vector3 localPosition, Quaternion localRotation)
        {
            Transform anchor = currentAnchor;
            if (anchor == null)
            {
                Transform existing = parent.Find(name);
                if (existing != null)
                {
                    anchor = existing;
                }
                else
                {
                    GameObject anchorObject = new GameObject(name);
                    anchor = anchorObject.transform;
                    anchor.SetParent(parent, false);
                }
            }

            anchor.SetParent(parent, false);
            anchor.localPosition = localPosition;
            anchor.localRotation = localRotation;
            return anchor;
        }

        private Quaternion ComputeIntroLookRotation(Transform relativeTo, Vector3 localPosition)
        {
            Vector3 worldPosition = relativeTo.TransformPoint(localPosition);
            Vector3 lookTarget = enemyRoot != null ? enemyRoot.position + Vector3.up * 0.48f : tableRoot != null ? tableRoot.position : relativeTo.position + relativeTo.forward;
            Vector3 forward = (lookTarget - worldPosition).normalized;
            Quaternion worldRotation = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward, Vector3.up) : relativeTo.rotation;
            return Quaternion.Inverse(relativeTo.rotation) * worldRotation;
        }
    }
}
