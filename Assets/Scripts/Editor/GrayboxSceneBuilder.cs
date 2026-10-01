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
    ///  * MenuRoot canvas: bank total, deploy stats, a DEPLOY button hooked to
    ///    GameManager.StartRound and the upgrade shop (UpgradeShop on GameManager);
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

        /// <summary>Base squad: just the player. Extra soldiers are bought in the menu shop.</summary>
        private const int StartingSoldiers = 1;

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
            SetInt(squad, "startingSoldiers", StartingSoldiers);
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

            CreateHud(gameplayRoot.transform, squad, weapon);

            // Meta-progression lives on the GameManager object (persists across rounds).
            UpgradeShop shop = gameManager.gameObject.AddComponent<UpgradeShop>();
            SetRef(shop, "squad", squad);
            SetRef(shop, "weapon", weapon);

            // --- UI ---------------------------------------------------------------
            GameObject menuRoot = CreateMenu(gameManager, weapon, squad, shop);
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

        private static GameObject CreateMenu(GameManager gameManager, Weapon weapon, PlayerSquad squad,
            UpgradeShop shop)
        {
            Canvas canvas = CreateCanvas("MenuRoot", 0);

            CreateText(canvas.transform, "Title", "SECTOR CLEANSE\nIDLE", 90,
                new Vector2(0f, 650f), new Vector2(1000f, 300f));

            Text bankLabel = CreateText(canvas.transform, "Bank", "BANK $0", 72,
                new Vector2(0f, 430f), new Vector2(1000f, 120f));
            bankLabel.color = new Color(1f, 0.85f, 0.3f);

            Text statsLabel = CreateText(canvas.transform, "Stats", "", 40,
                new Vector2(0f, 270f), new Vector2(1000f, 140f));

            MenuView menuView = canvas.gameObject.AddComponent<MenuView>();
            SetRef(menuView, "bankLabel", bankLabel);
            SetRef(menuView, "statsLabel", statsLabel);
            SetRef(menuView, "weapon", weapon);
            SetRef(menuView, "squad", squad);
            SetRef(menuView, "shop", shop);

            Button deploy = CreateButton(canvas.transform, "DeployButton", "DEPLOY", 64,
                new Vector2(0f, 50f), new Vector2(500f, 160f), ButtonColor);
            UnityEventTools.AddPersistentListener(deploy.onClick, gameManager.StartRound);

            // --- Shop ---
            CreateText(canvas.transform, "ShopHeader", "SHOP", 56,
                new Vector2(0f, -200f), new Vector2(1000f, 100f));

            CreateShopRow(canvas.transform, shop, UpgradeShop.RecruitSoldierId, -330f, new Color(0.95f, 0.55f, 0.15f));
            CreateShopRow(canvas.transform, shop, UpgradeShop.DamageId, -500f, new Color(0.85f, 0.3f, 0.3f));
            CreateShopRow(canvas.transform, shop, UpgradeShop.FireRateId, -670f, new Color(0.3f, 0.55f, 0.95f));

            return canvas.gameObject;
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
        private static void CreateHud(Transform gameplayRoot, PlayerSquad squad, Weapon weapon)
        {
            Canvas canvas = CreateCanvas("HUD", 5);
            canvas.transform.SetParent(gameplayRoot, false);

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
            rt.sizeDelta = new Vector2(520f, 300f);
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
                new Vector2(0f, -80f), new Vector2(1000f, 300f));
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

        private static void SetInt(Object target, string fieldName, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[Sector Cleanse] Field '{fieldName}' not found on {target.GetType().Name}.");
                return;
            }
            prop.intValue = value;
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

            AssetDatabase.ImportAsset(SquareSpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SquareSpritePath);
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
