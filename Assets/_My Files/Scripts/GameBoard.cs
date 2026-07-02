using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace SlideAndMatch
{
    /// <summary>
    /// Manages the visual 4×4 grid: background cells, tile spawning/destruction,
    /// and animation orchestration.  Also auto-configures the camera at startup.
    /// Works with or without a tile prefab (creates tiles from code as fallback).
    /// </summary>
    public class GameBoard : MonoBehaviour
    {
        // ── Inspector (all optional — auto-created if null) ───
        [Header("Prefab (optional — auto-creates if null)")]
        [SerializeField] private GameObject tilePrefab;

        [Header("Grid Dimensions")]
        [SerializeField] private float cellSize    = 1.1f;
        [SerializeField] private float cellSpacing = 0.1f;

        [Header("Colours")]
        [SerializeField] private Color boardColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        [SerializeField] private Color cellColor  = new Color(0.15f, 0.16f, 0.19f, 1f);

        // ── Runtime state ─────────────────────────────────────
        private Tile[,] tileGrid = new Tile[4, 4];
        private float totalCellSize;
        private Vector3 boardOrigin;
        private Vector3 originalBoardPos;

        public bool IsAnimating { get; private set; }

        private Coroutine boardShakeCoroutine;

        // ───────────────────────────────────────────────────────
        // Lifecycle
        // ───────────────────────────────────────────────────────
        void Start()
        {
            originalBoardPos = transform.position;
            totalCellSize = cellSize + cellSpacing;
            float halfBoard = (totalCellSize * 4f - cellSpacing) / 2f;
            boardOrigin = new Vector3(
                -halfBoard + cellSize / 2f,
                -halfBoard + cellSize / 2f,
                0f);

            AutoConfigureCamera();
            CreateBackground();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted       += HandleGameStarted;
                GameManager.Instance.OnBoardRefreshNeeded += HandleBoardRefresh;

                // Delay one frame so UIManager.Start can subscribe first
                StartCoroutine(DelayedStart());
            }
            else
            {
                Debug.LogError("[GameBoard] GameManager.Instance is null — " +
                               "make sure a GameManager exists in the scene.");
            }
        }

        private IEnumerator DelayedStart()
        {
            yield return null;   // wait one frame
            GameManager.Instance.StartNewGame();
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStarted       -= HandleGameStarted;
                GameManager.Instance.OnBoardRefreshNeeded -= HandleBoardRefresh;
            }
        }

        // ───────────────────────────────────────────────────────
        // Event handlers
        // ───────────────────────────────────────────────────────

        private void HandleGameStarted()
        {
            StopAllCoroutines();
            IsAnimating = false;
            ClearAllTiles();
            SpawnRandomTile();
            SpawnRandomTile();
        }

        private void HandleBoardRefresh()
        {
            StopAllCoroutines();
            IsAnimating = false;
            ClearAllTiles();
            RefreshFromGrid();
        }

        private void RefreshFromGrid()
        {
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                {
                    int val = GameManager.Instance.GetCell(x, y);
                    if (val != 0)
                        SpawnTileVisual(new Vector2Int(x, y), val, false);
                }
        }

        // ───────────────────────────────────────────────────────
        // Camera
        // ───────────────────────────────────────────────────────

        private void AutoConfigureCamera()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            cam.orthographic     = true;
            cam.orthographicSize = 5.5f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = new Color(0.04f, 0.04f, 0.06f, 1f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        // ───────────────────────────────────────────────────────
        // Background
        // ───────────────────────────────────────────────────────

        private void CreateBackground()
        {
            // Board backdrop (slightly larger than the grid)
            float boardSize = totalCellSize * 4f - cellSpacing + 0.4f;

            GameObject boardBg = new GameObject("BoardBackground");
            boardBg.transform.SetParent(transform, false);
            boardBg.transform.localPosition = Vector3.zero;

            var boardSr = boardBg.AddComponent<SpriteRenderer>();
            boardSr.sprite       = CreateSquareSprite();
            boardSr.color        = boardColor;
            boardSr.sortingOrder = -2;
            boardBg.transform.localScale = new Vector3(boardSize, boardSize, 1f);

            // Individual cell backgrounds
            for (int x = 0; x < 4; x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    Vector3 pos = GridToWorldPosition(new Vector2Int(x, y));

                    GameObject cell = new GameObject($"Cell_{x}_{y}");
                    cell.transform.SetParent(transform, false);
                    cell.transform.position = pos;

                    var sr = cell.AddComponent<SpriteRenderer>();
                    sr.sprite       = CreateSquareSprite();
                    sr.color        = cellColor;
                    sr.sortingOrder = -1;
                    cell.transform.localScale = new Vector3(cellSize, cellSize, 1f);
                }
            }
        }

        // ───────────────────────────────────────────────────────
        // Coordinate conversion
        // ───────────────────────────────────────────────────────

        public Vector3 GridToWorldPosition(Vector2Int gridPos)
        {
            return transform.position + boardOrigin + new Vector3(
                gridPos.x * totalCellSize,
                gridPos.y * totalCellSize,
                0f);
        }

        // ───────────────────────────────────────────────────────
        // Input entry point (called by InputManager)
        // ───────────────────────────────────────────────────────

        public void HandleSwipe(Direction dir)
        {
            if (IsAnimating) return;

            MoveResult result = GameManager.Instance.Move(dir);
            if (!result.isValid) return;

            StartCoroutine(AnimateMove(result, dir));
        }

        // ───────────────────────────────────────────────────────
        // Animation orchestration
        // ───────────────────────────────────────────────────────

        private struct MergeJob
        {
            public Tile survivor;
            public Tile consumed;
            public Vector2Int mergeTo;
            public Vector2Int tile1From;
            public Vector2Int tile2From;
            public int newValue;
        }

        private struct MoveJob
        {
            public Tile tile;
            public Vector2Int from;
            public Vector2Int to;
        }

        private IEnumerator AnimateMove(MoveResult result, Direction swipeDir)
        {
            IsAnimating = true;

            // ── 1. Gather all moving and merging tiles from the original tileGrid first ──
            List<MergeJob> mergeJobs = new List<MergeJob>();
            foreach (var merge in result.merges)
            {
                Tile survivor = tileGrid[merge.tile1From.x, merge.tile1From.y];
                Tile consumed = tileGrid[merge.tile2From.x, merge.tile2From.y];
                if (survivor != null && consumed != null)
                {
                    mergeJobs.Add(new MergeJob
                    {
                        survivor = survivor,
                        consumed = consumed,
                        mergeTo = merge.mergeTo,
                        tile1From = merge.tile1From,
                        tile2From = merge.tile2From,
                        newValue = merge.newValue
                    });
                }
            }

            List<MoveJob> moveJobs = new List<MoveJob>();
            foreach (var move in result.moves)
            {
                Tile tile = tileGrid[move.from.x, move.from.y];
                if (tile != null)
                {
                    moveJobs.Add(new MoveJob
                    {
                        tile = tile,
                        from = move.from,
                        to = move.to
                    });
                }
            }

            // ── 2. Update the tileGrid reference array mapping ──
            Tile[,] nextTileGrid = (Tile[,])tileGrid.Clone();

            // Clear old positions
            foreach (var job in mergeJobs)
            {
                nextTileGrid[job.tile1From.x, job.tile1From.y] = null;
                nextTileGrid[job.tile2From.x, job.tile2From.y] = null;
            }
            foreach (var job in moveJobs)
            {
                nextTileGrid[job.from.x, job.from.y] = null;
            }

            // Assign new positions
            foreach (var job in mergeJobs)
            {
                nextTileGrid[job.mergeTo.x, job.mergeTo.y] = job.survivor;
                job.survivor.GridPosition = job.mergeTo;
            }
            foreach (var job in moveJobs)
            {
                nextTileGrid[job.to.x, job.to.y] = job.tile;
                job.tile.GridPosition = job.to;
            }

            tileGrid = nextTileGrid;

            // ── 3. Start Lerp animations ──
            List<Tile> tilesToDestroy = new List<Tile>();
            List<KeyValuePair<Tile, int>> tilesToUpdate = new List<KeyValuePair<Tile, int>>();

            int animsRemaining = 0;
            bool allDone = false;

            System.Action onAnimDone = () =>
            {
                animsRemaining--;
                if (animsRemaining <= 0) allDone = true;
            };

            // Animate Merges
            foreach (var job in mergeJobs)
            {
                Vector3 targetPos = GridToWorldPosition(job.mergeTo);

                // survivor moves to targetPos
                animsRemaining++;
                job.survivor.AnimateMove(targetPos, onAnimDone);

                // consumed moves to targetPos and is destroyed
                animsRemaining++;
                job.consumed.AnimateMove(targetPos, onAnimDone);

                tilesToDestroy.Add(job.consumed);
                tilesToUpdate.Add(new KeyValuePair<Tile, int>(job.survivor, job.newValue));
            }

            // Animate Simple Moves
            foreach (var job in moveJobs)
            {
                animsRemaining++;
                job.tile.AnimateMove(GridToWorldPosition(job.to), onAnimDone);
            }

            // ── 4. Wait for all slides to finish ───────────────
            if (animsRemaining > 0)
            {
                while (!allDone) yield return null;
            }

            // Trigger board shake on impact
            Vector3 shakeDir = Vector3.zero;
            switch (swipeDir)
            {
                case Direction.Left:  shakeDir = Vector3.left; break;
                case Direction.Right: shakeDir = Vector3.right; break;
                case Direction.Up:    shakeDir = Vector3.up; break;
                case Direction.Down:  shakeDir = Vector3.down; break;
            }
            boardShakeCoroutine = StartCoroutine(ShakeBoard(shakeDir));

            // ── 5. Merge cleanup (destroy consumed, pop survivor) ─
            foreach (var t in tilesToDestroy)
            {
                if (t != null) Destroy(t.gameObject);
            }

            foreach (var kv in tilesToUpdate)
            {
                if (kv.Key != null)
                {
                    kv.Key.Value = kv.Value;
                    kv.Key.AnimateMerge();
                    SpawnWorldFloatingText(kv.Key.transform.position, kv.Value);
                }
            }

            // ── 6. Sound ───────────────────────────────────────
            if (result.merges.Count > 0)
                AudioManager.Instance?.PlayMerge();
            else
                AudioManager.Instance?.PlaySlide();

            // ── 7. Spawn new tile ──────────────────────────────
            yield return new WaitForSeconds(0.05f);
            SpawnRandomTile();

            // ── 8. Check game over ─────────────────────────────
            if (GameManager.Instance.IsGameOver())
            {
                yield return new WaitForSeconds(0.5f);
                GameManager.Instance.NotifyGameOver();
            }

            IsAnimating = false;
        }

        // ───────────────────────────────────────────────────────
        // Tile spawning
        // ───────────────────────────────────────────────────────

        private void SpawnRandomTile()
        {
            Vector2Int? pos = GameManager.Instance.FindRandomEmptyCell();
            if (pos == null) return;

            int value = GameManager.Instance.GetRandomSpawnValue();
            GameManager.Instance.SetCell(pos.Value.x, pos.Value.y, value);
            SpawnTileVisual(pos.Value, value, true);
        }

        private void SpawnTileVisual(Vector2Int gridPos, int value, bool animate)
        {
            Vector3 worldPos = GridToWorldPosition(gridPos);
            GameObject tileObj;
            Tile tile;

            if (tilePrefab != null)
            {
                // ── Prefab path ──
                tileObj = Instantiate(tilePrefab, worldPos, Quaternion.identity, transform);
                tile = tileObj.GetComponent<Tile>();
            }
            else
            {
                // ── Auto-create from code ──
                tileObj = CreateTileFromCode(worldPos);
                tile = tileObj.GetComponent<Tile>();
            }

            tileObj.name = $"Tile_{gridPos.x}_{gridPos.y}";
            tile.GridPosition = gridPos;
            tile.SetPositionImmediate(worldPos);
            tile.Value = value;

            if (animate) tile.AnimateSpawn();

            tileGrid[gridPos.x, gridPos.y] = tile;
        }

        /// <summary>
        /// Builds a tile entirely from code — SpriteRenderer + TextMeshPro child.
        /// Used when no tile prefab is assigned.
        /// </summary>
        private GameObject CreateTileFromCode(Vector3 position)
        {
            float tileSize = cellSize * 0.92f;

            GameObject tileObj = new GameObject("Tile");
            tileObj.transform.SetParent(transform, false);
            tileObj.transform.position   = position;
            tileObj.transform.localScale = new Vector3(tileSize, tileSize, 1f);

            // Background sprite
            var sr = tileObj.AddComponent<SpriteRenderer>();
            sr.sprite       = CreateSquareSprite();
            sr.sortingOrder = 1;

            // Text label (child)
            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(tileObj.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            textObj.transform.localScale    = Vector3.one;

            var tmp = textObj.AddComponent<TextMeshPro>();
            tmp.alignment       = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin     = 1f;
            tmp.fontSizeMax     = 8f;
            tmp.fontStyle       = FontStyles.Bold;
            tmp.sortingOrder    = 2;
            tmp.rectTransform.sizeDelta = new Vector2(0.85f, 0.85f);

            // Tile component — initialised from code
            var tile = tileObj.AddComponent<Tile>();
            tile.Initialize(sr, tmp, tileObj.transform.localScale);

            return tileObj;
        }

        // ───────────────────────────────────────────────────────
        // Board Shake Animation
        // ───────────────────────────────────────────────────────

        private IEnumerator ShakeBoard(Vector3 direction)
        {
            float duration = 0.15f;
            float elapsed = 0f;
            float maxOffset = 0.08f; // small, subtle offset

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;
                float decay = 1f - percent;

                // Sin wave oscillation (rebounding shake)
                float offsetAmount = Mathf.Sin(percent * Mathf.PI * 4f) * maxOffset * decay;
                transform.position = originalBoardPos + direction * offsetAmount;

                yield return null;
            }

            transform.position = originalBoardPos;
            boardShakeCoroutine = null;
        }

        // ───────────────────────────────────────────────────────
        // Floating Text Animation
        // ───────────────────────────────────────────────────────

        private void SpawnWorldFloatingText(Vector3 position, int value)
        {
            GameObject go = new GameObject("FloatingText_" + value);
            go.transform.position = position + Vector3.up * 0.3f; // Start slightly above the tile
            
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "+" + value;
            tmp.fontSize = 5f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.sortingOrder = 5; // Render in front of tiles
            
            // Text color matches the style of the merged tile color
            tmp.color = TileStyleLookup.GetTileColor(value);
            
            StartCoroutine(AnimateWorldFloatingText(go, tmp));
        }

        private IEnumerator AnimateWorldFloatingText(GameObject go, TextMeshPro tmp)
        {
            float duration = 0.6f;
            float elapsed = 0f;
            Vector3 startPos = go.transform.position;
            Vector3 endPos = startPos + Vector3.up * 0.8f;
            Color startColor = tmp.color;
            Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // Ease out cubic path
                float ease = 1f - Mathf.Pow(1f - t, 3f);
                go.transform.position = Vector3.Lerp(startPos, endPos, ease);
                tmp.color = Color.Lerp(startColor, endColor, t);

                yield return null;
            }

            Destroy(go);
        }

        // ───────────────────────────────────────────────────────
        // Cleanup
        // ───────────────────────────────────────────────────────

        private void ClearAllTiles()
        {
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                {
                    if (tileGrid[x, y] != null)
                    {
                        Destroy(tileGrid[x, y].gameObject);
                        tileGrid[x, y] = null;
                    }
                }
        }

        // ───────────────────────────────────────────────────────
        // Sprite utility (shared across board)
        // ───────────────────────────────────────────────────────

        private static Sprite cachedSquare;

        /// <summary>Creates a tiny white texture and wraps it as a Sprite (cached).</summary>
        public static Sprite CreateSquareSprite()
        {
            if (cachedSquare != null) return cachedSquare;

            Texture2D tex = new Texture2D(4, 4);
            Color[] px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Point;

            cachedSquare = Sprite.Create(
                tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            return cachedSquare;
        }
    }
}
