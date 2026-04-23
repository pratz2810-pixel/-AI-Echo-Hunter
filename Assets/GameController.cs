using UnityEngine;

/// <summary>
/// Central configuration controller for the stealth AI game.
/// Singleton — all systems read parameters from GameController.Instance.
/// If this object is missing from the scene, systems fall back to their own defaults.
/// </summary>
public class GameController : MonoBehaviour
{
    // ─────────────────────────────────────────────
    // Singleton
    // ─────────────────────────────────────────────

    public static GameController Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[GameController] Duplicate detected — destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // ─────────────────────────────────────────────
    // AI Mode Enum
    // ─────────────────────────────────────────────

    public enum AIMode
    {
        /// <summary>Chase the loudest echo directly.</summary>
        Greedy,
        /// <summary>Use delta-prediction + habit only (existing logic, no A*).</summary>
        PredictionOnly,
        /// <summary>Use A* pathfinding to reach loudest echo (no prediction).</summary>
        AStarOnly,
        /// <summary>Predict target position, then use A* to reach it.</summary>
        PredictionPlusAStar,
        /// <summary>Full pipeline: prediction → habit → uncertainty → A* movement.</summary>
        FullCombined
    }

    // ─────────────────────────────────────────────
    // Player Settings
    // ─────────────────────────────────────────────

    [Header("Player")]
    [Tooltip("Minimum seconds between player moves")]
    public float moveDelay = 0.15f;

    [Tooltip("Play a footstep sound on each successful move")]
    public bool enableStepSound = true;

    // ─────────────────────────────────────────────
    // Sound Settings
    // ─────────────────────────────────────────────

    [Header("Sound")]
    [Tooltip("Initial intensity when a sound echo is created")]
    [Range(1, 20)]
    public int soundStrength = 5;

    [Tooltip("How many intensity points decay per tick")]
    [Range(1, 5)]
    public int decaySpeed = 1;

    // ─────────────────────────────────────────────
    // Enemy AI Settings
    // ─────────────────────────────────────────────

    [Header("Enemy AI")]
    [Tooltip("Grid cells per tick the enemy can move")]
    [Range(1, 3)]
    public int moveSpeed = 1;

    [Tooltip("Number of recent echoes used for prediction")]
    [Range(2, 10)]
    public int predictCount = 4;

    [Tooltip("Probability (0–1) that AI uses prediction vs. random investigation")]
    [Range(0f, 1f)]
    public float predictionAccuracy = 0.8f;

    [Tooltip("Enable habit-learning (directional frequency analysis)")]
    public bool enableHabitLearning = true;

    [Tooltip("Enable uncertainty (random investigation rolls)")]
    public bool enableUncertainty = true;

    [Tooltip("Enable A* pathfinding for enemy movement")]
    public bool enableAStar = true;

    [Header("AI Mode")]
    [Tooltip("Select the AI behavior mode")]
    public AIMode aiMode = AIMode.FullCombined;

    // ─────────────────────────────────────────────
    // Visual Effects
    // ─────────────────────────────────────────────

    [Header("Visual — Player")]
    [Tooltip("Speed of player pulse oscillation")]
    public float playerPulseSpeed = 2f;

    [Tooltip("Amount of player scale oscillation")]
    [Range(0f, 0.3f)]
    public float playerPulseAmount = 0.05f;

    [Header("Visual — Enemy")]
    [Tooltip("Speed of enemy pulse oscillation")]
    public float enemyPulseSpeed = 3f;

    [Tooltip("Amount of enemy scale oscillation")]
    [Range(0f, 0.3f)]
    public float enemyPulseAmount = 0.08f;

    [Tooltip("Strength of enemy alpha flicker (0 = none)")]
    [Range(0f, 0.5f)]
    public float enemyFlickerStrength = 0.15f;
}
