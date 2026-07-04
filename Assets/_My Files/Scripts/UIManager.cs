using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace SlideAndMatch
{
    /// <summary>
    /// Manages all Canvas UI: score display, New Game / Undo buttons,
    /// Game Over and Win overlay panels.
    /// If no Inspector references are wired, creates the entire UI from code.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        // ── Inspector references (optional — auto-creates if null) ──
        [Header("Score Display")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI bestScoreText;

        [Header("Panels")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject winPanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject menuPanel;

        [Header("Buttons")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button undoButton;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button keepPlayingButton;
        [SerializeField] private Button winNewGameButton;
        [SerializeField] private RectTransform scoreBoxRect;
        [SerializeField] private RectTransform bestBoxRect;

        [Header("Ads Settings")]
        [SerializeField] private GameObject adPromptPanel;
        [SerializeField] private GameObject adPlayPanel;
        [SerializeField] private TextMeshProUGUI adCountdownText;
        [SerializeField] private Button adCloseButton;

        [Header("Premium UI References")]
        [SerializeField] private RectTransform progressBarFillRect;
        private RectTransform autoProgressBarFill;
        private Button musicToggleBtn;
        private Button sfxToggleBtn;
        private GameObject howToPlayModal;
        private Sprite cachedButtonSprite;
        private TMP_FontAsset cachedRegularFont;
        private TMP_FontAsset cachedBoldFont;

        public static bool IsAdActive { get; private set; }
        public static bool IsSplashActive { get; private set; }

        private float nextNetworkCheckTime = 0f;
        private bool lastOnlineState = true;

        // ── Animation state ──
        private int lastScore = -1;
        private int lastBest = -1;
        private int displayedScore = 0;
        private int displayedBest = 0;

        private Coroutine scoreTickCoroutine;
        private Coroutine scorePunchCoroutine;
        private Coroutine bestPunchCoroutine;

        // ───────────────────────────────────────────────────────
        // Lifecycle
        // ───────────────────────────────────────────────────────
        void Start()
        {
            // Auto-create UI if nothing is wired
            if (scoreText == null)
            {
                CreateUI();
            }
            else
            {
                EnsureAdPanelsCreated();
            }

            Canvas mainCanvas = null;
            GameObject dynamicCanvasObj = GameObject.Find("GameCanvas");
            if (dynamicCanvasObj != null)
            {
                mainCanvas = dynamicCanvasObj.GetComponent<Canvas>();
            }
            if (mainCanvas == null)
            {
                mainCanvas = FindAnyObjectByType<Canvas>();
            }
            Transform splashParent = mainCanvas != null ? mainCanvas.transform : transform;

            IsSplashActive = true;
            GameSplashScreen.Create(splashParent, () =>
            {
                IsSplashActive = false;
            });

            // Try to resolve rect references if still null (for custom inspector UIs)
            if (scoreBoxRect == null && scoreText != null && scoreText.transform.parent != null)
                scoreBoxRect = scoreText.transform.parent.GetComponent<RectTransform>();
            if (bestBoxRect == null && bestScoreText != null && bestScoreText.transform.parent != null)
                bestBoxRect = bestScoreText.transform.parent.GetComponent<RectTransform>();

            // Ensure an EventSystem exists (required for buttons)
            EnsureEventSystem();

            // Subscribe to game events
            var gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[UIManager] GameManager.Instance is null.");
                return;
            }

            gm.OnScoreChanged       += UpdateScore;
            gm.OnGameOver           += ShowGameOver;
            gm.OnGameWon            += ShowWin;
            gm.OnGameStarted        += HideOverlays;
            gm.OnBoardRefreshNeeded += HideOverlays;

            // Wire buttons
            if (newGameButton  != null) newGameButton.onClick.AddListener(()  => gm.StartNewGame());
            if (undoButton     != null) undoButton.onClick.AddListener(()     => OnUndoClicked());
            if (retryButton    != null) retryButton.onClick.AddListener(()    => gm.StartNewGame());
            if (keepPlayingButton != null)
                keepPlayingButton.onClick.AddListener(() => { gm.ContinuePlaying(); winPanel?.SetActive(false); });
            if (winNewGameButton != null)
                winNewGameButton.onClick.AddListener(() => gm.StartNewGame());

            HideOverlays();
            UpdateScore(gm.Score, gm.BestScore);
            UpdateUndoButtonState();
        }

        void Update()
        {
            if (Time.time >= nextNetworkCheckTime)
            {
                nextNetworkCheckTime = Time.time + 1f;
                bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
                if (isOnline != lastOnlineState)
                {
                    lastOnlineState = isOnline;
                    UpdateUndoButtonState();
                }
            }
        }

        void OnDestroy()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.OnScoreChanged       -= UpdateScore;
            gm.OnGameOver           -= ShowGameOver;
            gm.OnGameWon            -= ShowWin;
            gm.OnGameStarted        -= HideOverlays;
            gm.OnBoardRefreshNeeded -= HideOverlays;
        }

        // ───────────────────────────────────────────────────────
        // Event callbacks
        // ───────────────────────────────────────────────────────

        private void UpdateScore(int score, int best)
        {
            UpdateUndoButtonState();

            // Stop any running tick coroutine to prevent overwriting the score
            if (scoreTickCoroutine != null)
            {
                StopCoroutine(scoreTickCoroutine);
                scoreTickCoroutine = null;
            }

            // First time setup, just initialize and set immediately
            if (lastScore == -1)
            {
                lastScore = score;
                lastBest = best;
                displayedScore = score;
                displayedBest = best;
                if (scoreText != null) scoreText.text = score.ToString();
                if (bestScoreText != null) bestScoreText.text = best.ToString();
                UpdateProgressBar(score);
                return;
            }

            bool scoreIncreased = score > lastScore;
            bool bestIncreased = best > lastBest;

            // Handle Score Increase
            if (scoreIncreased)
            {
                // Trigger score box punch
                if (scorePunchCoroutine != null) StopCoroutine(scorePunchCoroutine);
                scorePunchCoroutine = StartCoroutine(PunchScale(scoreBoxRect, 0.15f, 0.2f));

                // Spawn UI floating text popup above the score box
                SpawnCanvasFloatingText(scoreBoxRect, "+" + (score - lastScore));
            }
            else
            {
                // Instant update if score decreased or reset
                displayedScore = score;
                if (scoreText != null) scoreText.text = score.ToString();
            }

            // Handle Best Score Increase
            if (bestIncreased)
            {
                // Trigger best box punch
                if (bestPunchCoroutine != null) StopCoroutine(bestPunchCoroutine);
                bestPunchCoroutine = StartCoroutine(PunchScale(bestBoxRect, 0.15f, 0.2f));
            }
            else
            {
                // Instant update if best score decreased or reset
                displayedBest = best;
                if (bestScoreText != null) bestScoreText.text = best.ToString();
            }

            // If score or best increased, tick them up smoothly
            if (scoreIncreased || bestIncreased)
            {
                if (scoreTickCoroutine != null) StopCoroutine(scoreTickCoroutine);
                scoreTickCoroutine = StartCoroutine(TickScoresCoroutine(score, best));
            }

            lastScore = score;
            lastBest = best;
            UpdateProgressBar(score);
        }

        private void UpdateProgressBar(int score)
        {
            RectTransform fill = progressBarFillRect != null ? progressBarFillRect : autoProgressBarFill;
            if (fill != null)
            {
                float percentage = Mathf.Clamp01((float)score / 10000f);
                fill.anchorMax = new Vector2(percentage, 1f);
                fill.offsetMax = Vector2.zero;
            }
        }

        private void ShowGameOver()
        {
            gameOverPanel?.SetActive(true);
            AudioManager.Instance?.PlayGameOver();
        }

        private void ShowWin()
        {
            winPanel?.SetActive(true);
        }

        private void HideOverlays()
        {
            gameOverPanel?.SetActive(false);
            winPanel?.SetActive(false);
            if (adPromptPanel != null) adPromptPanel.SetActive(false);
            if (adPlayPanel != null) adPlayPanel.SetActive(false);
            IsAdActive = false;
            UpdateUndoButtonState();
        }

        // ───────────────────────────────────────────────────────
        // EventSystem
        // ───────────────────────────────────────────────────────

        private void EnsureEventSystem()
        {
            EventSystem es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
            }

            Type newModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (newModuleType != null)
            {
                var oldModule = es.GetComponent<StandaloneInputModule>();
                if (oldModule != null)
                {
                    DestroyImmediate(oldModule);
                }

                if (es.GetComponent(newModuleType) == null)
                {
                    es.gameObject.AddComponent(newModuleType);
                }
            }
            else
            {
                if (es.GetComponent<StandaloneInputModule>() == null)
                {
                    es.gameObject.AddComponent<StandaloneInputModule>();
                }
            }
        }

        // ═══════════════════════════════════════════════════════
        // AUTO-CREATE UI (fallback when no Inspector refs)
        // ═══════════════════════════════════════════════════════

        #region Auto UI Creation

        private void CreateUI()
        {
            // ── Canvas ─────────────────────────────────────────
            GameObject canvasObj = new GameObject("GameCanvas");
            canvasObj.transform.SetParent(transform, false);

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // ── SafeAreaPanel ──────────────────────────────────
            GameObject safeAreaObj = new GameObject("SafeAreaPanel");
            safeAreaObj.transform.SetParent(canvasObj.transform, false);
            RectTransform safeAreaRt = safeAreaObj.AddComponent<RectTransform>();
            safeAreaRt.anchorMin = Vector2.zero;
            safeAreaRt.anchorMax = Vector2.one;
            safeAreaRt.offsetMin = Vector2.zero;
            safeAreaRt.offsetMax = Vector2.zero;
            safeAreaObj.AddComponent<SafeAreaHandler>();

            // ── Top App Bar ────────────────────────────────────
            // Title
            CreateLabel(safeAreaObj.transform, "TitleText", "2048", 80,
                new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(400, 120),
                HexColor("#ddb7ff"), FontStyles.Bold);

            // Settings Button
            Button settingsBtn = CreateBtn(safeAreaObj.transform, "SettingsBtn", "",
                new Vector2(1f, 1f), new Vector2(-100, -100), new Vector2(80, 80),
                Color.white);
            Sprite settingsSprite = LoadSpriteFromResources("settings");
            if (settingsSprite != null)
            {
                settingsBtn.GetComponent<Image>().sprite = settingsSprite;
            }
            var label = settingsBtn.transform.Find("Label");
            if (label != null) label.gameObject.SetActive(false);
            settingsBtn.onClick.AddListener(() => ShowSettingsPanel());

            // ── Score boxes ────────────────────────────────────
            // Score Box
            GameObject scoreBox = CreateBox(safeAreaObj.transform, "ScoreBox",
                new Vector2(0.5f, 1f), new Vector2(-260, -260), new Vector2(440, 140),
                HexColor("#171f33"));
            scoreBoxRect = scoreBox.GetComponent<RectTransform>();

            CreateLabel(scoreBox.transform, "ScoreLabel", "SCORE", 26,
                new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(400, 30),
                HexColor("#94a3b8"), FontStyles.Normal);

            scoreText = CreateLabel(scoreBox.transform, "ScoreValue", "0", 48,
                new Vector2(0.5f, 0.5f), new Vector2(0, -25), new Vector2(400, 60),
                HexColor("#ddb7ff"), FontStyles.Bold);

            // Best Box
            GameObject bestBox = CreateBox(safeAreaObj.transform, "BestBox",
                new Vector2(0.5f, 1f), new Vector2(260, -260), new Vector2(440, 140),
                HexColor("#171f33"));
            bestBoxRect = bestBox.GetComponent<RectTransform>();

            CreateLabel(bestBox.transform, "BestLabel", "BEST", 26,
                new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(400, 30),
                HexColor("#94a3b8"), FontStyles.Normal);

            bestScoreText = CreateLabel(bestBox.transform, "BestValue", "0", 48,
                new Vector2(0.5f, 0.5f), new Vector2(0, -25), new Vector2(400, 60),
                Color.white, FontStyles.Bold);

            // ── Progress Bar ───────────────────────────────────
            GameObject progressBg = CreateBox(safeAreaObj.transform, "ProgressBarBg",
                new Vector2(0.5f, 1f), new Vector2(0, -370), new Vector2(960, 16),
                HexColor("#171f33"));
            GameObject progressFill = CreateBox(progressBg.transform, "ProgressBarFill",
                new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0, 16),
                HexColor("#b76dff"));
            autoProgressBarFill = progressFill.GetComponent<RectTransform>();
            autoProgressBarFill.anchorMin = new Vector2(0f, 0f);
            autoProgressBarFill.anchorMax = new Vector2(0f, 1f);
            autoProgressBarFill.offsetMin = Vector2.zero;
            autoProgressBarFill.offsetMax = Vector2.zero;

            // ── Action Buttons ─────────────────────────────────
            newGameButton = CreateBtn(safeAreaObj.transform, "NewGameBtn", "NEW GAME",
                new Vector2(0.5f, 0f), new Vector2(-260, 150), new Vector2(450, 110),
                HexColor("#b76dff"));

            undoButton = CreateBtn(safeAreaObj.transform, "UndoBtn", "UNDO",
                new Vector2(0.5f, 0f), new Vector2(260, 150), new Vector2(450, 110),
                HexColor("#3e495d"));

            // ── Game Over Panel ────────────────────────────────
            gameOverPanel = CreateOverlay(safeAreaObj.transform, "GameOverPanel",
                "GAME OVER", HexColor("#ef4444"));

            retryButton = CreateBtn(gameOverPanel.transform, "RetryBtn", "TRY AGAIN",
                new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(340, 80),
                HexColor("#ef4444"));

            // ── Win Panel ──────────────────────────────────────
            winPanel = CreateOverlay(safeAreaObj.transform, "WinPanel",
                "YOU WIN!", HexColor("#b76dff"));
            keepPlayingButton = CreateBtn(winPanel.transform, "KeepPlayingBtn", "KEEP PLAYING",
                new Vector2(0.5f, 0.42f), new Vector2(0, 30), new Vector2(360, 80),
                HexColor("#b76dff"));

            winNewGameButton = CreateBtn(winPanel.transform, "WinNewGameBtn", "NEW GAME",
                new Vector2(0.5f, 0.42f), new Vector2(0, -70), new Vector2(360, 80),
                HexColor("#3e495d"));

            // ── Custom Overlays (Settings & Menu) ──────────────
            CreateSettingsPanel(safeAreaObj.transform);

            CreateAdPanels(safeAreaObj.transform);
        }

        private void CreateSettingsPanel(Transform parent)
        {
            if (settingsPanel != null) return;

            settingsPanel = CreateOverlay(parent, "SettingsPanel", "", Color.clear);
            settingsPanel.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.06f, 0.94f);

            GameObject container = CreateBox(settingsPanel.transform, "SettingsContainer",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 1000), HexColor("#171f33"));

            CreateLabel(container.transform, "SettingsTitle", "SETTINGS", 56,
                new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(800, 80),
                HexColor("#b76dff"), FontStyles.Bold);

            // Music Toggle Btn
            musicToggleBtn = CreateBtn(container.transform, "MusicToggleBtn", "MUSIC: ON",
                new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(700, 110),
                HexColor("#3e495d"));
            musicToggleBtn.onClick.AddListener(() => ToggleMusic());
            UpdateMusicBtnLabel();

            // SFX Toggle Btn
            sfxToggleBtn = CreateBtn(container.transform, "SfxToggleBtn", "SFX: ON",
                new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(700, 110),
                HexColor("#3e495d"));
            sfxToggleBtn.onClick.AddListener(() => ToggleSfx());
            UpdateSfxBtnLabel();

            // How To Play Btn
            Button tutorialBtn = CreateBtn(container.transform, "TutorialBtn", "HOW TO PLAY",
                new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(700, 110),
                HexColor("#3e495d"));
            tutorialBtn.onClick.AddListener(() => ShowHowToPlay());

            // Reset High Score Btn
            Button resetBtn = CreateBtn(container.transform, "ResetBtn", "RESET BEST SCORE",
                new Vector2(0.5f, 0.5f), new Vector2(0, -190), new Vector2(700, 110),
                HexColor("#ef4444"));
            resetBtn.onClick.AddListener(() => ResetBestScoreClick());

            // Close Btn
            Button closeBtn = CreateBtn(container.transform, "SettingsCloseBtn", "CLOSE",
                new Vector2(0.5f, 0f), new Vector2(0, 80), new Vector2(400, 90),
                HexColor("#3e495d"));
            closeBtn.onClick.AddListener(() => settingsPanel.SetActive(false));

            // Create sub-modal for How to Play inside SettingsPanel
            howToPlayModal = CreateOverlay(settingsPanel.transform, "HowToPlayModal", "", Color.clear);
            howToPlayModal.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 0.98f);
            
            GameObject htContainer = CreateBox(howToPlayModal.transform, "HowToPlayContainer",
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(850, 900), HexColor("#222a3d"));

            CreateLabel(htContainer.transform, "HTTitle", "HOW TO PLAY", 48,
                new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(750, 80),
                HexColor("#ddb7ff"), FontStyles.Bold);

            string rules = "Slide tiles in any direction (swiping on screen or using Arrow/WASD keys).\n\n" +
                           "When two tiles of the same value touch, they merge into a single tile with double the value!\n\n" +
                           "Combine tiles to reach the ultimate 2048 tile!\n\n" +
                           "Keep playing to set a new High Score!";

            var ruleLabel = CreateLabel(htContainer.transform, "HTText", rules, 30,
                new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(750, 500),
                Color.white, FontStyles.Normal);
            ruleLabel.textWrappingMode = TextWrappingModes.Normal;
            ruleLabel.alignment = TextAlignmentOptions.Center;

            Button htCloseBtn = CreateBtn(htContainer.transform, "HTCloseBtn", "UNDERSTOOD",
                new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(500, 90),
                HexColor("#b76dff"));
            htCloseBtn.onClick.AddListener(() => howToPlayModal.SetActive(false));
            howToPlayModal.SetActive(false);
        }

        private void ShowSettingsPanel()
        {
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
                UpdateMusicBtnLabel();
                UpdateSfxBtnLabel();
            }
        }

        private void ToggleMusic()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.MusicMuted = !AudioManager.Instance.MusicMuted;
                UpdateMusicBtnLabel();
            }
        }

        private void UpdateMusicBtnLabel()
        {
            if (musicToggleBtn != null)
            {
                var labelTmp = musicToggleBtn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (labelTmp != null)
                {
                    bool isMuted = AudioManager.Instance != null && AudioManager.Instance.MusicMuted;
                    labelTmp.text = isMuted ? "MUSIC: OFF" : "MUSIC: ON";
                }
            }
        }

        private void ToggleSfx()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SfxMuted = !AudioManager.Instance.SfxMuted;
                UpdateSfxBtnLabel();
                if (!AudioManager.Instance.SfxMuted)
                {
                    AudioManager.Instance.PlayMerge();
                }
            }
        }

        private void UpdateSfxBtnLabel()
        {
            if (sfxToggleBtn != null)
            {
                var labelTmp = sfxToggleBtn.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
                if (labelTmp != null)
                {
                    bool isMuted = AudioManager.Instance != null && AudioManager.Instance.SfxMuted;
                    labelTmp.text = isMuted ? "SFX: OFF" : "SFX: ON";
                }
            }
        }

        private void ResetBestScoreClick()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.ResetBestScore();
                SpawnCanvasFloatingText(bestBoxRect, "Score Reset!", HexColor("#ef4444"));
            }
        }

        private void ShowHowToPlay()
        {
            if (howToPlayModal != null)
            {
                howToPlayModal.SetActive(true);
            }
        }

        private void EnsureAdPanelsCreated()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }
            if (canvas == null) return;

            CreateAdPanels(canvas.transform);
        }

        private void CreateAdPanels(Transform parent)
        {
            // ── Ad Prompt Panel ────────────────────────────────
            if (adPromptPanel == null)
            {
                adPromptPanel = CreateOverlay(parent, "AdPromptPanel", "", Color.clear);
                GameObject container = CreateBox(adPromptPanel.transform, "ModalContainer",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 600), HexColor("#1e293b"));
                
                CreateLabel(container.transform, "PromptTitle", "WATCH AD TO UNDO", 48,
                    new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(700, 80),
                    HexColor("#c4b5fd"), FontStyles.Bold);

                var promptDesc = CreateLabel(container.transform, "PromptDesc", "Watch a short video ad to undo your last move and keep playing!", 32,
                    new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(700, 200),
                    HexColor("#e2e8f0"), FontStyles.Normal);
                promptDesc.textWrappingMode = TextWrappingModes.Normal;

                Button watchBtn = CreateBtn(container.transform, "WatchAdBtn", "WATCH AD",
                    new Vector2(0.5f, 0f), new Vector2(-180, 100), new Vector2(300, 80),
                    HexColor("#22c55e"));
                watchBtn.onClick.AddListener(() => StartAdFlow());

                Button cancelBtn = CreateBtn(container.transform, "CancelBtn", "CANCEL",
                    new Vector2(0.5f, 0f), new Vector2(180, 100), new Vector2(300, 80),
                    HexColor("#475569"));
                cancelBtn.onClick.AddListener(() => adPromptPanel.SetActive(false));
            }

            // ── Ad Play Panel ──────────────────────────────────
            if (adPlayPanel == null)
            {
                adPlayPanel = CreateOverlay(parent, "AdPlayPanel", "", Color.clear);
                adPlayPanel.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 1f);

                CreateLabel(adPlayPanel.transform, "SponsoredLabel", "Sponsored Ad", 24,
                    new Vector2(0f, 1f), new Vector2(140, -60), new Vector2(250, 40),
                    HexColor("#64748b"), FontStyles.Normal).alignment = TextAlignmentOptions.Left;

                adCountdownText = CreateLabel(adPlayPanel.transform, "AdCountdownText", "Reward in 5s", 28,
                    new Vector2(1f, 1f), new Vector2(-160, -60), new Vector2(300, 40),
                    HexColor("#f59e0b"), FontStyles.Bold);
                adCountdownText.alignment = TextAlignmentOptions.Right;

                GameObject adContent = CreateBox(adPlayPanel.transform, "AdContent",
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 1100), HexColor("#1e1e24"));
                
                GameObject appIcon = CreateBox(adContent.transform, "AppIcon",
                    new Vector2(0.5f, 1f), new Vector2(0, -150), new Vector2(180, 180), HexColor("#8b5cf6"));
                CreateLabel(appIcon.transform, "IconText", "GAME", 36,
                    Vector2.zero, Vector2.zero, Vector2.zero, Color.white, FontStyles.Bold)
                    .rectTransform.anchorMax = Vector2.one;
                var iconTextRt = appIcon.transform.Find("IconText").GetComponent<RectTransform>();
                iconTextRt.anchorMin = Vector2.zero;
                iconTextRt.anchorMax = Vector2.one;
                iconTextRt.offsetMin = Vector2.zero;
                iconTextRt.offsetMax = Vector2.zero;
                iconTextRt.anchoredPosition = Vector2.zero;

                CreateLabel(adContent.transform, "AppTitle", "Puzzle Match 3D", 48,
                    new Vector2(0.5f, 1f), new Vector2(0, -320), new Vector2(800, 70),
                    Color.white, FontStyles.Bold);

                CreateLabel(adContent.transform, "AppStars", "RATING: 4.9 / 5.0 (1.2M Reviews)", 26,
                    new Vector2(0.5f, 1f), new Vector2(0, -390), new Vector2(800, 50),
                    HexColor("#fbbf24"), FontStyles.Normal);

                GameObject screenshot = CreateBox(adContent.transform, "AdScreenshot",
                    new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(760, 420), HexColor("#2d2d3a"));
                var scrText = CreateLabel(screenshot.transform, "ScreenshotText", "Slide and match numbers to win!", 28,
                    new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(700, 60),
                    HexColor("#e2e8f0"), FontStyles.Italic);
                scrText.textWrappingMode = TextWrappingModes.Normal;

                GameObject miniGrid = CreateBox(screenshot.transform, "MiniGrid",
                    new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(240, 240), HexColor("#1a1a24"));
                
                int[,] miniVals = new int[,] { { 2, 4 }, { 8, 16 } };
                for (int mx = 0; mx < 2; mx++)
                {
                    for (int my = 0; my < 2; my++)
                    {
                        float px = (mx - 0.5f) * 100f;
                        float py = (my - 0.5f) * 100f;
                        GameObject miniTile = CreateBox(miniGrid.transform, "MiniTile",
                            new Vector2(0.5f, 0.5f), new Vector2(px, py), new Vector2(80, 80), HexColor("#f97316"));
                        CreateLabel(miniTile.transform, "Text", miniVals[mx, my].ToString(), 28,
                            Vector2.zero, Vector2.zero, Vector2.zero, Color.white, FontStyles.Bold)
                            .rectTransform.anchorMax = Vector2.one;
                        var tRt = miniTile.transform.Find("Text").GetComponent<RectTransform>();
                        tRt.anchorMin = Vector2.zero;
                        tRt.anchorMax = Vector2.one;
                        tRt.offsetMin = Vector2.zero;
                        tRt.offsetMax = Vector2.zero;
                        tRt.anchoredPosition = Vector2.zero;
                    }
                }

                var appDescLabel = CreateLabel(adContent.transform, "AppDesc", "Challenge your brain with the most addicting slide and match game ever created! Smooth controls, 3D graphics, and hours of fun.", 28,
                    new Vector2(0.5f, 0f), new Vector2(0, 200), new Vector2(800, 160),
                    HexColor("#e2e8f0"), FontStyles.Normal);
                appDescLabel.textWrappingMode = TextWrappingModes.Normal;

                Button installBtn = CreateBtn(adContent.transform, "InstallBtn", "INSTALL NOW",
                    new Vector2(0.5f, 0f), new Vector2(0, 60), new Vector2(600, 90),
                    HexColor("#22c55e"));
                installBtn.onClick.AddListener(() => Debug.Log("Mock Ad Install Button Clicked!"));

                adCloseButton = CreateBtn(adPlayPanel.transform, "AdCloseBtn", "X",
                    new Vector2(1f, 1f), new Vector2(-70, -60), new Vector2(70, 70),
                    HexColor("#ef4444"));
                adCloseButton.onClick.AddListener(() => CloseAdAndUndo());
                adCloseButton.gameObject.SetActive(false);
            }
        }

        // ── Helpers ────────────────────────────────────────────

        private TextMeshProUGUI CreateLabel(Transform parent, string name,
            string text, float fontSize,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size,
            Color color, FontStyles style = FontStyles.Normal)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var tmp = obj.AddComponent<TextMeshProUGUI>();

            // Apply Roboto Font
            if (style == FontStyles.Bold)
            {
                if (cachedBoldFont == null)
                {
                    cachedBoldFont = Resources.Load<TMP_FontAsset>("Roboto_Condensed-Bold SDF");
                }
                if (cachedBoldFont != null)
                {
                    tmp.font = cachedBoldFont;
                }
            }
            else
            {
                if (cachedRegularFont == null)
                {
                    cachedRegularFont = Resources.Load<TMP_FontAsset>("Roboto_Condensed-Regular SDF");
                }
                if (cachedRegularFont != null)
                {
                    tmp.font = cachedRegularFont;
                }
            }

            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            return tmp;
        }

        private GameObject CreateBox(Transform parent, string name,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = obj.AddComponent<Image>();
            img.color = color;

            return obj;
        }

        private Button CreateBtn(Transform parent, string name, string label,
            Vector2 anchor, Vector2 anchoredPos, Vector2 size, Color bgColor)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            Image img = obj.AddComponent<Image>();
            if (cachedButtonSprite == null)
            {
                cachedButtonSprite = LoadSpriteFromResources("Button", "minus_0", new Vector4(60f, 60f, 60f, 60f));
            }
            if (cachedButtonSprite != null)
            {
                img.sprite = cachedButtonSprite;
                img.type = Image.Type.Sliced;
            }
            img.color = bgColor;

            Button btn = obj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.highlightedColor = Color.white;
            cb.pressedColor     = new Color(0.60f, 0.60f, 0.60f, 1f);
            cb.selectedColor    = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.disabledColor    = new Color(0.85f, 0.85f, 0.85f, 0.35f);
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;

            obj.AddComponent<ButtonHoverHandler>();

            // Stretched label
            CreateLabel(obj.transform, "Label", label, 28,
                Vector2.zero, Vector2.zero, Vector2.zero,          // ignored when stretched
                Color.white, FontStyles.Bold)
                .rectTransform.anchorMax = Vector2.one;            // stretch

            // Fix: set anchors properly so text fills button
            var labelRt = obj.transform.Find("Label").GetComponent<RectTransform>();
            labelRt.anchorMin      = Vector2.zero;
            labelRt.anchorMax      = Vector2.one;
            labelRt.offsetMin      = Vector2.zero;
            labelRt.offsetMax      = Vector2.zero;
            labelRt.anchoredPosition = Vector2.zero;

            return btn;
        }

        private GameObject CreateOverlay(Transform parent, string name,
            string title, Color titleColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.04f, 0.06f, 0.88f);

            CreateLabel(panel.transform, "Title", title, 80,
                new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(700, 140),
                titleColor, FontStyles.Bold);

            panel.SetActive(false);
            return panel;
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        // ───────────────────────────────────────────────────────
        // UI Animation Helpers
        // ───────────────────────────────────────────────────────

        private IEnumerator PunchScale(RectTransform rect, float punchFactor, float duration)
        {
            if (rect == null) yield break;

            Vector3 originalScale = Vector3.one;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Sine wave punch multiplier: 1.0 -> 1.15 -> 1.0
                float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * punchFactor;
                rect.localScale = originalScale * scaleMultiplier;
                yield return null;
            }

            rect.localScale = originalScale;
        }

        private IEnumerator TickScoresCoroutine(int targetScore, int targetBest)
        {
            float duration = 0.25f;
            float elapsed = 0f;
            int startScore = displayedScore;
            int startBest = displayedBest;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Ticking values
                displayedScore = Mathf.RoundToInt(Mathf.Lerp(startScore, targetScore, t));
                displayedBest = Mathf.RoundToInt(Mathf.Lerp(startBest, targetBest, t));

                if (scoreText != null) scoreText.text = displayedScore.ToString();
                if (bestScoreText != null) bestScoreText.text = displayedBest.ToString();

                yield return null;
            }

            displayedScore = targetScore;
            displayedBest = targetBest;
            if (scoreText != null) scoreText.text = displayedScore.ToString();
            if (bestScoreText != null) bestScoreText.text = displayedBest.ToString();
            
            scoreTickCoroutine = null;
        }

        private void SpawnCanvasFloatingText(RectTransform parentBox, string text, Color? customColor = null)
        {
            if (parentBox == null) return;

            GameObject go = new GameObject("UIFloatingText");
            go.transform.SetParent(parentBox, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, 10f); // start slightly above the box
            rt.sizeDelta = new Vector2(200f, 50f);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 32f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = customColor ?? HexColor("#eab308");

            StartCoroutine(AnimateCanvasFloatingText(go, rt, tmp));
        }

        private IEnumerator AnimateCanvasFloatingText(GameObject go, RectTransform rt, TextMeshProUGUI tmp)
        {
            float duration = 0.8f;
            float elapsed = 0f;
            Vector2 startPos = rt.anchoredPosition;
            Vector2 endPos = startPos + new Vector2(0f, 60f); // float up 60 units
            Color startColor = tmp.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float ease = 1f - Mathf.Pow(1f - t, 3f); // EaseOutCubic
                rt.anchoredPosition = Vector2.Lerp(startPos, endPos, ease);
                tmp.color = Color.Lerp(startColor, endColor, t);

                yield return null;
            }

            Destroy(go);
        }

        private Action onMockAdClosedCallback;

        private bool AreAdsEnabled()
        {
            if (AdManager.Instance != null)
            {
                return AdManager.Instance.adsEnabled;
            }
            return PlayerPrefs.GetInt("MockAdsEnabled", 1) == 1;
        }

        private void OnUndoClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.CanUndo) return;

            var gb = FindAnyObjectByType<GameBoard>();
            if (gb != null && gb.IsAnimating) return;

            bool adsAreEnabled = AreAdsEnabled();

            if (adsAreEnabled)
            {
                // Block undo in offline mode
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    SpawnCanvasFloatingText(undoButton.GetComponent<RectTransform>(), "Internet Required!", HexColor("#ef4444"));
                    UpdateUndoButtonState();
                    return;
                }

                if (adPromptPanel != null)
                {
                    adPromptPanel.SetActive(true);
                }
                else
                {
                    StartAdFlow();
                }
            }
            else
            {
                // Directly undo if ads are disabled
                gm.Undo();
                UpdateUndoButtonState();
            }
        }

        private void StartAdFlow()
        {
            if (adPromptPanel != null) adPromptPanel.SetActive(false);

            if (AdManager.Instance != null)
            {
                AdManager.Instance.ShowRewardedAd(() =>
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.Undo();
                    }
                    UpdateUndoButtonState();
                });
            }
            else
            {
                StartMockAdSequence(() =>
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.Undo();
                    }
                    UpdateUndoButtonState();
                });
            }
        }

        private void UpdateUndoButtonState()
        {
            if (undoButton != null && GameManager.Instance != null)
            {
                bool adsAreEnabled = AreAdsEnabled();
                if (adsAreEnabled)
                {
                    bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;
                    undoButton.interactable = GameManager.Instance.CanUndo && !IsAdActive && isOnline;
                }
                else
                {
                    undoButton.interactable = GameManager.Instance.CanUndo && !IsAdActive;
                }
            }
        }

        public void StartMockAdSequence(Action onComplete)
        {
            StartCoroutine(PlayMockAdRoutine(onComplete));
        }

        private IEnumerator PlayMockAdRoutine(Action onComplete)
        {
            if (adPromptPanel != null) adPromptPanel.SetActive(false);
            if (adPlayPanel != null) adPlayPanel.SetActive(true);

            IsAdActive = true;
            UpdateUndoButtonState();

            if (adCloseButton != null)
            {
                adCloseButton.gameObject.SetActive(false);
            }

            int countdown = 5;
            while (countdown > 0)
            {
                if (adCountdownText != null)
                {
                    adCountdownText.text = $"Reward in {countdown}s";
                }
                yield return new WaitForSeconds(1f);
                countdown--;
            }

            if (adCountdownText != null)
            {
                adCountdownText.text = "Reward Granted!";
                adCountdownText.color = HexColor("#22c55e");
            }

            if (adCloseButton != null)
            {
                adCloseButton.gameObject.SetActive(true);
                StartCoroutine(PunchScale(adCloseButton.GetComponent<RectTransform>(), 0.2f, 0.3f));
            }

            onMockAdClosedCallback = onComplete;
        }

        private void CloseAdAndUndo()
        {
            if (adPlayPanel != null) adPlayPanel.SetActive(false);
            IsAdActive = false;

            if (adCountdownText != null)
            {
                adCountdownText.color = HexColor("#f59e0b");
            }

            if (onMockAdClosedCallback != null)
            {
                onMockAdClosedCallback.Invoke();
                onMockAdClosedCallback = null;
            }

            UpdateUndoButtonState();
        }

        private Sprite LoadSpriteFromResources(string resourcePath, string subSpriteName = null, Vector4? customBorder = null)
        {
            Sprite baseSprite = null;
            if (!string.IsNullOrEmpty(subSpriteName))
            {
                Sprite[] sprites = Resources.LoadAll<Sprite>(resourcePath);
                foreach (var s in sprites)
                {
                    if (s.name == subSpriteName)
                    {
                        baseSprite = s;
                        break;
                    }
                }
            }
            else
            {
                baseSprite = Resources.Load<Sprite>(resourcePath);
            }

            if (baseSprite != null && customBorder.HasValue)
            {
                return Sprite.Create(
                    baseSprite.texture,
                    baseSprite.rect,
                    new Vector2(0.5f, 0.5f),
                    baseSprite.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect,
                    customBorder.Value
                );
            }

            return baseSprite;
        }

        #endregion
    }

    public class ButtonHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Vector3 originalScale = Vector3.one;
        private float hoverScale = 1.05f;
        private float pressScale = 0.95f;
        private float animationDuration = 0.1f;
        private Coroutine scaleCoroutine;

        void Awake()
        {
            originalScale = transform.localScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            StartScaleAnimation(originalScale * hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            StartScaleAnimation(originalScale);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            StartScaleAnimation(originalScale * pressScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Vector3 target = eventData.dragging || !RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, eventData.position, eventData.pressEventCamera) 
                ? originalScale 
                : originalScale * hoverScale;
            StartScaleAnimation(target);
        }

        private void StartScaleAnimation(Vector3 targetScale)
        {
            if (gameObject.activeInHierarchy)
            {
                if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
                scaleCoroutine = StartCoroutine(AnimateScale(targetScale));
            }
            else
            {
                transform.localScale = targetScale;
            }
        }

        private IEnumerator AnimateScale(Vector3 targetScale)
        {
            Vector3 startScale = transform.localScale;
            float elapsed = 0f;
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / animationDuration);
                yield return null;
            }
            transform.localScale = targetScale;
        }

        void OnDisable()
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            transform.localScale = originalScale;
        }
    }
}
