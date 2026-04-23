using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enemy AI for the 2D grid-based stealth echo game.
///
/// Responsibilities (AI logic only):
///   - Read echo data from SoundSystem (no duplicate echo storage)
///   - Predict player position from recent echo history
///   - Uncertainty behavior (80/20 predicted vs. random investigation)
///   - Learn player movement habits over time
///   - Move toward the chosen target each tick (wall-aware)
///
/// This class does NOT own or manage echo lifetimes — SoundSystem does that.
/// Attach to a GameObject with a visible sprite to represent the enemy.
/// </summary>
public class EnemyAI : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Configurable parameters (exposed in Inspector)
    // ─────────────────────────────────────────────

    [Header("AI Settings")]
    [Tooltip("Number of recent echoes used for movement-delta prediction")]
    public int PredictCount = 4;

    [Tooltip("Probability (0–1) that the AI uses prediction vs. random investigation")]
    [Range(0f, 1f)]
    public float PredictionAccuracy = 0.8f;

    [Tooltip("Grid cells per tick the enemy can move")]
    public int MoveSpeed = 1;

    // ─────────────────────────────────────────────
    // References
    // ─────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private SoundSystem soundSystem;

    // ─────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────

    /// <summary>Current enemy grid position (array indices, not world).</summary>
    public int GridX { get; private set; }
    public int GridY { get; private set; }

    /// <summary>The grid position the enemy is currently trying to reach.</summary>
    public (int x, int y) PredictedTarget { get; private set; }

    /// <summary>
    /// Temporal echo history — ordered ring buffer of recent echo positions
    /// for computing movement deltas. SoundSystem's grid doesn't preserve
    /// temporal ordering, so the AI keeps its own small history.
    /// </summary>
    private readonly List<(int x, int y)> echoHistory = new();
    private const int MaxHistory = 20;

    /// <summary>
    /// Habit-learning: counts how often each cardinal direction appears
    /// in the player's movement patterns.
    /// </summary>
    private readonly Dictionary<string, int> directionFrequencies = new()
    {
        { "Up",    0 },
        { "Down",  0 },
        { "Left",  0 },
        { "Right", 0 }
    };

    private System.Random rng = new();

    // ─────────────────────────────────────────────
    // A* pathfinding state
    // ─────────────────────────────────────────────

    /// <summary>Cached A* path (grid positions from current toward target).</summary>
    private List<Vector2Int> currentPath = new();

    /// <summary>Index into currentPath for the next step to take.</summary>
    private int pathIndex = 0;

    /// <summary>Last known echo position — used when grid goes silent.</summary>
    private (int x, int y)? lastKnownPosition = null;

    // ─────────────────────────────────────────────
    // Initialization
    // ─────────────────────────────────────────────

    void Start()
    {
        if (soundSystem == null)
            soundSystem = FindObjectOfType<SoundSystem>();

        if (soundSystem == null)
        {
            Debug.LogError("[EnemyAI] No SoundSystem found! Add one to the scene.");
            return;
        }

        // Snap to nearest tile center using coordinate conversion
        var (gx, gy) = soundSystem.WorldToGrid(transform.position);
        GridX = gx;
        GridY = gy;

        // Make sure the enemy starts on a walkable cell
        if (soundSystem.IsWallGrid(GridX, GridY))
        {
            Debug.LogWarning($"[EnemyAI] Starting position ({GridX},{GridY}) is a wall! Finding nearest floor...");
            FindNearestFloor();
        }

        // Sync visual to grid
        transform.position = soundSystem.GridToWorld(GridX, GridY);
        PredictedTarget = (GridX, GridY);

        // Disable physics interference if Rigidbody2D exists
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log($"[EnemyAI] Initialized at grid ({GridX},{GridY}), world {transform.position}");
    }

    /// <summary>Spiral search for nearest walkable cell from current GridX/GridY.</summary>
    private void FindNearestFloor()
    {
        for (int r = 1; r < Mathf.Max(soundSystem.GetGridWidth(), soundSystem.GetGridHeight()); r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // only ring
                    int nx = GridX + dx, ny = GridY + dy;
                    if (soundSystem.IsFloor(nx, ny))
                    {
                        GridX = nx;
                        GridY = ny;
                        return;
                    }
                }
            }
        }
    }

    // ─────────────────────────────────────────────
    // Main tick — called by GameManager each game turn
    // ─────────────────────────────────────────────

    /// <summary>
    /// One AI turn: observe echoes from SoundSystem, update history,
    /// decide a target, move toward it, sync visual position.
    /// </summary>
    public void Tick()
    {
        if (soundSystem == null) return;

        // Read config from GameController if available
        SyncConfigFromController();

        // 1. Observe — read active echoes from SoundSystem
        var activeEchoes = soundSystem.GetActiveEchoes();

        // Grid is completely silent — use last-known-position fallback
        if (activeEchoes.Count == 0)
        {
            if (lastKnownPosition.HasValue)
            {
                // Move toward the last known echo position
                PredictedTarget = lastKnownPosition.Value;
                ExecuteMovement(PredictedTarget.x, PredictedTarget.y);
                transform.position = soundSystem.GridToWorld(GridX, GridY);

                // Clear once we arrive
                if (GridX == lastKnownPosition.Value.x && GridY == lastKnownPosition.Value.y)
                    lastKnownPosition = null;
            }
            return;
        }

        // 2. Update temporal history with the loudest echo (likely the newest)
        var loudest = (activeEchoes[0].x, activeEchoes[0].y);
        lastKnownPosition = loudest; // Track last known position

        // Only add if it differs from the last recorded position
        bool habitEnabled = GameController.Instance != null
            ? GameController.Instance.enableHabitLearning
            : true;

        if (echoHistory.Count == 0 || echoHistory[^1] != loudest)
        {
            // --- Habit Learning ---
            if (habitEnabled && echoHistory.Count > 0)
            {
                var prev = echoHistory[^1];
                int dx = loudest.x - prev.x;
                int dy = loudest.y - prev.y;
                string dir = ClassifyDirection(dx, dy);
                if (dir != null)
                    directionFrequencies[dir]++;
            }

            echoHistory.Add(loudest);

            if (echoHistory.Count > MaxHistory)
                echoHistory.RemoveAt(0);
        }

        // 3. Decide target (mode-aware)
        DecideTarget(activeEchoes);

        // 4. Move toward the target (A*-aware)
        ExecuteMovement(PredictedTarget.x, PredictedTarget.y);

        // 5. Sync visual position to grid position
        transform.position = soundSystem.GridToWorld(GridX, GridY);
    }

    /// <summary>Pull config values from GameController so Inspector changes apply in real-time.</summary>
    private void SyncConfigFromController()
    {
        if (GameController.Instance == null) return;
        PredictCount       = GameController.Instance.predictCount;
        PredictionAccuracy = GameController.Instance.predictionAccuracy;
        MoveSpeed          = GameController.Instance.moveSpeed;
    }

    // ─────────────────────────────────────────────
    // Decision-making: prediction → habit → fallback
    // with uncertainty layered on top
    // ─────────────────────────────────────────────

    private void DecideTarget(List<(int x, int y, int intensity)> activeEchoes)
    {
        if (activeEchoes.Count == 0) return;

        // Determine AI mode
        var mode = GameController.Instance != null
            ? GameController.Instance.aiMode
            : GameController.AIMode.FullCombined;

        switch (mode)
        {
            case GameController.AIMode.Greedy:
                // Simply chase the loudest echo — no prediction
                PredictedTarget = (activeEchoes[0].x, activeEchoes[0].y);
                break;

            case GameController.AIMode.AStarOnly:
                // Chase loudest echo, A* handles the path in ExecuteMovement
                PredictedTarget = (activeEchoes[0].x, activeEchoes[0].y);
                break;

            case GameController.AIMode.PredictionOnly:
                // Use existing prediction pipeline (no A*)
                DecideTargetFullPipeline(activeEchoes);
                break;

            case GameController.AIMode.PredictionPlusAStar:
                // Predict the target, A* handles the path in ExecuteMovement
                DecideTargetFullPipeline(activeEchoes);
                break;

            case GameController.AIMode.FullCombined:
            default:
                // Full pipeline: prediction → habit → uncertainty → fallback
                DecideTargetFullPipeline(activeEchoes);
                break;
        }
    }

    /// <summary>
    /// Original decision-making pipeline: prediction → habit → uncertainty → fallback.
    /// Preserved as-is, called by modes that use prediction.
    /// </summary>
    private void DecideTargetFullPipeline(List<(int x, int y, int intensity)> activeEchoes)
    {
        if (activeEchoes.Count == 0) return;

        bool uncertaintyEnabled = GameController.Instance != null
            ? GameController.Instance.enableUncertainty
            : true;

        // --- Uncertainty Behavior ---
        double roll = rng.NextDouble();

        if (!uncertaintyEnabled || roll <= PredictionAccuracy)
        {
            // Try prediction first (movement delta extrapolation)
            var predicted = TryPredictPosition();

            if (predicted.HasValue)
            {
                PredictedTarget = predicted.Value;
            }
            else
            {
                bool habitEnabled = GameController.Instance != null
                    ? GameController.Instance.enableHabitLearning
                    : true;

                // Try habit bias when prediction isn't possible yet
                if (habitEnabled)
                {
                    var habitBiased = TryHabitBiasedPrediction();
                    if (habitBiased.HasValue)
                    {
                        PredictedTarget = habitBiased.Value;
                        return;
                    }
                }

                // Fallback — chase the loudest echo
                PredictedTarget = (activeEchoes[0].x, activeEchoes[0].y);
            }
        }
        else
        {
            // --- Investigation behavior ---
            // Pick a random active echo to investigate
            int idx = rng.Next(activeEchoes.Count);
            PredictedTarget = (activeEchoes[idx].x, activeEchoes[idx].y);
        }
    }

    // ─────────────────────────────────────────────
    // Prediction via movement deltas
    // ─────────────────────────────────────────────

    /// <summary>
    /// Analyses the last N echo-history entries, computes movement deltas,
    /// averages them, and extrapolates the next position.
    /// </summary>
    private (int x, int y)? TryPredictPosition()
    {
        if (echoHistory.Count < 2) return null;

        int count = Math.Min(PredictCount, echoHistory.Count);
        int start = echoHistory.Count - count;

        int dxSum = 0, dySum = 0;
        int deltas = 0;

        for (int i = start + 1; i < echoHistory.Count; i++)
        {
            dxSum += echoHistory[i].x - echoHistory[i - 1].x;
            dySum += echoHistory[i].y - echoHistory[i - 1].y;
            deltas++;
        }

        if (deltas == 0) return null;

        int avgDx = dxSum / deltas;
        int avgDy = dySum / deltas;

        var newest = echoHistory[^1];
        int px = Mathf.Clamp(newest.x + avgDx, 0, soundSystem.GetGridWidth() - 1);
        int py = Mathf.Clamp(newest.y + avgDy, 0, soundSystem.GetGridHeight() - 1);

        return (px, py);
    }

    // ─────────────────────────────────────────────
    // Habit-biased prediction
    // ─────────────────────────────────────────────

    /// <summary>
    /// Falls back to the most frequently observed player direction
    /// when delta-based prediction is not possible.
    /// </summary>
    private (int x, int y)? TryHabitBiasedPrediction()
    {
        string bestDir = null;
        int bestCount = 0;

        foreach (var kvp in directionFrequencies)
        {
            if (kvp.Value > bestCount)
            {
                bestCount = kvp.Value;
                bestDir = kvp.Key;
            }
        }

        if (bestDir == null || bestCount == 0) return null;

        var (dx, dy) = DirectionToVector(bestDir);
        int newestX = echoHistory.Count > 0 ? echoHistory[^1].x : GridX;
        int newestY = echoHistory.Count > 0 ? echoHistory[^1].y : GridY;

        int px = Mathf.Clamp(newestX + dx, 0, soundSystem.GetGridWidth() - 1);
        int py = Mathf.Clamp(newestY + dy, 0, soundSystem.GetGridHeight() - 1);

        return (px, py);
    }

    // ─────────────────────────────────────────────
    // Movement dispatch (A*-aware)
    // ─────────────────────────────────────────────

    /// <summary>
    /// Decides whether to use A* pathfinding or direct greedy movement,
    /// based on the current AI mode and GameController settings.
    /// </summary>
    private void ExecuteMovement(int tx, int ty)
    {
        bool useAStar = false;

        if (GameController.Instance != null)
        {
            var mode = GameController.Instance.aiMode;
            useAStar = GameController.Instance.enableAStar &&
                (mode == GameController.AIMode.AStarOnly ||
                 mode == GameController.AIMode.PredictionPlusAStar ||
                 mode == GameController.AIMode.FullCombined);
        }

        if (useAStar)
            MoveWithAStar(tx, ty);
        else
            MoveTowardDirect(tx, ty);
    }

    // ─────────────────────────────────────────────
    // A* pathfinding movement
    // ─────────────────────────────────────────────

    /// <summary>
    /// Uses AStarPathfinder to compute a path, then follows it step by step.
    /// Recomputes the path when the target changes.
    /// </summary>
    private void MoveWithAStar(int tx, int ty)
    {
        // Recompute path if target changed or path is exhausted
        bool needNewPath = currentPath.Count == 0 ||
                           pathIndex >= currentPath.Count ||
                           currentPath[currentPath.Count - 1] != new Vector2Int(tx, ty);

        if (needNewPath)
        {
            currentPath = AStarPathfinder.FindPath(soundSystem, GridX, GridY, tx, ty);
            pathIndex = 1; // index 0 is current position
        }

        // Follow the path ONE step per tick (strict single-tile movement)
        if (pathIndex < currentPath.Count)
        {
            var next = currentPath[pathIndex];

            // Safety: verify the next step is walkable (grid may change)
            if (soundSystem.IsWallGrid(next.x, next.y))
            {
                currentPath.Clear();
                // fall through to direct movement fallback below
            }
            else
            {
                Debug.Log($"[EnemyAI] Before Move: {GridX},{GridY}");
                GridX = next.x;
                GridY = next.y;
                pathIndex++;
                Debug.Log($"[EnemyAI] After Move: {GridX},{GridY} (A* step, path length: {currentPath.Count}, pathIndex: {pathIndex})");
            }
        }

        // If A* couldn't find a path, fall back to direct movement
        if (currentPath.Count == 0)
            MoveTowardDirect(tx, ty);
    }

    // ─────────────────────────────────────────────
    // Direct grid movement (wall-aware via SoundSystem)
    // Original movement logic — preserved as-is
    // ─────────────────────────────────────────────

    /// <summary>
    /// Moves up to MoveSpeed cells toward the target per tick.
    /// Uses SoundSystem.IsWallGrid() so all boundary logic is centralized.
    /// If the dominant axis is blocked, tries the secondary axis as fallback.
    /// </summary>
    private void MoveTowardDirect(int tx, int ty)
    {
        // Move EXACTLY one tile per tick (no loop)
        if (GridX == tx && GridY == ty) return;

        Debug.Log($"[EnemyAI] Before Move: {GridX},{GridY}");

        int dx = tx - GridX;
        int dy = ty - GridY;

        int newX = GridX;
        int newY = GridY;
        bool moved = false;

        // Try dominant axis first (Manhattan movement)
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            // Primary: horizontal
            newX = GridX + Math.Sign(dx);
            if (!soundSystem.IsWallGrid(newX, newY))
            {
                GridX = newX;
                moved = true;
            }
            else if (dy != 0)
            {
                // Fallback: try vertical if horizontal is blocked
                newX = GridX;
                newY = GridY + Math.Sign(dy);
                if (!soundSystem.IsWallGrid(newX, newY))
                {
                    GridY = newY;
                    moved = true;
                }
            }
        }
        else
        {
            // Primary: vertical
            newY = GridY + Math.Sign(dy);
            if (!soundSystem.IsWallGrid(newX, newY))
            {
                GridY = newY;
                moved = true;
            }
            else if (dx != 0)
            {
                // Fallback: try horizontal if vertical is blocked
                newY = GridY;
                newX = GridX + Math.Sign(dx);
                if (!soundSystem.IsWallGrid(newX, newY))
                {
                    GridX = newX;
                    moved = true;
                }
            }
        }

        if (moved)
            Debug.Log($"[EnemyAI] After Move: {GridX},{GridY} (direct step)");
        else
            Debug.Log($"[EnemyAI] Stuck at: {GridX},{GridY} — blocked toward ({tx},{ty})");
    }

    // ─────────────────────────────────────────────
    // Debug output
    // ─────────────────────────────────────────────

    public void PrintDebug()
    {
        string habits = "";
        foreach (var kvp in directionFrequencies)
            habits += $"{kvp.Key}={kvp.Value} ";

        Debug.Log($"[EnemyAI] pos: ({GridX},{GridY}) | Target: ({PredictedTarget.x},{PredictedTarget.y}) | History: {echoHistory.Count} | Habits: {habits}");
    }

    // ─────────────────────────────────────────────
    // Gizmos
    // ─────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (soundSystem == null || !Application.isPlaying) return;

        // Draw predicted target as cyan wireframe
        Gizmos.color = Color.cyan;
        Vector3 targetWorld = soundSystem.GridToWorld(PredictedTarget.x, PredictedTarget.y);
        Gizmos.DrawWireCube(targetWorld, Vector3.one * 0.8f);

        // Draw line from enemy to target
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawLine(transform.position, targetWorld);

        // Draw echo history trail as fading green dots
        for (int i = 0; i < echoHistory.Count; i++)
        {
            float t = (float)i / Mathf.Max(1, echoHistory.Count - 1);
            Gizmos.color = Color.Lerp(new Color(0, 0.5f, 0, 0.2f), new Color(0, 1f, 0, 0.8f), t);
            Vector3 echoWorld = soundSystem.GridToWorld(echoHistory[i].x, echoHistory[i].y);
            Gizmos.DrawSphere(echoWorld, 0.12f);
        }
    }
#endif

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    /// <summary>Classifies a delta into a cardinal direction name (Unity +Y = Up).</summary>
    private static string ClassifyDirection(int dx, int dy)
    {
        if (dx == 0 && dy == 0) return null;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx > 0 ? "Right" : "Left";
        else
            return dy > 0 ? "Up" : "Down";
    }

    /// <summary>Converts a cardinal direction name to a unit delta (Unity +Y = Up).</summary>
    private static (int dx, int dy) DirectionToVector(string direction) => direction switch
    {
        "Up"    => ( 0,  1),
        "Down"  => ( 0, -1),
        "Left"  => (-1,  0),
        "Right" => ( 1,  0),
        _       => ( 0,  0)
    };
}
