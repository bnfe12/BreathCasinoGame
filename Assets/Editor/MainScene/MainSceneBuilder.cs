using System;
using BreathCasino.Core;
using BreathCasino.Gameplay;
using BreathCasino.Systems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BreathCasino.EditorTools
{
    public static class MainSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/mainScene.unity";
        private const string EnvironmentModelPath = "Assets/Scenes/mainScene.fbx";
        private const string GunModelPath = "Assets/Scenes/gun1212.fbx";
        private const string MainCardPrefabPath = "Assets/Prefabs/Cards/MainCard.prefab";
        private const string SpecialCardPrefabPath = "Assets/Prefabs/Cards/SpecialCard.prefab";

        [MenuItem("Breath Casino/Main Scene/Rebuild mainScene")]
        public static void BuildMainScene()
        {
            BuildMainSceneInternal();
        }

        public static void BuildMainSceneBatch()
        {
            BuildMainSceneInternal();
            EditorApplication.Exit(0);
        }

        private static void BuildMainSceneInternal()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject environmentRoot = InstantiateAssetPrefab(EnvironmentModelPath, "EnvironmentRoot");
            Bounds sceneBounds = CalculateReferenceBounds(environmentRoot);

            Vector3 center = sceneBounds.center;
            float tableDepth = Mathf.Max(2.8f, sceneBounds.size.z);
            float tableTopY = center.y + Mathf.Clamp(sceneBounds.extents.y * 0.15f, 0.9f, 1.35f);

            GameObject mainCameraObject = new("Main Camera");
            Camera camera = mainCameraObject.AddComponent<Camera>();
            mainCameraObject.tag = "MainCamera";
            mainCameraObject.AddComponent<AudioListener>();
            mainCameraObject.AddComponent<UniversalAdditionalCameraData>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 150f;
            BCCameraController cameraController = mainCameraObject.AddComponent<BCCameraController>();
            BCInteractionRaycaster raycaster = mainCameraObject.AddComponent<BCInteractionRaycaster>();

            GameObject lightObject = new("Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            directionalLight.intensity = 1.15f;
            lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);

            GameObject gameRootObject = new("GameRoot");
            SceneBootstrap bootstrap = gameRootObject.AddComponent<SceneBootstrap>();

            Transform managersRoot = CreateEmpty("Managers", gameRootObject.transform, Vector3.zero);
            Transform tableRoot = CreateEmpty("TableRoot", gameRootObject.transform, new Vector3(center.x, tableTopY, center.z));
            Transform playerRoot = CreateActorRoot("Player", gameRootObject.transform, new Vector3(center.x, tableTopY + 0.42f, center.z - tableDepth * 0.95f));
            Transform enemyRoot = CreateActorRoot("Enemy", gameRootObject.transform, new Vector3(center.x, tableTopY + 0.42f, center.z + tableDepth * 0.95f));
            enemyRoot.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Transform gunRoot = CreateEmpty("GunRoot", gameRootObject.transform, Vector3.zero);
            Transform uiRoot = CreateUi(gameRootObject.transform, out Canvas canvas, out Text statusText);

            Transform playerWeaponHolder = CreateEmpty("WeaponHolder", playerRoot, new Vector3(0.45f, 0.16f, 0f));
            Transform enemyWeaponHolder = CreateEmpty("WeaponHolder", enemyRoot, new Vector3(0.45f, 0.16f, 0f));
            Transform playerCardsHolder = CreateEmpty("CardsHolder", playerRoot, new Vector3(0f, -0.05f, 0.34f));
            Transform enemyCardsHolder = CreateEmpty("CardsHolder", enemyRoot, new Vector3(0f, -0.05f, 0.54f));

            Transform playerMainSlot = CreateCardSlot("PlayerMainSlot", tableRoot, new Vector3(-0.72f, 0.015f, -0.72f), new Vector3(0.52f, 0.02f, 0.36f), Side.Player, SlotKind.Slot1, "Combat");
            Transform playerSpecialSlot = CreateCardSlot("PlayerSpecialSlot", tableRoot, new Vector3(0.18f, 0.015f, -0.72f), new Vector3(0.32f, 0.02f, 0.24f), Side.Player, SlotKind.Slot2, "Special");
            Transform enemyMainSlot = CreateCardSlot("EnemyMainSlot", tableRoot, new Vector3(-0.72f, 0.015f, 0.72f), new Vector3(0.52f, 0.02f, 0.36f), Side.Enemy, SlotKind.Slot1, "Combat");
            Transform enemySpecialSlot = CreateCardSlot("EnemySpecialSlot", tableRoot, new Vector3(0.18f, 0.015f, 0.72f), new Vector3(0.32f, 0.02f, 0.24f), Side.Enemy, SlotKind.Slot2, "Special");
            Transform gunSpot = CreateEmpty("GunSpot", tableRoot, new Vector3(1.35f, 0.08f, 0f));
            Transform bulletSpot = CreateEmpty("BulletSpot", tableRoot, new Vector3(-1.42f, 0.12f, 0f));
            Transform ticketStack = CreateTicketStack(tableRoot, new Vector3(1.28f, 0.02f, -0.88f));
            Transform playerSpawn = CreateEmpty("TableCenterPlayerSpawn", tableRoot, new Vector3(-0.2f, 0.03f, -0.18f));
            Transform enemySpawn = CreateEmpty("TableCenterEnemySpawn", tableRoot, new Vector3(-0.2f, 0.03f, 0.18f));

            Transform cameraTargets = CreateEmpty("CameraTargets", tableRoot, Vector3.zero);
            Transform cardsPlaceholder = CreateCameraPlaceholder(cameraTargets, "CardsPosition", playerRoot.position + new Vector3(0f, 0.3f, 0.25f), playerCardsHolder.position);
            Transform playerSlotsPlaceholder = CreateCameraPlaceholder(cameraTargets, "PlayerSlotsPosition", tableRoot.TransformPoint(new Vector3(-0.1f, 0.6f, -1.1f)), tableRoot.position + new Vector3(0f, 0f, -0.1f));
            Transform tableSlotsPlaceholder = CreateCameraPlaceholder(cameraTargets, "TableSlotsPosition", tableRoot.position + new Vector3(0f, 0.78f, 0.1f), tableRoot.position);
            Transform enemySlotsPlaceholder = CreateCameraPlaceholder(cameraTargets, "EnemySlotsPosition", tableRoot.position + new Vector3(0f, 0.7f, -0.45f), enemyMainSlot.position);

            CreateGunModel(gunRoot, gunSpot);

            GameObject audioManagerObject = new("BCAudioManager");
            audioManagerObject.transform.SetParent(managersRoot, false);
            audioManagerObject.AddComponent<BCAudioManager>();

            GameObject cardManagerObject = new("BCCardManager");
            cardManagerObject.transform.SetParent(managersRoot, false);
            BCCardManager cardManager = cardManagerObject.AddComponent<BCCardManager>();

            GameObject ticketManagerObject = new("TicketManager");
            ticketManagerObject.transform.SetParent(managersRoot, false);
            TicketManager ticketManager = ticketManagerObject.AddComponent<TicketManager>();

            GameObject handAnimatorObject = new("BCHandAnimator");
            handAnimatorObject.transform.SetParent(managersRoot, false);
            BCHandAnimator handAnimator = handAnimatorObject.AddComponent<BCHandAnimator>();

            GameObject gameManagerObject = new("GameManager");
            gameManagerObject.transform.SetParent(managersRoot, false);
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();

            GameObject validatorObject = new("SceneValidator");
            validatorObject.transform.SetParent(managersRoot, false);
            SceneValidator validator = validatorObject.AddComponent<SceneValidator>();

            SetObjectReference(raycaster, "raycastCamera", camera);
            SetObjectReference(cameraController, "tableCenter", tableRoot);
            SetObjectReference(cameraController, "playerRoot", playerRoot);
            SetObjectReference(cameraController, "playerHandHolder", playerCardsHolder);
            SetObjectReference(cameraController, "playerMainSlot", playerMainSlot);
            SetObjectReference(cameraController, "enemyMainSlot", enemyMainSlot);
            SetObjectReference(cameraController, "cardsPlaceholder", cardsPlaceholder);
            SetObjectReference(cameraController, "playerSlotsPlaceholder", playerSlotsPlaceholder);
            SetObjectReference(cameraController, "tableSlotsPlaceholder", tableSlotsPlaceholder);
            SetObjectReference(cameraController, "enemySlotsPlaceholder", enemySlotsPlaceholder);

            SetObjectReference(cardManager, "mainCardPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(MainCardPrefabPath));
            SetObjectReference(cardManager, "specialCardPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(SpecialCardPrefabPath));
            SetObjectReference(handAnimator, "playerHandHolder", playerCardsHolder);
            SetObjectReference(handAnimator, "playerSpecialHolder", playerCardsHolder);
            SetObjectReference(handAnimator, "enemyHandHolder", enemyCardsHolder);
            SetObjectReference(handAnimator, "enemySpecialHolder", enemyCardsHolder);

            bootstrap.managersRoot = managersRoot;
            bootstrap.tableRoot = tableRoot;
            bootstrap.playerRoot = playerRoot;
            bootstrap.enemyRoot = enemyRoot;
            bootstrap.gunRoot = gunRoot;
            bootstrap.uiRoot = uiRoot;
            bootstrap.mainCamera = camera;
            bootstrap.directionalLight = directionalLight;
            bootstrap.hudCanvas = canvas;
            bootstrap.statusText = statusText;
            bootstrap.playerMainSlot = playerMainSlot;
            bootstrap.playerSpecialSlot = playerSpecialSlot;
            bootstrap.enemyMainSlot = enemyMainSlot;
            bootstrap.enemySpecialSlot = enemySpecialSlot;
            bootstrap.gunSpot = gunSpot;
            bootstrap.bulletSpot = bulletSpot;
            bootstrap.ticketStack = ticketStack;
            bootstrap.tableCenterPlayerSpawn = playerSpawn;
            bootstrap.tableCenterEnemySpawn = enemySpawn;
            bootstrap.playerWeaponHolder = playerWeaponHolder;
            bootstrap.playerHandHolder = playerCardsHolder;
            bootstrap.playerSpecialHolder = playerCardsHolder;
            bootstrap.enemyWeaponHolder = enemyWeaponHolder;
            bootstrap.enemyHandHolder = enemyCardsHolder;
            bootstrap.enemySpecialHolder = enemyCardsHolder;
            bootstrap.gameManager = gameManager;
            bootstrap.ticketManager = ticketManager;
            bootstrap.cardManager = cardManager;
            bootstrap.validator = validator;
            bootstrap.handAnimator = handAnimator;

            statusText.text = "Breath Casino Debug\nmainScene generated for custom-model setup.";

            EnsureEventSystem();

            validator.ValidateAndReport(bootstrap);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = gameRootObject;
            Debug.Log($"Built main scene at {ScenePath}");
        }

        private static void CreateGunModel(Transform gunRoot, Transform gunSpot)
        {
            if (gunRoot == null || gunSpot == null)
            {
                return;
            }

            gunRoot.SetParent(gunSpot, false);
            gunRoot.localPosition = Vector3.zero;
            gunRoot.localRotation = Quaternion.identity;

            GameObject gunModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunModelPath);
            if (gunModelPrefab != null)
            {
                GameObject gunModel = PrefabUtility.InstantiatePrefab(gunModelPrefab) as GameObject;
                if (gunModel != null)
                {
                    gunModel.name = gunModelPrefab.name;
                    gunModel.transform.SetParent(gunRoot, false);
                    gunModel.transform.localPosition = Vector3.zero;
                    gunModel.transform.localRotation = Quaternion.identity;
                }
            }

            BoxCollider collider = gunRoot.gameObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = gunRoot.gameObject.AddComponent<BoxCollider>();
            }

            collider.center = Vector3.zero;
            collider.size = new Vector3(0.36f, 0.18f, 0.18f);
        }

        private static Transform CreateTicketStack(Transform parent, Vector3 localPosition)
        {
            Transform stack = CreateEmpty("TicketStack", parent, localPosition);
            BCTicketStack stackComponent = stack.gameObject.AddComponent<BCTicketStack>();
            BCTicketStackInteractable interactable = stack.gameObject.AddComponent<BCTicketStackInteractable>();
            BoxCollider collider = stack.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.22f, 0.1f, 0.34f);
            SetObjectReference(interactable, "ticketStack", stackComponent);
            return stack;
        }

        private static Transform CreateActorRoot(string name, Transform parent, Vector3 worldPosition)
        {
            GameObject root = new(name);
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;
            return root.transform;
        }

        private static Transform CreateCardSlot(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Side side, SlotKind kind, string label)
        {
            GameObject slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slot.name = name;
            slot.transform.SetParent(parent, false);
            slot.transform.localPosition = localPosition;
            slot.transform.localScale = localScale;

            Renderer renderer = slot.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = CreateLitMaterial(kind == SlotKind.Slot2
                    ? (side == Side.Player ? new Color(0.42f, 0.24f, 0.65f) : new Color(0.66f, 0.22f, 0.54f))
                    : (side == Side.Player ? new Color(0.16f, 0.36f, 0.4f) : new Color(0.5f, 0.2f, 0.2f)));
            }

            BCCardSlot cardSlot = slot.AddComponent<BCCardSlot>();
            cardSlot.Configure(side, kind, label);

            if (side == Side.Player)
            {
                BCSlotInteractable interactable = slot.AddComponent<BCSlotInteractable>();
                interactable.SetMainSlot(kind == SlotKind.Slot1);
            }

            return slot.transform;
        }

        private static Transform CreateCameraPlaceholder(Transform parent, string name, Vector3 position, Vector3 lookAt)
        {
            Transform placeholder = CreateEmpty(name, parent, Vector3.zero);
            placeholder.position = position;
            Vector3 direction = lookAt - position;
            placeholder.rotation = direction.sqrMagnitude > 0.001f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
            return placeholder;
        }

        private static Transform CreateUi(Transform parent, out Canvas canvas, out Text statusText)
        {
            GameObject uiRoot = new("UI");
            uiRoot.transform.SetParent(parent, false);
            canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            uiRoot.AddComponent<CanvasScaler>();
            uiRoot.AddComponent<GraphicRaycaster>();

            GameObject textObject = new("StatusText");
            textObject.transform.SetParent(uiRoot.transform, false);
            statusText = textObject.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 20;
            statusText.color = Color.white;
            statusText.alignment = TextAnchor.UpperLeft;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rect = statusText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
            rect.sizeDelta = new Vector2(760f, 420f);

            return uiRoot.transform;
        }

        private static Transform CreateEmpty(string name, Transform parent, Vector3 localPosition)
        {
            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            return gameObject.transform;
        }

        private static Material CreateLitMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new(shader);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            return material;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();

            Type inputSystemModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemModuleType != null)
            {
                eventSystemObject.AddComponent(inputSystemModuleType);
            }
        }

        private static GameObject InstantiateAssetPrefab(string assetPath, string fallbackName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.name = fallbackName;
            }

            return instance;
        }

        private static Bounds CalculateReferenceBounds(GameObject root)
        {
            if (root == null)
            {
                return new Bounds(Vector3.zero, new Vector3(5f, 2f, 3.2f));
            }

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, new Vector3(5f, 2f, 3.2f));
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
