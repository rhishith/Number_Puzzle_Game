using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace SlideAndMatch
{
    public class GameSplashScreen : MonoBehaviour
    {
        private System.Action onCompleteCallback;

        // UI References created dynamically
        private CanvasGroup mainCanvasGroup;
        private RectTransform rootRt;
        private Image backgroundImage;
        private Image centralGlowImage;
        private TextMeshProUGUI centerNumberText;
        private GameObject starsContainer;

        // Loading Screen Elements
        private GameObject loadingBarContainer;
        private Image loadingBarFill;
        private TextMeshProUGUI loadingText;
        private TextMeshProUGUI studioText;

        // Assets created dynamically
        private Sprite gradientSprite;
        private Sprite radialGlowSprite;
        private Sprite dotSprite;
        private Sprite ringSprite;
        private Sprite buttonSprite; // cached button background for loading bar

        // Particle pools
        private List<StarParticle> stars = new List<StarParticle>();
        private List<BurstParticle> bursts = new List<BurstParticle>();
        private List<RippleEffect> ripples = new List<RippleEffect>();

        private bool isSplashActive = true;

        private class StarParticle
        {
            public RectTransform rt;
            public Image img;
            public Vector2 speed;
            public float minAlpha;
            public float maxAlpha;
            public float fadeSpeed;
            public float timeOffset;
        }

        private class BurstParticle
        {
            public RectTransform rt;
            public Image img;
            public Vector2 velocity;
            public float life;
            public float maxLife;
        }

        private class RippleEffect
        {
            public RectTransform rt;
            public Image img;
            public float life;
            public float maxLife;
            public float startScale;
            public float targetScale;
        }

        public static GameSplashScreen Create(Transform parent, System.Action onComplete)
        {
            GameObject splashObj = new GameObject("GameSplashScreen");
            splashObj.transform.SetParent(parent, false);

            Canvas canvas = splashObj.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999; // Top overlay

            splashObj.AddComponent<GraphicRaycaster>();
            
            GameSplashScreen splash = splashObj.AddComponent<GameSplashScreen>();
            splash.onCompleteCallback = onComplete;
            return splash;
        }

        void Awake()
        {
            // Set up CanvasGroup to allow fading the whole screen out
            mainCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            mainCanvasGroup.alpha = 1f;
            mainCanvasGroup.blocksRaycasts = true;

            rootRt = GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            GenerateTextures();
            BuildUI();
            SpawnStars();

            StartCoroutine(PlaySplashScreenTimeline());
        }

        void Update()
        {
            if (!isSplashActive) return;

            UpdateStars();
            UpdateBursts();
            UpdateRipples();
        }

        private void GenerateTextures()
        {
            // 1. Background Gradient (Dark Navy #0B1025 to Deep Purple #161A35)
            Texture2D gradTex = new Texture2D(1, 2);
            gradTex.wrapMode = TextureWrapMode.Clamp;
            gradTex.filterMode = FilterMode.Bilinear;
            Color bottomColor;
            Color topColor;
            ColorUtility.TryParseHtmlString("#0B1025", out bottomColor);
            ColorUtility.TryParseHtmlString("#161A35", out topColor);
            gradTex.SetPixels(new Color[] { bottomColor, topColor });
            gradTex.Apply();
            gradientSprite = Sprite.Create(gradTex, new Rect(0, 0, 1, 2), new Vector2(0.5f, 0.5f));

            // 2. Radial Glow (Center Neon Bloom background)
            int glowSize = 256;
            Texture2D glowTex = new Texture2D(glowSize, glowSize);
            glowTex.wrapMode = TextureWrapMode.Clamp;
            glowTex.filterMode = FilterMode.Bilinear;
            Color[] colors = new Color[glowSize * glowSize];
            float glowCenter = glowSize / 2f;
            for (int y = 0; y < glowSize; y++)
            {
                for (int x = 0; x < glowSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(glowCenter, glowCenter)) / glowCenter;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.Pow(alpha, 2.5f); // Soft falloff
                    colors[y * glowSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            glowTex.SetPixels(colors);
            glowTex.Apply();
            radialGlowSprite = Sprite.Create(glowTex, new Rect(0, 0, glowSize, glowSize), new Vector2(0.5f, 0.5f));

            // 3. Dot Sprite for particles
            int dotSize = 16;
            Texture2D dotTex = new Texture2D(dotSize, dotSize);
            dotTex.wrapMode = TextureWrapMode.Clamp;
            dotTex.filterMode = FilterMode.Bilinear;
            Color[] dotColors = new Color[dotSize * dotSize];
            float dotCenter = dotSize / 2f;
            for (int y = 0; y < dotSize; y++)
            {
                for (int x = 0; x < dotSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(dotCenter, dotCenter)) / dotCenter;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha; // Smooth edge
                    dotColors[y * dotSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            dotTex.SetPixels(dotColors);
            dotTex.Apply();
            dotSprite = Sprite.Create(dotTex, new Rect(0, 0, dotSize, dotSize), new Vector2(0.5f, 0.5f));

            // 4. Ring Sprite for expanding ripples
            int ringSize = 128;
            Texture2D ringTex = new Texture2D(ringSize, ringSize);
            ringTex.wrapMode = TextureWrapMode.Clamp;
            ringTex.filterMode = FilterMode.Bilinear;
            Color[] ringColors = new Color[ringSize * ringSize];
            float ringCenter = ringSize / 2f;
            for (int y = 0; y < ringSize; y++)
            {
                for (int x = 0; x < ringSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(ringCenter, ringCenter)) / ringCenter;
                    // ring between 0.85 and 0.95 radial distance
                    float thickness = 0.05f;
                    float rCenter = 0.90f;
                    float alpha = Mathf.Clamp01(1f - (Mathf.Abs(dist - rCenter) / thickness));
                    alpha = alpha * alpha; // Soft ring edges
                    ringColors[y * ringSize + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            ringTex.SetPixels(ringColors);
            ringTex.Apply();
            ringSprite = Sprite.Create(ringTex, new Rect(0, 0, ringSize, ringSize), new Vector2(0.5f, 0.5f));

            // 5. Load Button.png sub-sprite from Resources for the loading bar structure
            Sprite baseButtonSprite = null;
            Sprite[] sprites = Resources.LoadAll<Sprite>("Button");
            foreach (var s in sprites)
            {
                if (s.name == "minus_0")
                {
                    baseButtonSprite = s;
                    break;
                }
            }
            if (baseButtonSprite != null)
            {
                buttonSprite = Sprite.Create(
                    baseButtonSprite.texture,
                    baseButtonSprite.rect,
                    new Vector2(0.5f, 0.5f),
                    baseButtonSprite.pixelsPerUnit,
                    0,
                    SpriteMeshType.FullRect,
                    new Vector4(60f, 60f, 60f, 60f)
                );
            }
        }

        private void BuildUI()
        {
            // Gradient Background Panel
            GameObject bgObj = new GameObject("BackgroundGradient");
            bgObj.transform.SetParent(transform, false);
            backgroundImage = bgObj.AddComponent<Image>();
            backgroundImage.sprite = gradientSprite;
            backgroundImage.color = Color.white;
            RectTransform bgRt = backgroundImage.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            // Stars Container
            starsContainer = new GameObject("StarsContainer");
            starsContainer.transform.SetParent(transform, false);

            // Ambient Glow (Soft Purple center backdrop)
            GameObject glowObj = new GameObject("CentralGlow");
            glowObj.transform.SetParent(transform, false);
            centralGlowImage = glowObj.AddComponent<Image>();
            centralGlowImage.sprite = radialGlowSprite;
            Color glowColor;
            ColorUtility.TryParseHtmlString("#b76dff", out glowColor);
            glowColor.a = 0.22f; // Soft ambient alpha
            centralGlowImage.color = glowColor;
            RectTransform glowRt = centralGlowImage.rectTransform;
            glowRt.sizeDelta = new Vector2(900, 900);
            glowRt.anchoredPosition = Vector2.zero;

            // Centered Number text
            GameObject numObj = new GameObject("CenterNumberText");
            numObj.transform.SetParent(transform, false);
            centerNumberText = numObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset boldFont = Resources.Load<TMP_FontAsset>("Roboto_Condensed-Bold SDF");
            if (boldFont != null) centerNumberText.font = boldFont;
            centerNumberText.alignment = TextAlignmentOptions.Center;
            centerNumberText.fontStyle = FontStyles.Bold;
            centerNumberText.fontSize = 200;
            centerNumberText.text = "";
            RectTransform numRt = centerNumberText.rectTransform;
            numRt.sizeDelta = new Vector2(800, 300);
            numRt.anchoredPosition = Vector2.zero;

            // RK Studio branding (at the bottom center)
            GameObject brandingObj = new GameObject("BrandingText");
            brandingObj.transform.SetParent(transform, false);
            studioText = brandingObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset regularFont = Resources.Load<TMP_FontAsset>("Roboto_Condensed-Regular SDF");
            if (regularFont != null) studioText.font = regularFont;
            studioText.alignment = TextAlignmentOptions.Center;
            studioText.fontSize = 32;
            studioText.text = "RK Studio";
            studioText.color = HexColor("#475569"); // Soft gray-purple
            studioText.fontStyle = FontStyles.Bold;
            RectTransform brandingRt = studioText.rectTransform;
            brandingRt.anchorMin = new Vector2(0.5f, 0f); // anchor to bottom
            brandingRt.anchorMax = new Vector2(0.5f, 0f);
            brandingRt.sizeDelta = new Vector2(600, 80);
            brandingRt.anchoredPosition = new Vector2(0, 100); // 100 pixels offset from bottom
        }

        private void SpawnStars()
        {
            int numStars = 25;
            for (int i = 0; i < numStars; i++)
            {
                GameObject starObj = new GameObject("StarStar");
                starObj.transform.SetParent(starsContainer.transform, false);

                Image img = starObj.AddComponent<Image>();
                img.sprite = dotSprite;
                img.color = new Color(1f, 1f, 1f, 0f);

                RectTransform rt = img.rectTransform;
                // Random position inside reference resolution bounds (1080 x 1920)
                rt.anchoredPosition = new Vector2(
                    Random.Range(-540f, 540f),
                    Random.Range(-960f, 960f)
                );
                float size = Random.Range(4f, 12f);
                rt.sizeDelta = new Vector2(size, size);

                StarParticle s = new StarParticle
                {
                    rt = rt,
                    img = img,
                    speed = new Vector2(0f, Random.Range(10f, 35f)), // drift upwards
                    minAlpha = Random.Range(0.05f, 0.2f),
                    maxAlpha = Random.Range(0.4f, 0.95f),
                    fadeSpeed = Random.Range(0.4f, 1.5f),
                    timeOffset = Random.Range(0f, 100f)
                };
                stars.Add(s);
            }
        }

        private void UpdateStars()
        {
            for (int i = 0; i < stars.Count; i++)
            {
                StarParticle s = stars[i];
                // Move star upwards
                s.rt.anchoredPosition += s.speed * Time.deltaTime;

                // Twinkle (oscillate opacity)
                float noise = Mathf.PingPong((Time.time + s.timeOffset) * s.fadeSpeed, 1f);
                s.img.color = new Color(1f, 1f, 1f, Mathf.Lerp(s.minAlpha, s.maxAlpha, noise));

                // Wrap around bottom if it drifts off top
                if (s.rt.anchoredPosition.y > 980f)
                {
                    s.rt.anchoredPosition = new Vector2(
                        Random.Range(-540f, 540f),
                        -980f
                    );
                }
            }
        }

        private void SpawnMergeBurst(Vector2 centerPos, int count, Color burstColor)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject pObj = new GameObject("BurstParticle");
                pObj.transform.SetParent(transform, false);
                pObj.transform.SetSiblingIndex(starsContainer.transform.GetSiblingIndex() + 1);

                Image img = pObj.AddComponent<Image>();
                img.sprite = dotSprite;
                img.color = burstColor;

                RectTransform rt = img.rectTransform;
                rt.anchoredPosition = centerPos;
                float size = Random.Range(8f, 22f);
                rt.sizeDelta = new Vector2(size, size);

                // Explode in radial direction
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float force = Random.Range(150f, 600f);
                Vector2 vel = new Vector2(Mathf.Cos(angle) * force, Mathf.Sin(angle) * force);

                BurstParticle p = new BurstParticle
                {
                    rt = rt,
                    img = img,
                    velocity = vel,
                    life = 0f,
                    maxLife = Random.Range(0.4f, 0.8f)
                };
                bursts.Add(p);
            }
        }

        private void UpdateBursts()
        {
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                BurstParticle p = bursts[i];
                p.life += Time.deltaTime;

                // Move with air friction (decay velocity)
                p.rt.anchoredPosition += p.velocity * Time.deltaTime;
                p.velocity *= Mathf.Exp(-5f * Time.deltaTime); // air resistance

                float progress = p.life / p.maxLife;
                Color col = p.img.color;
                col.a = Mathf.Clamp01(1f - progress);
                p.img.color = col;

                // Shrink
                p.rt.localScale = Vector3.one * (1f - progress);

                if (p.life >= p.maxLife)
                {
                    Destroy(p.rt.gameObject);
                    bursts.RemoveAt(i);
                }
            }
        }

        private void SpawnRipple(Vector2 centerPos, float startS, float targetS, float duration, Color color)
        {
            GameObject rObj = new GameObject("RippleRing");
            rObj.transform.SetParent(transform, false);
            rObj.transform.SetSiblingIndex(starsContainer.transform.GetSiblingIndex() + 1);

            Image img = rObj.AddComponent<Image>();
            img.sprite = ringSprite;
            img.color = color;

            RectTransform rt = img.rectTransform;
            rt.anchoredPosition = centerPos;
            rt.sizeDelta = new Vector2(300, 300); // base size
            rt.localScale = Vector3.one * startS;

            RippleEffect r = new RippleEffect
            {
                rt = rt,
                img = img,
                life = 0f,
                maxLife = duration,
                startScale = startS,
                targetScale = targetS
            };
            ripples.Add(r);
        }

        private void UpdateRipples()
        {
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                RippleEffect r = ripples[i];
                r.life += Time.deltaTime;

                float progress = r.life / r.maxLife;
                float currentScale = Mathf.Lerp(r.startScale, r.targetScale, progress);
                r.rt.localScale = Vector3.one * currentScale;

                Color col = r.img.color;
                col.a = Mathf.Clamp01(1f - progress);
                r.img.color = col;

                if (r.life >= r.maxLife)
                {
                    Destroy(r.rt.gameObject);
                    ripples.RemoveAt(i);
                }
            }
        }

        private IEnumerator PlaySplashScreenTimeline()
        {
            // ───────────────────────────────────────────────────
            // 0.0s - 0.8s: Centered "2" scales up and fades in
            // ───────────────────────────────────────────────────
            centerNumberText.text = "2";
            Color neonViolet;
            ColorUtility.TryParseHtmlString("#ddb7ff", out neonViolet);
            centerNumberText.color = neonViolet;
            centerNumberText.transform.localScale = Vector3.zero;

            float elapsed = 0f;
            float duration = 0.4f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Easing out elastic-like or smooth rise
                float scale = Mathf.SmoothStep(0f, 1f, t);
                centerNumberText.transform.localScale = Vector3.one * scale;
                
                Color c = centerNumberText.color;
                c.a = t;
                centerNumberText.color = c;
                yield return null;
            }
            centerNumberText.transform.localScale = Vector3.one;

            yield return new WaitForSeconds(0.05f);

            // ───────────────────────────────────────────────────
            // Merges Timeline: 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048
            // Each merge takes about 0.35s total to keep the total timing around 5-6s.
            // ───────────────────────────────────────────────────
            int[] values = { 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048 };
            string[] hexColors = {
                "#ddb7ff", // 4 (Neon light purple)
                "#cf9eff", // 8
                "#bf82ff", // 16
                "#b066ff", // 32
                "#a14aff", // 64 (Deeper purple glow)
                "#b25fff", // 128
                "#c275ff", // 256
                "#d28aff", // 512
                "#e39fff", // 1024
                "#ffffff"  // 2048 (Blinding Neon White)
            };

            for (int m = 0; m < values.Length; m++)
            {
                int nextVal = values[m];
                string prevText = (nextVal / 2).ToString();
                string nextText = nextVal.ToString();

                Color targetColor;
                ColorUtility.TryParseHtmlString(hexColors[m], out targetColor);

                // Create falling number object
                GameObject fallObj = new GameObject("FallingNumber");
                fallObj.transform.SetParent(transform, false);
                fallObj.transform.SetSiblingIndex(centerNumberText.transform.GetSiblingIndex());

                TextMeshProUGUI fallText = fallObj.AddComponent<TextMeshProUGUI>();
                if (centerNumberText.font != null) fallText.font = centerNumberText.font;
                fallText.alignment = TextAlignmentOptions.Center;
                fallText.fontStyle = FontStyles.Bold;
                fallText.fontSize = centerNumberText.fontSize;
                fallText.text = prevText;
                
                Color colVal = targetColor;
                colVal.a = 0.8f;
                fallText.color = colVal;

                RectTransform fallRt = fallText.rectTransform;
                fallRt.sizeDelta = centerNumberText.rectTransform.sizeDelta;

                // Fall animation (0.0s to 0.14s)
                float fElapsed = 0f;
                float fDuration = 0.14f;
                Vector2 startPos = new Vector2(0, 800);
                Vector2 targetPos = Vector2.zero;

                while (fElapsed < fDuration)
                {
                    fElapsed += Time.deltaTime;
                    float progress = fElapsed / fDuration;
                    // cubic ease-in
                    float easeT = progress * progress * progress;
                    fallRt.anchoredPosition = Vector2.Lerp(startPos, targetPos, easeT);
                    
                    // Simple simulated vertical motion blur (stretch scale along Y, shrink along X)
                    float stretchY = Mathf.Lerp(1.5f, 1f, progress);
                    float shrinkX = Mathf.Lerp(0.7f, 1f, progress);
                    fallRt.localScale = new Vector3(shrinkX, stretchY, 1f);

                    yield return null;
                }

                // Impact!
                Destroy(fallObj);

                // Morph center text value and color
                centerNumberText.text = nextText;
                centerNumberText.color = targetColor;

                // Sfx play merge
                AudioManager.Instance?.PlayMerge(nextVal);

                // 1. Squash and stretch animation (0.10s)
                float sElapsed = 0f;
                float sDuration = 0.10f;
                while (sElapsed < sDuration)
                {
                    sElapsed += Time.deltaTime;
                    float t = sElapsed / sDuration;
                    // Sin-wave squash and bounce back
                    // On impact (t=0), squash: Y small, X wide.
                    float xMultiplier = 1f + 0.35f * Mathf.Sin(t * Mathf.PI) * (1f - t);
                    float yMultiplier = 1f - 0.25f * Mathf.Sin(t * Mathf.PI) * (1f - t);
                    centerNumberText.transform.localScale = new Vector3(xMultiplier, yMultiplier, 1f);
                    yield return null;
                }
                centerNumberText.transform.localScale = Vector3.one;

                // 2. Spawn star burst particles
                Color burstColor = targetColor;
                burstColor.a = 0.85f;
                SpawnMergeBurst(Vector2.zero, 12, burstColor);

                // 3. Spawn expanding light ring ripple
                Color ripColor = targetColor;
                ripColor.a = 0.65f;
                SpawnRipple(Vector2.zero, 0.2f, 2.5f, 0.20f, ripColor);

                // 4. Flare ambient glow
                StartCoroutine(PulseAmbientGlow(targetColor, m + 1));

                // Small rest spacing before next number falls
                yield return new WaitForSeconds(0.04f);
            }

            // ───────────────────────────────────────────────────
            // 2048 Formed! Final bloom and Circular wave reveal
            // ───────────────────────────────────────────────────
            Color finalGoldColor = Color.white; // Shiny white-purple
            SpawnRipple(Vector2.zero, 0.2f, 5f, 0.6f, finalGoldColor);
            SpawnMergeBurst(Vector2.zero, 30, finalGoldColor);

            // Punch scale of final logo
            StartCoroutine(PunchScaleFinal(1.5f, 0.25f));

            // Hold completed 2048 logo for 0.8 seconds
            yield return new WaitForSeconds(0.8f);

            // ───────────────────────────────────────────────────
            // Fadeout entire splash overlay over 0.4 seconds
            // ───────────────────────────────────────────────────
            float exitElapsed = 0f;
            float exitDuration = 0.4f;
            while (exitElapsed < exitDuration)
            {
                exitElapsed += Time.deltaTime;
                float t = exitElapsed / exitDuration;
                mainCanvasGroup.alpha = 1f - t;
                yield return null;
            }
            mainCanvasGroup.alpha = 0f;

            // Finish! Trigger gameplay reveal
            isSplashActive = false;
            if (onCompleteCallback != null)
            {
                onCompleteCallback.Invoke();
            }

            Destroy(gameObject);
        }

        private IEnumerator PulseAmbientGlow(Color targetColor, int mergeLevel)
        {
            Color initCol = centralGlowImage.color;
            Color flashCol = targetColor;
            flashCol.a = Mathf.Lerp(0.35f, 0.70f, mergeLevel / 10f);

            Vector2 baseSize = new Vector2(900, 900);
            Vector2 peakSize = baseSize * Mathf.Lerp(1.1f, 1.4f, mergeLevel / 10f);

            float elapsed = 0f;
            float duration = 0.15f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float curve = Mathf.Sin(t * Mathf.PI);

                centralGlowImage.color = Color.Lerp(initCol, flashCol, curve);
                centralGlowImage.rectTransform.sizeDelta = Vector2.Lerp(baseSize, peakSize, curve);
                yield return null;
            }

            // Settle ambient opacity higher as number progresses
            Color settleCol = targetColor;
            settleCol.a = Mathf.Lerp(0.18f, 0.38f, mergeLevel / 10f);
            centralGlowImage.color = settleCol;
            centralGlowImage.rectTransform.sizeDelta = baseSize * Mathf.Lerp(1f, 1.25f, mergeLevel / 10f);
        }

        private IEnumerator PunchScaleFinal(float factor, float duration)
        {
            Vector3 original = Vector3.one;
            Vector3 target = Vector3.one * factor;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float curve = Mathf.Sin(t * Mathf.PI);
                centerNumberText.transform.localScale = Vector3.Lerp(original, target, curve);
                yield return null;
            }
            centerNumberText.transform.localScale = original;
        }

        private void BuildLoadingScreenUI()
        {
            // Create container for loading bar elements with a CanvasGroup
            loadingBarContainer = new GameObject("LoadingBarContainer");
            loadingBarContainer.transform.SetParent(transform, false);
            CanvasGroup loaderCg = loadingBarContainer.AddComponent<CanvasGroup>();
            loaderCg.alpha = 0f; // start hidden for fade-in

            // Rounded Loading Bar Background (width 600, height 28)
            GameObject barBgObj = new GameObject("LoadingBarBackground");
            barBgObj.transform.SetParent(loadingBarContainer.transform, false);
            Image barBgImg = barBgObj.AddComponent<Image>();
            if (buttonSprite != null)
            {
                barBgImg.sprite = buttonSprite;
                barBgImg.type = Image.Type.Sliced;
            }
            barBgImg.color = HexColor("#171f33"); // Dark slate background
            RectTransform bgRt = barBgImg.rectTransform;
            bgRt.sizeDelta = new Vector2(600, 28);
            bgRt.anchoredPosition = new Vector2(0, -60); // beneath centerNumberText (which moved to 150, so spacing is fine!)

            // Loading Bar Mask container (inside the background)
            GameObject maskObj = new GameObject("LoadingBarMask");
            maskObj.transform.SetParent(barBgObj.transform, false);
            Mask mask = maskObj.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            Image maskImg = maskObj.AddComponent<Image>();
            if (buttonSprite != null)
            {
                maskImg.sprite = buttonSprite;
                maskImg.type = Image.Type.Sliced;
            }
            RectTransform maskRt = maskImg.rectTransform;
            maskRt.anchorMin = Vector2.zero;
            maskRt.anchorMax = Vector2.one;
            maskRt.offsetMin = new Vector2(4, 4); // inset padding
            maskRt.offsetMax = new Vector2(-4, -4);

            // Loading Bar Fill
            GameObject fillObj = new GameObject("LoadingBarFill");
            fillObj.transform.SetParent(maskObj.transform, false);
            loadingBarFill = fillObj.AddComponent<Image>();
            if (buttonSprite != null)
            {
                loadingBarFill.sprite = buttonSprite;
                loadingBarFill.type = Image.Type.Sliced;
            }
            loadingBarFill.color = HexColor("#b76dff"); // Neon purple fill
            RectTransform fillRt = loadingBarFill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f); // starts empty
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            // LOADING ASSETS... Text
            GameObject txtObj = new GameObject("LoadingText");
            txtObj.transform.SetParent(loadingBarContainer.transform, false);
            loadingText = txtObj.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset regularFont = Resources.Load<TMP_FontAsset>("Roboto_Condensed-Regular SDF");
            if (regularFont != null) loadingText.font = regularFont;
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingText.fontSize = 24;
            loadingText.text = "LOADING ASSETS...";
            loadingText.color = HexColor("#94a3b8"); // Gray-purple
            loadingText.fontStyle = FontStyles.Bold;
            RectTransform txtRt = loadingText.rectTransform;
            txtRt.sizeDelta = new Vector2(600, 60);
            txtRt.anchoredPosition = new Vector2(0, -120);

            // RK Studio branding (at the bottom)
            GameObject brandingObj = new GameObject("BrandingText");
            brandingObj.transform.SetParent(transform, false);
            studioText = brandingObj.AddComponent<TextMeshProUGUI>();
            if (regularFont != null) studioText.font = regularFont;
            studioText.alignment = TextAlignmentOptions.Center;
            studioText.fontSize = 32;
            studioText.text = "RK Studio";
            studioText.color = HexColor("#475569"); // Soft gray-purple
            studioText.fontStyle = FontStyles.Bold;
            CanvasGroup brandingCg = brandingObj.AddComponent<CanvasGroup>();
            brandingCg.alpha = 0f;
            RectTransform brandingRt = studioText.rectTransform;
            brandingRt.anchorMin = new Vector2(0.5f, 0f); // anchor to bottom
            brandingRt.anchorMax = new Vector2(0.5f, 0f);
            brandingRt.sizeDelta = new Vector2(600, 80);
            brandingRt.anchoredPosition = new Vector2(0, 100); // 100 pixels offset from bottom
        }

        private static Color HexColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
