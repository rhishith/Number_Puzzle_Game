using System.Collections.Generic;
using UnityEngine;

namespace SlideAndMatch
{
    public enum Direction { Up, Down, Left, Right }

    /// <summary>
    /// Describes a tile that slid from one cell to another (no merge).
    /// </summary>
    [System.Serializable]
    public struct TileMoveInfo
    {
        public Vector2Int from;
        public Vector2Int to;
    }

    /// <summary>
    /// Describes two tiles that merged into one cell.
    /// tile1 is the "survivor" (value doubles), tile2 is consumed and destroyed.
    /// </summary>
    [System.Serializable]
    public struct TileMergeInfo
    {
        public Vector2Int tile1From;
        public Vector2Int tile2From;
        public Vector2Int mergeTo;
        public int newValue;
    }

    /// <summary>
    /// Everything GameBoard needs to animate a single move.
    /// </summary>
    public class MoveResult
    {
        public bool isValid;
        public int scoreGained;
        public List<TileMoveInfo> moves = new List<TileMoveInfo>();
        public List<TileMergeInfo> merges = new List<TileMergeInfo>();
    }

    /// <summary>
    /// Singleton that owns the int[4,4] grid and all 2048 game rules.
    /// Pure data — no visual/audio concerns.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        // ── Grid ──────────────────────────────────────────────
        private int[,] grid = new int[4, 4];

        private struct UndoState
        {
            public int[,] grid;
            public int score;
        }
        [Header("Undo Settings")]
        public bool allowMultipleUndos = true;

        private Stack<UndoState> undoHistory = new Stack<UndoState>();

        // ── Score ─────────────────────────────────────────────
        public int Score { get; private set; }
        public int BestScore { get; private set; }

        // ── Events (public delegates, not C# events) ─────────
        public System.Action<int, int> OnScoreChanged;   // (score, bestScore)
        public System.Action OnGameOver;
        public System.Action OnGameWon;
        public System.Action OnGameStarted;               // new game
        public System.Action OnBoardRefreshNeeded;         // undo → rebuild visuals

        // ── State ─────────────────────────────────────────────
        private bool hasWon;
        private bool keepPlaying;

        // ───────────────────────────────────────────────────────
        // Lifecycle
        // ───────────────────────────────────────────────────────
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BestScore = PlayerPrefs.GetInt("BestScore2048", 0);
        }

        // ───────────────────────────────────────────────────────
        // Public API
        // ───────────────────────────────────────────────────────

        /// <summary>Reset everything and fire OnGameStarted so GameBoard spawns 2 tiles.</summary>
        public void StartNewGame()
        {
            grid = new int[4, 4];
            Score = 0;
            hasWon = false;
            keepPlaying = false;
            undoHistory.Clear();
            OnScoreChanged?.Invoke(Score, BestScore);
            OnGameStarted?.Invoke();
        }

        public void ResetBestScore()
        {
            BestScore = 0;
            PlayerPrefs.SetInt("BestScore2048", 0);
            PlayerPrefs.Save();
            OnScoreChanged?.Invoke(Score, BestScore);
        }

        public int GetCell(int x, int y) => grid[x, y];
        public void SetCell(int x, int y, int value) => grid[x, y] = value;

        /// <summary>Returns a random empty cell, or null if the board is full.</summary>
        public Vector2Int? FindRandomEmptyCell()
        {
            List<Vector2Int> empty = new List<Vector2Int>();
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    if (grid[x, y] == 0)
                        empty.Add(new Vector2Int(x, y));

            if (empty.Count == 0) return null;
            return empty[Random.Range(0, empty.Count)];
        }

        /// <summary>90 % chance of 2, 10 % chance of 4.</summary>
        public int GetRandomSpawnValue() => Random.value < 0.9f ? 2 : 4;

        // ───────────────────────────────────────────────────────
        // Core move
        // ───────────────────────────────────────────────────────

        /// <summary>
        /// Execute a slide in the given direction.
        /// Returns a MoveResult that GameBoard uses for animation.
        /// </summary>
        public MoveResult Move(Direction dir)
        {
            MoveResult result = new MoveResult();
            SaveState();

            int[,] oldGrid = (int[,])grid.Clone();
            int scoreGained = 0;

            for (int lineIdx = 0; lineIdx < 4; lineIdx++)
            {
                // Extract one row / column as a 4-element line
                int[] lineValues = new int[4];
                Vector2Int[] lineCoords = new Vector2Int[4];

                for (int pos = 0; pos < 4; pos++)
                {
                    Vector2Int coord = GetCoordinate(lineIdx, pos, dir);
                    lineValues[pos] = grid[coord.x, coord.y];
                    lineCoords[pos] = coord;
                }

                // Slide the line toward index 0
                int lineScore;
                List<TileMoveInfo> lineMoves;
                List<TileMergeInfo> lineMerges;
                int[] newLine = SlideLine(lineValues, lineCoords,
                                          out lineMoves, out lineMerges, out lineScore);

                // Write back
                for (int pos = 0; pos < 4; pos++)
                    grid[lineCoords[pos].x, lineCoords[pos].y] = newLine[pos];

                result.moves.AddRange(lineMoves);
                result.merges.AddRange(lineMerges);
                scoreGained += lineScore;
            }

            bool moved = !GridsEqual(oldGrid, grid);
            result.isValid = moved;
            result.scoreGained = scoreGained;

            if (moved)
            {
                Score += scoreGained;
                if (Score > BestScore)
                {
                    BestScore = Score;
                    PlayerPrefs.SetInt("BestScore2048", BestScore);
                    PlayerPrefs.Save();
                }
                OnScoreChanged?.Invoke(Score, BestScore);

                if (!hasWon && !keepPlaying && HasValue(2048))
                {
                    hasWon = true;
                    OnGameWon?.Invoke();
                }
            }
            else
            {
                // Nothing changed — roll back so undo state stays clean
                RestoreState();
            }

            return result;
        }

        // ───────────────────────────────────────────────────────
        // Game-over / win
        // ───────────────────────────────────────────────────────

        /// <summary>True when no empty cells AND no adjacent equal pairs remain.</summary>
        public bool IsGameOver()
        {
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    if (grid[x, y] == 0) return false;

            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                {
                    int val = grid[x, y];
                    if (x + 1 < 4 && grid[x + 1, y] == val) return false;
                    if (y + 1 < 4 && grid[x, y + 1] == val) return false;
                }

            return true;
        }

        /// <summary>Called by GameBoard after spawning a tile, so the event fires from the right context.</summary>
        public void NotifyGameOver()
        {
            OnGameOver?.Invoke();
        }

        /// <summary>Let the player keep going after reaching 2048.</summary>
        public void ContinuePlaying() => keepPlaying = true;

        // ───────────────────────────────────────────────────────
        // Undo
        // ───────────────────────────────────────────────────────

        public bool CanUndo => undoHistory.Count > 0;

        public void Undo()
        {
            if (undoHistory.Count == 0) return;

            UndoState state = undoHistory.Pop();
            grid = state.grid;
            Score = state.score;

            if (!allowMultipleUndos)
            {
                undoHistory.Clear();
            }

            OnScoreChanged?.Invoke(Score, BestScore);
            OnBoardRefreshNeeded?.Invoke();
        }

        // ───────────────────────────────────────────────────────
        // Internals
        // ───────────────────────────────────────────────────────

        private void SaveState()
        {
            int[,] gridCopy = new int[4, 4];
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    gridCopy[x, y] = grid[x, y];

            if (!allowMultipleUndos)
            {
                undoHistory.Clear();
            }

            undoHistory.Push(new UndoState { grid = gridCopy, score = Score });
        }

        private void RestoreState()
        {
            if (undoHistory.Count > 0)
            {
                UndoState state = undoHistory.Pop();
                grid = state.grid;
                Score = state.score;
            }
        }

        /// <summary>
        /// Maps (lineIndex, positionInLine, direction) → grid [x, y].
        /// The line is always processed toward index 0, so the direction
        /// determines which end of the row/column is "index 0".
        /// </summary>
        private Vector2Int GetCoordinate(int lineIndex, int pos, Direction dir)
        {
            switch (dir)
            {
                case Direction.Left:  return new Vector2Int(pos, lineIndex);
                case Direction.Right: return new Vector2Int(3 - pos, lineIndex);
                case Direction.Up:    return new Vector2Int(lineIndex, 3 - pos);
                case Direction.Down:  return new Vector2Int(lineIndex, pos);
                default:              return Vector2Int.zero;
            }
        }

        /// <summary>
        /// Slide a 4-element line toward index 0 (compact → merge → compact).
        /// Produces move and merge records for GameBoard animation.
        /// </summary>
        private int[] SlideLine(int[] values, Vector2Int[] coords,
            out List<TileMoveInfo> moves, out List<TileMergeInfo> merges, out int score)
        {
            int[] result = new int[4];
            moves = new List<TileMoveInfo>();
            merges = new List<TileMergeInfo>();
            score = 0;

            // 1. Collect non-empty cells with their original line-indices
            List<int> nzValues = new List<int>();
            List<int> nzIndices = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                if (values[i] != 0)
                {
                    nzValues.Add(values[i]);
                    nzIndices.Add(i);
                }
            }

            // 2. Walk through, merging adjacent equal pairs (each tile merges at most once)
            int writePos = 0;
            int readIdx = 0;

            while (readIdx < nzValues.Count)
            {
                if (readIdx + 1 < nzValues.Count && nzValues[readIdx] == nzValues[readIdx + 1])
                {
                    // ── Merge ──
                    int mergedValue = nzValues[readIdx] * 2;
                    result[writePos] = mergedValue;
                    score += mergedValue;

                    merges.Add(new TileMergeInfo
                    {
                        tile1From = coords[nzIndices[readIdx]],
                        tile2From = coords[nzIndices[readIdx + 1]],
                        mergeTo   = coords[writePos],
                        newValue  = mergedValue
                    });

                    readIdx += 2;
                }
                else
                {
                    // ── Simple slide ──
                    result[writePos] = nzValues[readIdx];

                    Vector2Int from = coords[nzIndices[readIdx]];
                    Vector2Int to   = coords[writePos];

                    if (from != to)
                    {
                        moves.Add(new TileMoveInfo { from = from, to = to });
                    }

                    readIdx++;
                }
                writePos++;
            }

            return result;
        }

        private bool GridsEqual(int[,] a, int[,] b)
        {
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    if (a[x, y] != b[x, y]) return false;
            return true;
        }

        private bool HasValue(int value)
        {
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    if (grid[x, y] == value) return true;
            return false;
        }
    }
}
