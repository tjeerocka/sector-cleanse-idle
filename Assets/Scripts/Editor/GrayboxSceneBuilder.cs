using System.IO;
using SectorCleanse.Combat;
using SectorCleanse.Core;
using SectorCleanse.Meta;
using SectorCleanse.Player;
using SectorCleanse.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SectorCleanse.EditorTools
{
    /// <summary>
    /// One-click graybox scene setup: <b>Sector Cleanse → Build Graybox Scene</b>.
    ///
    /// Creates (or rebuilds) <c>Assets/Scenes/Graybox.unity</c> containing:
    ///  * an orthographic 2D camera;
    ///  * GameManager with all scene roots wired up;
    ///  * GameplayRoot → Lanes (LaneSystem + visible lane strips), Player (movement,
    ///    squad + soldier formation, weapon), EnemySpawner and HUD;
    ///  * MenuRoot canvas: bank total, front-line stats, DEPLOY / CONTINUE / ABANDON,
    ///    the Barracks screen and the upgrade shop (Barracks + UpgradeShop live on the
    ///    GameManager object);
    ///  * HUD with a pause button (suspends and saves the run);
    ///  * GameOverRoot overlay with the round summary;
    ///  * an EventSystem matching the project's active input handling.
    ///
    /// Safe to re-run: it always generates a fresh scene, so tweak values in the
    /// components (or in this file) rather than hand-editing the generated scene
    /// if you plan to rebuild it.
    /// </summary>
    public static class GrayboxSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Graybox.unity";
        private const string SquareSpritePath = "Assets/Art/Graybox/Square.png";

        private static readonly Color BackgroundColor = new Color(0.12f, 0.14f, 0.19f);
        private static readonly Color LaneColorA = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color LaneColorB = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color PlayerLineColor = new Color(0.3f, 1f, 0.4f, 0.6f);
        private static readonly Color PlayerColor = new Color(0.2f, 0.9f, 0.35f);
        private static readonly Color ButtonColor = new Color(0.2f, 0.75f, 0.35f);

        private static readonly Color SoldierColor = new Color(1f, 0.55f, 0.1f);

        [MenuItem("Sector Cleanse/Build Graybox Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild graybox scene?",
                    $"{ScenePath} already exists and will be overwritten.", "Rebuild", "Cancel"))
            {
                return;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Sprite square = GetOrCreateSquareSprite();

            Camera cam = CreateCamera();
            GameManager gameManager = new GameObject("GameManager").AddComponent<GameManager>();

            // --- Gameplay -------------------------------------------------------
            var gameplayRoot = new GameObject("GameplayRoot");

            var lanesGo = new GameObject("Lanes");
            lanesGo.transform.SetParent(gameplayRoot.transform, false);
            LaneSystem lanes = lanesGo.AddComponent<LaneSystem>();
            CreateLaneVisuals(lanes, square);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(gameplayRoot.transform, false);
            playerGo.transform.position = lanes.GetPlayerPosition(lanes.CenterLane);
            playerGo.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            var playerRenderer = playerGo.AddComponent<SpriteRenderer>();
            playerRenderer.sprite = square;
            playerRenderer.color = PlayerColor;
            playerRenderer.sortingOrder = 10;
            PlayerController controller = playerGo.AddComponent<PlayerController>();
            PlayerSquad squad = playerGo.AddComponent<PlayerSquad>();
            SquadFormation formation = playerGo.AddComponent<SquadFormation>();
            Weapon weapon = playerGo.AddComponent<Weapon>();

            SetRef(controller, "laneSystem", lanes);
            SetRef(controller, "worldCamera", cam);
            SetRef(formation, "soldierSprite", square);
            SetColor(formation, "soldierColor", SoldierColor);
            SetRef(weapon, "laneSystem", lanes);
            SetRef(weapon, "bulletSprite", square);

            var spawnerGo = new GameObject("EnemySpawner");
            spawnerGo.transform.SetParent(gameplayRoot.transform, false);
            EnemySpawner spawner = spawnerGo.AddComponent<EnemySpawner>();
            SetRef(spawner, "laneSystem", lanes);
            SetRef(spawner, "player", controller);
            SetRef(spawner, "squad", squad);
            SetRef(spawner, "enemySprite", square);

            CreateHud(gameplayRoot.transform, gameManager, squad, weapon);

            // Meta-progression lives on the GameManager object (persists across rounds).
            UpgradeShop shop = gameManager.gameObject.AddComponent<UpgradeShop>();
            SetRef(shop, "weapon", weapon);
            Barracks barracks = gameManager.gameObject.AddComponent<Barracks>();
            SetRef(squad, "barracks", barracks);

            // --- UI ---------------------------------------------------------------
            GameObject menuRoot = CreateMenu(gameManager, weapon, shop, barracks);
            GameObject gameOverRoot = CreateGameOverOverlay();
            CreateEventSystem();

            // --- Wiring -----------------------------------------------------------
            SetRef(gameManager, "menuRoot", menuRoot);
            SetRef(gameManager, "gameplayRoot", gameplayRoot);
            SetRef(gameManager, "gameOverRoot", gameOverRoot);

            // GameManager sets the real active states on Start; hide the overlay in
            // the editor so the scene view isn't cluttered.
            gameOverRoot.SetActive(false);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Selection.activeGameObject = gameManager.gameObject;
            Debug.Log($"[Sector Cleanse] Graybox scene built at {ScenePath}. Press Play and click DEPLOY.");
        }

        // ------------------------------------------------------------------
        // Scene objects
        // ------------------------------------------------------------------

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            var cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            return cam;
        }

        /// <summary>Faint strips per lane plus the player line, so lanes are visible in the Game view.</summary>
        private static void CreateLaneVisuals(LaneSystem lanes, Sprite square)
        {
            float bottom = lanes.PlayerLineWorldY - 1f;
            float top = lanes.SpawnLineWorldY;
            float height = top - bottom;

            for (int i = 0; i < lanes.LaneCount; i++)
            {
                var strip = new GameObject($"LaneStrip_{i}");
                strip.transform.SetParent(lanes.transform, false);
                strip.transform.position = new Vector3(lanes.GetLaneX(i), bottom + height * 0.5f, 0f);
                strip.transform.localScale = new Vector3(lanes.LaneSpacing * 0.92f, height, 1f);

                var sr = strip.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.color = i % 2 == 0 ? LaneColorA : LaneColorB;
                sr.sortingOrder = -10;
            }

            var line = new GameObject("PlayerLine");
            line.transform.SetParent(lanes.transform, false);
            line.transform.position = new Vector3(lanes.transform.position.x, lanes.PlayerLineWorldY - 0.5f, 0f);
            line.transform.localScale = new Vector3(lanes.LaneCount * lanes.LaneSpacing, 0.05f, 1f);

            var lineRenderer = line.AddComponent<SpriteRenderer>();
            lineRenderer.sprite = square;
            lineRenderer.color = PlayerLineColor;
            lineRenderer.sortingOrder = -5;
        }

        private static GameObject CreateMenu(GameManager gameManager, Weapon weapon, UpgradeShop shop,
            Barracks barracks)
        {
            Canvas canvas = CreateCanvas("MenuRoot", 0);

            CreateText(canvas.transform, "Title", "SECTOR CLEANSE\nIDLE", 90,
                new Vector2(0f, 650f), new Vector2(1000f, 300f));

            Text bankLabel = CreateText(canvas.transform, "Bank", "BANK $0", 72,
                new Vector2(0f, 470f), new Vector2(1000f, 120f));
            bankLabel.color = new Color(1f, 0.85f, 0.3f);

            Text statsLabel = CreateText(canvas.transform, "Stats", "", 32,
                new Vector2(0f, 335f), new Vector2(1060f, 150f));

            // --- Run buttons: DEPLOY (no saved run) or CONTINUE + ABANDON ---
            Button deploy = CreateButton(canvas.transform, "DeployButton", "DEPLOY", 64,
                new Vector2(0f, 170f), new Vector2(500f, 150f), ButtonColor);
            UnityEventTools.AddPersistentListener(deploy.onClick, gameManager.StartRound);

            Button resume = CreateButton(canvas.transform, "ContinueButton", "CONTINUE", 64,
                new Vector2(0f, 170f), new Vector2(500f, 150f), new Color(0.2f, 0.6f, 0.95f));
            UnityEventTools.AddPersistentListener(resume.onClick, gameManager.ContinueRun);

            Button abandon = CreateButton(canvas.transform, "AbandonButton", "ABANDON RUN (BANK MONEY)", 30,
                new Vector2(0f, 50f), new Vector2(500f, 80f), new Color(0.6f, 0.2f, 0.2f));
            UnityEventTools.AddPersistentListener(abandon.onClick, gameManager.AbandonRun);

            // --- Barracks ---
            GameObject barracksPanel = CreateBarracksPanel(canvas.transform, barracks);
            Button openBarracks = CreateButton(canvas.transform, "BarracksButton", "BARRACKS", 52,
                new Vector2(0f, -80f), new Vector2(820f, 130f), new Color(0.95f, 0.55f, 0.15f));
            UnityEventTools.AddBoolPersistentListener(openBarracks.onClick, barracksPanel.SetActive, true);

            // --- Shop ---
            CreateText(canvas.transform, "ShopHeader", "SHOP", 56,
                new Vector2(0f, -200f), new Vector2(1000f, 100f));
            CreateShopRow(canvas.transform, shop, UpgradeShop.DamageId, -320f, new Color(0.85f, 0.3f, 0.3f));
            CreateShopRow(canvas.transform, shop, UpgradeShop.FireRateId, -480f, new Color(0.3f, 0.55f, 0.95f));

            // Created after everything else in the menu so the open overlay draws (and blocks clicks) on top.
            barracksPanel.transform.SetAsLastSibling();

            MenuView menuView = canvas.gameObject.AddComponent<MenuView>();
            SetRef(menuView, "bankLabel", bankLabel);
            SetRef(menuView, "statsLabel", statsLabel);
            SetRef(menuView, "deployButton", deploy.gameObject);
            SetRef(menuView, "continueButton", resume.gameObject);
            SetRef(menuView, "abandonButton", abandon.gameObject);
            SetRef(menuView, "weapon", weapon);
            SetRef(menuView, "shop", shop);
            SetRef(menuView, "barracks", barracks);

            return canvas.gameObject;
        }

        /// <summary>Full-screen barracks overlay: recruit button, scrollable tier list, close button.</summary>
        private static GameObject CreateBarracksPanel(Transform parent, Barracks barracks)
        {
            var panel = new GameObject("BarracksPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch((RectTransform)panel.transform);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.97f);

            Text header = CreateText(panel.transform, "Header", "BARRACKS", 56,
                new Vector2(0f, 820f), new Vector2(1040f, 100f));
            Text info = CreateText(panel.transform, "Info", "", 28,
                new Vector2(0f, 735f), new Vector2(1040f, 80f));
            info.color = new Color(0.8f, 0.8f, 0.85f);

            Button recruit = CreateButton(panel.transform, "RecruitButton", "", 44,
                new Vector2(0f, 620f), new Vector2(820f, 130f), new Color(0.95f, 0.55f, 0.15f));

            // Scroll view with a vertical list of tier rows.
            var scroll = new GameObject("TierList", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scroll.transform.SetParent(panel.transform, false);
            var scrollRt = (RectTransform)scroll.transform;
            scrollRt.anchoredPosition = new Vector2(0f, -70f);
            scrollRt.sizeDelta = new Vector2(1040f, 1220f);
            scroll.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scroll.transform, false);
            Stretch((RectTransform)viewport.transform);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scroll.GetComponent<ScrollRect>();
            scrollRect.content = contentRt;
            scrollRect.viewport = (RectTransform)viewport.transform;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            Button close = CreateButton(panel.transform, "CloseButton", "CLOSE", 48,
                new Vector2(0f, -820f), new Vector2(400f, 120f), new Color(0.4f, 0.4f, 0.45f));
            UnityEventTools.AddBoolPersistentListener(close.onClick, panel.SetActive, false);

            BarracksView view = panel.AddComponent<BarracksView>();
            SetRef(view, "barracks", barracks);
            SetRef(view, "headerLabel", header);
            SetRef(view, "infoLabel", info);
            SetRef(view, "recruitButton", recruit);
            SetRef(view, "recruitLabel", recruit.GetComponentInChildren<Text>());
            SetRef(view, "rowContainer", contentRt);

            panel.SetActive(false);
            return panel;
        }

        private static void CreateShopRow(Transform parent, UpgradeShop shop, string upgradeId, float y, Color color)
        {
            Button button = CreateButton(parent, $"Shop_{upgradeId}", "", 40,
                new Vector2(0f, y), new Vector2(820f, 150f), color);
            UpgradeButtonView view = button.gameObject.AddComponent<UpgradeButtonView>();
            SetRef(view, "shop", shop);
            SetString(view, "upgradeId", upgradeId);
            SetRef(view, "label", button.GetComponentInChildren<Text>());
        }

        /// <summary>
        /// Side-column HUD (money left, combat stats right) so it never covers the lanes.
        /// Lives under GameplayRoot so it hides with the round.
        /// </summary>
        private static void CreateHud(Transform gameplayRoot, GameManager gameManager, PlayerSquad squad,
            Weapon weapon)
        {
            Canvas canvas = CreateCanvas("HUD", 5);
            canvas.transform.SetParent(gameplayRoot, false);

            // Pause: saves the run and returns to the menu (CONTINUE resumes it).
            Button pause = CreateButton(canvas.transform, "PauseButton", "II", 56,
                Vector2.zero, new Vector2(150f, 110f), new Color(0.3f, 0.3f, 0.35f, 0.9f));
            var pauseRt = (RectTransform)pause.transform;
            pauseRt.anchorMin = pauseRt.anchorMax = new Vector2(0.5f, 1f);
            pauseRt.pivot = new Vector2(0.5f, 1f);
            pauseRt.anchoredPosition = new Vector2(0f, -30f);
            UnityEventTools.AddPersistentListener(pause.onClick, gameManager.SuspendRun);

            Text left = CreateCornerText(canvas.transform, "LeftStats", rightSide: false);
            Text right = CreateCornerText(canvas.transform, "RightStats", rightSide: true);

            HudView hud = canvas.gameObject.AddComponent<HudView>();
            SetRef(hud, "leftLabel", left);
            SetRef(hud, "rightLabel", right);
            SetRef(hud, "squad", squad);
            SetRef(hud, "weapon", weapon);
        }

        private static Text CreateCornerText(Transform parent, string name, bool rightSide)
        {
            Text text = CreateText(parent, name, "", 52, Vector2.zero, Vector2.zero);
            text.alignment = rightSide ? TextAnchor.UpperRight : TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;

            float x = rightSide ? 1f : 0f;
            var rt = (RectTransform)text.transform;
            rt.anchorMin = new Vector2(x, 1f);
            rt.anchorMax = new Vector2(x, 1f);
            rt.pivot = new Vector2(x, 1f);
            rt.anchoredPosition = new Vector2(rightSide ? -40f : 40f, -40f);
            rt.sizeDelta = new Vector2(560f, 420f);
            return text;
        }

        private static GameObject CreateGameOverOverlay()
        {
            Canvas canvas = CreateCanvas("GameOverRoot", 10);

            var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)dim.transform);
            dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            CreateText(canvas.transform, "Message", "SQUAD WIPED", 100,
                new Vector2(0f, 200f), new Vector2(1000f, 200f)).color = new Color(1f, 0.35f, 0.3f);

            Text summary = CreateText(canvas.transform, "Summary", "", 56,
                new Vector2(0f, -110f), new Vector2(1000f, 360f));
            summary.color = new Color(1f, 0.85f, 0.3f);

            GameOverView view = canvas.gameObject.AddComponent<GameOverView>();
            SetRef(view, "summaryLabel", summary);

            return canvas.gameObject;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            // Portrait mobile reference resolution.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            // Match height so the full portrait layout always fits vertically, whatever the
            // aspect ratio (editor Game view, tablets, tall phones).
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        private static Button CreateButton(Transform parent, string name, string text, int fontSize,
            Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = color;

            Text label = CreateText(go.transform, "Label", text, fontSize, Vector2.zero, Vector2.zero);
            Stretch((RectTransform)label.transform);
            return go.GetComponent<Button>();
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize,
            Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Assigns a private [SerializeField] reference the same way the Inspector would.</summary>
        private static void SetRef(Object target, string fieldName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Sector Cleanse] Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(Object target, string fieldName, Color value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Sector Cleanse] Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            prop.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Sector Cleanse] Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A plain white 1x1-unit sprite used for every graybox shape.</summary>
        private static Sprite GetOrCreateSquareSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
            if (existing) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(SquareSpritePath));
            var tex = new Texture2D(4, 4);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(SquareSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.Refresh(); // Register the new folder before importing into it.
            AssetDatabase.ImportAsset(SquareSpritePath);
            var importer = AssetImporter.GetAtPath(SquareSpritePath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Sector Cleanse] Could not import {SquareSpritePath}.");
                return null;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4f; // 4px texture -> exactly 1 world unit.
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(SquareSpritePath);
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
