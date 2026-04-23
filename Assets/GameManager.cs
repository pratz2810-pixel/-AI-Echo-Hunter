using UnityEngine;

/// <summary>
/// Orchestrates the game loop: sound decay and enemy AI ticking.
/// Runs on a configurable tick interval independent of player input.
/// Attach to a GameObject in the scene and assign references.
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Tick Settings")]
    [Tooltip("Seconds between each game tick (sound decay + enemy AI update)")]
    [SerializeField] private float tickInterval = 0.3f;

    [Header("References")]
    [SerializeField] private SoundSystem soundSystem;
    [SerializeField] private EnemyAI enemyAI;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = false;

    private float tickTimer;

    void Start()
    {
        // Auto-find references if not assigned in Inspector
        if (soundSystem == null)
            soundSystem = FindObjectOfType<SoundSystem>();
        if (enemyAI == null)
            enemyAI = FindObjectOfType<EnemyAI>();

        if (soundSystem == null)
            Debug.LogError("[GameManager] SoundSystem not found! Add one to the scene.");
        if (enemyAI == null)
            Debug.LogError("[GameManager] EnemyAI not found! Add an Enemy to the scene.");

        Debug.Log("[GameManager] Initialized. Tick interval: " + tickInterval + "s");
    }

    void Update()
    {
        tickTimer += Time.deltaTime;

        if (tickTimer >= tickInterval)
        {
            tickTimer -= tickInterval;

            // 1. Decay all sound echoes
            if (soundSystem != null)
                soundSystem.DecaySound();

            // 2. Enemy AI observes echoes and decides + moves
            if (enemyAI != null)
                enemyAI.Tick();

            if (showDebugLogs)
            {
                if (soundSystem != null)
                    soundSystem.PrintSoundGrid();
                if (enemyAI != null)
                    enemyAI.PrintDebug();
            }
        }
    }
}
