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

        public static bool IsAdActive { get; private set; }

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

            if (es.GetComponent<StandaloneInputModule>() == null)
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
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

            // ── Title ──────────────────────────────────────────
            CreateLabel(canvasObj.transform, "TitleText", "2048", 80,
                new Vector2(0.5f, 1f), new Vector2(0, -100), new Vector2(500, 120),
                HexColor("#c4b5fd"), FontStyles.Bold);

            // ── Score boxes ────────────────────────────────────
            // Score
            GameObject scoreBox = CreateBox(canvasObj.transform, "ScoreBox",
                new Vector2(0.5f, 1f), new Vector2(-150, -240), new Vector2(250, 110),
                HexColor("#1e1e24"));
            scoreBoxRect = scoreBox.GetComponent<RectTransform>();

            CreateLabel(scoreBox.transform, "ScoreLabel", "SCORE", 26,
                new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(220, 36),
                HexColor("#94a3b8"), FontStyles.Normal);

            scoreText = CreateLabel(scoreBox.transform, "ScoreValue", "0", 48,
                new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(220, 60),
                Color.white, FontStyles.Bold);

            // Best
            GameObject bestBox = CreateBox(canvasObj.transform, "BestBox",
                new Vector2(0.5f, 1f), new Vector2(150, -240), new Vector2(250, 110),
                HexColor("#1e1e24"));
            bestBoxRect = bestBox.GetComponent<RectTransform>();

            CreateLabel(bestBox.transform, "BestLabel", "BEST", 26,
                new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(220, 36),
                HexColor("#94a3b8"), FontStyles.Normal);

            bestScoreText = CreateLabel(bestBox.transform, "BestValue", "0", 48,
                new Vector2(0.5f, 1f), new Vector2(0, -58), new Vector2(220, 60),
                Color.white, FontStyles.Bold);

            // ── Buttons ────────────────────────────────────────
            newGameButton = CreateBtn(canvasObj.transform, "NewGameBtn", "NEW GAME",
                new Vector2(0.5f, 0f), new Vector2(-140, 120), new Vector2(240, 72),
                HexColor("#8b5cf6"));

            undoButton = CreateBtn(canvasObj.transform, "UndoBtn", "UNDO",
                new Vector2(0.5f, 0f), new Vector2(140, 120), new Vector2(240, 72),
                HexColor("#334155"));

            // ── Game Over Panel ────────────────────────────────
            gameOverPanel = CreateOverlay(canvasObj.transform, "GameOverPanel",
                "GAME OVER", HexColor("#ef4444"));

            retryButton = CreateBtn(gameOverPanel.transform, "RetryBtn", "TRY AGAIN",
                new Vector2(0.5f, 0.42f), Vector2.zero, new Vector2(320, 72),
                HexColor("#ef4444"));

            // ── Win Panel ──────────────────────────────────────
            winPanel = CreateOverlay(canvasObj.transform, "WinPanel",
                "YOU WIN!", HexColor("#8b5cf6"));
            keepPlayingButton = CreateBtn(winPanel.transform, "KeepPlayingBtn", "KEEP PLAYING",
                new Vector2(0.5f, 0.42f), new Vector2(0, 20), new Vector2(340, 72),
                HexColor("#8b5cf6"));

            winNewGameButton = CreateBtn(winPanel.transform, "WinNewGameBtn", "NEW GAME",
                new Vector2(0.5f, 0.42f), new Vector2(0, -70), new Vector2(340, 72),
                HexColor("#334155"));

            CreateAdPanels(canvasObj.transform);
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

                CreateLabel(container.transform, "PromptDesc", "Watch a short video ad to undo your last move and keep playing!", 32,
                    new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(700, 200),
                    HexColor("#94a3b8"), FontStyles.Normal);

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
                CreateLabel(appIcon.transform, "IconText", "🧩", 80,
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

                CreateLabel(adContent.transform, "AppStars", "⭐⭐⭐⭐⭐  4.9 (1.2M Reviews)", 26,
                    new Vector2(0.5f, 1f), new Vector2(0, -390), new Vector2(800, 50),
                    HexColor("#fbbf24"), FontStyles.Normal);

                GameObject screenshot = CreateBox(adContent.transform, "AdScreenshot",
                    new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(760, 420), HexColor("#2d2d3a"));
                CreateLabel(screenshot.transform, "ScreenshotText", "Slide and match numbers to win!", 28,
                    new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(700, 60),
                    HexColor("#94a3b8"), FontStyles.Italic);

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

                CreateLabel(adContent.transform, "AppDesc", "Challenge your brain with the most addicting slide and match game ever created! Smooth controls, 3D graphics, and hours of fun.", 28,
                    new Vector2(0.5f, 0f), new Vector2(0, 200), new Vector2(800, 160),
                    HexColor("#94a3b8"), FontStyles.Normal);

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
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = style;

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
            img.color = bgColor;

            Button btn = obj.AddComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor     = new Color(0.85f, 0.85f, 0.85f, 1f);
            cb.selectedColor    = Color.white;
            cb.disabledColor    = new Color(1f, 1f, 1f, 0.35f);
            btn.colors = cb;

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

        private void OnUndoClicked()
        {
            var gm = GameManager.Instance;
            if (gm == null || !gm.CanUndo) return;

            var gb = FindAnyObjectByType<GameBoard>();
            if (gb != null && gb.IsAnimating) return;

            bool adsAreEnabled = AdManager.Instance == null || AdManager.Instance.adsEnabled;

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
                bool adsAreEnabled = AdManager.Instance == null || AdManager.Instance.adsEnabled;
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

        #endregion
    }
}
