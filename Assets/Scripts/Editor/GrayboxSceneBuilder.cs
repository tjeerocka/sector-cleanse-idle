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
    ///    squad + soldier formation, weapon + manual fire), EnemySpawner, WaveDirector
    ///    (elites + buffs/debuffs) and HUD;
    ///  * MenuRoot canvas: bank total, front-line stats, DEPLOY / CONTINUE / ABANDON,
    ///    the Barracks screen, the upgrade shop, the Highscores screen and the
    ///    first-launch name screen (Barracks, UpgradeShop and PlayerProfile live on the
    ///    GameManager object);
    ///  * HUD with a pause button (suspends and saves the run), the hold-to-fire button
    ///    with its heat bar, the elite timer, a banner and the active effects list;
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
            ManualFire manualFire = playerGo.AddComponent<ManualFire>();
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

            var directorGo = new GameObject("WaveDirector");
            directorGo.transform.SetParent(gameplayRoot.transform, false);
            RunEffects effects = directorGo.AddComponent<RunEffects>();
            WaveDirector director = directorGo.AddComponent<WaveDirector>();
            SetRef(effects, "weapon", weapon);
            SetRef(effects, "squad", squad);
            SetRef(effects, "spawner", spawner);
            SetRef(director, "laneSystem", lanes);
            SetRef(director, "player", controller);
            SetRef(director, "squad", squad);
            SetRef(director, "weapon", weapon);
            SetRef(director, "spawner", spawner);
            SetRef(director, "effects", effects);
            SetRef(director, "sprite", square);

            CreateHud(gameplayRoot.transform, gameManager, squad, weapon, manualFire, director, effects);

            // Meta-progression lives on the GameManager object (persists across rounds).
            UpgradeShop shop = gameManager.gameObject.AddComponent<UpgradeShop>();
            SetRef(shop, "weapon", weapon);
            Barracks barracks = gameManager.gameObject.AddComponent<Barracks>();
            PlayerProfile profile = gameManager.gameObject.AddComponent<PlayerProfile>();
            SetRef(squad, "barracks", barracks);

            // --- UI ---------------------------------------------------------------
            GameObject menuRoot = CreateMenu(gameManager, weapon, shop, barracks, profile);
            GameObject gameOverRoot = CreateGameOverOverlay(profile);
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
            Barracks barracks, PlayerProfile profile)
        {
            Canvas canvas = CreateCanvas("MenuRoot", 0);

            CreateText(canvas.transform, "Title", "SECTOR CLEANSE\nIDLE", 90,
                new Vector2(0f, 700f), new Vector2(1000f, 260f));

            Text bankLabel = CreateText(canvas.transform, "Bank", "BANK $0", 72,
                new Vector2(0f, 530f), new Vector2(1000f, 110f));
            bankLabel.color = new Color(1f, 0.85f, 0.3f);

            Text statsLabel = CreateText(canvas.transform, "Stats", "", 32,
                new Vector2(0f, 365f), new Vector2(1060f, 200f));

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

            // --- Highscores ---
            GameObject leaderboardPanel = CreateLeaderboardPanel(canvas.transform, profile);
            Button openLeaderboard = CreateButton(canvas.transform, "HighscoresButton", "HIGHSCORES", 52,
                new Vector2(0f, -640f), new Vector2(820f, 130f), new Color(0.85f, 0.7f, 0.15f));
            UnityEventTools.AddBoolPersistentListener(openLeaderboard.onClick, leaderboardPanel.SetActive, true);

            // --- Name entry (blocks everything until a name is chosen) ---
            NameEntryView nameEntry = CreateNameEntryPanel(canvas.transform, profile);

            // Overlays are moved last so, when open, they draw (and block clicks) on top of the menu.
            barracksPanel.transform.SetAsLastSibling();
            leaderboardPanel.transform.SetAsLastSibling();
            nameEntry.transform.SetAsLastSibling();

            MenuView menuView = canvas.gameObject.AddComponent<MenuView>();
            SetRef(menuView, "bankLabel", bankLabel);
            SetRef(menuView, "statsLabel", statsLabel);
            SetRef(menuView, "deployButton", deploy.gameObject);
            SetRef(menuView, "continueButton", resume.gameObject);
            SetRef(menuView, "abandonButton", abandon.gameObject);
            SetRef(menuView, "weapon", weapon);
            SetRef(menuView, "shop", shop);
            SetRef(menuView, "barracks", barracks);
            SetRef(menuView, "profile", profile);
            SetRef(menuView, "nameEntry", nameEntry);

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

            RectTransform contentRt = CreateScrollList(panel.transform, "TierList",
                new Vector2(0f, -70f), new Vector2(1040f, 1220f));

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

        /// <summary>Full-screen highscores overlay: ranked list, your rank, close button.</summary>
        private static GameObject CreateLeaderboardPanel(Transform parent, PlayerProfile profile)
        {
            var panel = new GameObject("HighscoresPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch((RectTransform)panel.transform);
            panel.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.97f);

            CreateText(panel.transform, "Header", "HIGHSCORES", 64,
                new Vector2(0f, 820f), new Vector2(1040f, 110f)).color = new Color(1f, 0.85f, 0.3f);
            CreateText(panel.transform, "Subtitle", "RANKED BY HIGHEST WAVE", 30,
                new Vector2(0f, 740f), new Vector2(1040f, 60f)).color = new Color(0.8f, 0.8f, 0.85f);

            Text yourRank = CreateText(panel.transform, "YourRank", "", 44,
                new Vector2(0f, 650f), new Vector2(1040f, 90f));

            RectTransform content = CreateScrollList(panel.transform, "Ranking",
                new Vector2(0f, -60f), new Vector2(1040f, 1260f));

            Button close = CreateButton(panel.transform, "CloseButton", "CLOSE", 48,
                new Vector2(0f, -820f), new Vector2(400f, 120f), new Color(0.4f, 0.4f, 0.45f));
            UnityEventTools.AddBoolPersistentListener(close.onClick, panel.SetActive, false);

            LeaderboardView view = panel.AddComponent<LeaderboardView>();
            SetRef(view, "profile", profile);
            SetRef(view, "rowContainer", content);
            SetRef(view, "yourRankLabel", yourRank);

            panel.SetActive(false);
            return panel;
        }

        /// <summary>Full-screen "choose your name" overlay (active by default; hides itself once a name exists).</summary>
        private static NameEntryView CreateNameEntryPanel(Transform parent, PlayerProfile profile)
        {
            var panel = new GameObject("NameEntryPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            Stretch((RectTransform)panel.transform);
            panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 1f);

            CreateText(panel.transform, "Title", "WELCOME, COMMANDER", 72,
                new Vector2(0f, 420f), new Vector2(1040f, 120f)).color = new Color(1f, 0.85f, 0.3f);
            CreateText(panel.transform, "Rules",
                $"CHOOSE YOUR PLAYER NAME\n{PlayerProfile.MinLength}-{PlayerProfile.MaxLength} CHARACTERS, LETTERS A-Z AND DIGITS 0-9",
                34, new Vector2(0f, 290f), new Vector2(1040f, 120f));

            InputField input = CreateInputField(panel.transform, new Vector2(0f, 130f), new Vector2(760f, 130f));

            Text error = CreateText(panel.transform, "Error", "", 36,
                new Vector2(0f, 10f), new Vector2(1040f, 80f));
            error.color = new Color(1f, 0.4f, 0.35f);

            Button confirm = CreateButton(panel.transform, "ConfirmButton", "CONFIRM", 56,
                new Vector2(0f, -130f), new Vector2(500f, 140f), ButtonColor);

            NameEntryView view = panel.AddComponent<NameEntryView>();
            SetRef(view, "profile", profile);
            SetRef(view, "input", input);
            SetRef(view, "confirmButton", confirm);
            SetRef(view, "errorLabel", error);
            return view;
        }

        private static InputField CreateInputField(Transform parent, Vector2 anchoredPosition, Vector2 size)
        {
            var go = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd");
            image.type = Image.Type.Sliced;

            Text text = CreateText(go.transform, "Text", "", 60, Vector2.zero, Vector2.zero);
            Text placeholder = CreateText(go.transform, "Placeholder", "NAME", 60, Vector2.zero, Vector2.zero);
            foreach (Text t in new[] { text, placeholder })
            {
                var trt = (RectTransform)t.transform;
                Stretch(trt);
                trt.offsetMin = new Vector2(24f, 8f);
                trt.offsetMax = new Vector2(-24f, -8f);
                t.alignment = TextAnchor.MiddleCenter;
                t.supportRichText = false;
            }
            text.color = new Color(0.1f, 0.1f, 0.12f);
            placeholder.color = new Color(0.55f, 0.55f, 0.6f);
            placeholder.fontStyle = FontStyle.Italic;

            var input = go.GetComponent<InputField>();
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = PlayerProfile.MaxLength;
            input.contentType = InputField.ContentType.Alphanumeric;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        /// <summary>Scroll view with a vertical, auto-sizing list. Returns the content to add rows to.</summary>
        private static RectTransform CreateScrollList(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            var scroll = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scroll.transform.SetParent(parent, false);
            var scrollRt = (RectTransform)scroll.transform;
            scrollRt.anchoredPosition = anchoredPosition;
            scrollRt.sizeDelta = size;
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

            return contentRt;
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
            Weapon weapon, ManualFire manualFire, WaveDirector director, RunEffects effects)
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
            SetRef(hud, "director", director);

            CreateFireButton(canvas.transform, manualFire);

            // --- Elite timer bar (top centre, under the pause button) ---
            RectTransform eliteBar = CreateBar(canvas.transform, "EliteBar", new Vector2(0.5f, 1f),
                new Vector2(0f, -165f), new Vector2(680f, 60f), new Color(0.75f, 0.3f, 1f), out _, out Text eliteLabel);
            eliteLabel.fontSize = 36;
            eliteBar.gameObject.SetActive(false);

            // --- Banner (centre) ---
            Text banner = CreateText(canvas.transform, "Banner", "", 64,
                new Vector2(0f, 260f), new Vector2(1040f, 260f));
            banner.raycastTarget = false;

            // --- Active effects (left column, under the stats) ---
            Text effectsLabel = CreateText(canvas.transform, "Effects", "", 40, Vector2.zero, Vector2.zero);
            effectsLabel.alignment = TextAnchor.UpperLeft;
            effectsLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            effectsLabel.supportRichText = true;
            effectsLabel.raycastTarget = false;
            var effectsRt = (RectTransform)effectsLabel.transform;
            effectsRt.anchorMin = effectsRt.anchorMax = new Vector2(0f, 1f);
            effectsRt.pivot = new Vector2(0f, 1f);
            effectsRt.anchoredPosition = new Vector2(40f, -480f);
            effectsRt.sizeDelta = new Vector2(560f, 320f);

            CombatHudView combat = canvas.gameObject.AddComponent<CombatHudView>();
            SetRef(combat, "director", director);
            SetRef(combat, "effects", effects);
            SetRef(combat, "squad", squad);
            SetRef(combat, "bannerLabel", banner);
            SetRef(combat, "eliteBar", eliteBar.gameObject);
            SetRef(combat, "eliteBarFill", eliteBar.Find("Fill"));
            SetRef(combat, "eliteBarLabel", eliteLabel);
            SetRef(combat, "effectsLabel", effectsLabel);
        }

        /// <summary>Hold-to-fire button (bottom right) with its heat bar above it.</summary>
        private static void CreateFireButton(Transform parent, ManualFire manualFire)
        {
            var go = new GameObject("FireButton", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-30f, 50f);
            rt.sizeDelta = new Vector2(280f, 280f);

            var image = go.GetComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); // Round.
            image.color = new Color(0.85f, 0.3f, 0.25f, 0.85f);

            Text label = CreateText(go.transform, "Label", "HOLD\nFIRE", 52, Vector2.zero, Vector2.zero);
            Stretch((RectTransform)label.transform);
            label.raycastTarget = false;

            RectTransform heatBar = CreateBar(parent, "HeatBar", new Vector2(1f, 0f),
                new Vector2(-30f, 345f), new Vector2(280f, 40f), new Color(1f, 0.6f, 0.2f),
                out Image heatFill, out Text heatLabel);
            heatBar.pivot = new Vector2(1f, 0f);
            heatBar.anchoredPosition = new Vector2(-30f, 345f);
            heatLabel.text = "HEAT";
            heatLabel.fontSize = 26;

            FireButtonView view = go.AddComponent<FireButtonView>();
            SetRef(view, "manualFire", manualFire);
            SetRef(view, "background", image);
            SetRef(view, "label", label);
            SetRef(view, "heatFill", heatFill.rectTransform);
            SetRef(view, "heatFillImage", heatFill);
        }

        /// <summary>
        /// A horizontal bar: dark background, a "Fill" child scaled on X from the left
        /// (0..1), and a centred label. Not clickable.
        /// </summary>
        private static RectTransform CreateBar(Transform parent, string name, Vector2 anchor,
            Vector2 anchoredPosition, Vector2 size, Color fillColor, out Image fill, out Text label)
        {
            var bar = new GameObject(name, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
            var background = bar.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.6f);
            background.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bar.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            Stretch(fillRt);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.localScale = new Vector3(0f, 1f, 1f);
            fill = fillGo.GetComponent<Image>();
            fill.color = fillColor;
            fill.raycastTarget = false;

            label = CreateText(bar.transform, "Label", "", 32, Vector2.zero, Vector2.zero);
            Stretch((RectTransform)label.transform);
            label.raycastTarget = false;
            return rt;
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

        private static GameObject CreateGameOverOverlay(PlayerProfile profile)
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
            SetRef(view, "profile", profile);

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
