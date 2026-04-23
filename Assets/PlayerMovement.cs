using UnityEngine;

/// <summary>
/// Grid-based player movement (WASD, 1 cell per step).
/// Triggers SoundSystem on each successful move so the enemy can track echoes.
/// Uses SoundSystem.IsWall() which reads from the premade tilemap.
///
/// Optional: assign an AudioClip to stepSound to hear footsteps.
/// The audio is purely cosmetic and does not affect the AI echo system.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Minimum seconds between moves (movement cooldown)")]
    public float moveDelay = 0.15f;

    [Header("References")]
    [SerializeField] private SoundSystem soundSystem;

    [Header("Audio (Optional)")]
    [Tooltip("Assign a footstep AudioClip. Leave empty if none available.")]
    [SerializeField] private AudioClip stepSound;
    [SerializeField] [Range(0f, 1f)] private float stepVolume = 0.3f;

    private float timer;
    private AudioSource audioSource;

    void Start()
    {
        if (soundSystem == null)
            soundSystem = FindObjectOfType<SoundSystem>();

        if (soundSystem == null)
        {
            Debug.LogError("[PlayerMovement] No SoundSystem found! Add one to the scene.");
            return;
        }

        // Snap to nearest tile center using coordinate conversion
        var (gx, gy) = soundSystem.WorldToGrid(transform.position);
        transform.position = soundSystem.GridToWorld(gx, gy);

        // Disable physics interference — movement is pure grid-based
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }

        // Setup audio source for optional step sounds
        if (stepSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = stepSound;
            audioSource.playOnAwake = false;
            audioSource.volume = stepVolume;
        }

        Debug.Log($"[PlayerMovement] Initialized at grid ({gx},{gy}), world {transform.position}");
    }

    void Update()
    {
        if (soundSystem == null) return;

        timer += Time.deltaTime;

        float delay = (GameController.Instance != null)
            ? GameController.Instance.moveDelay
            : moveDelay;
        if (timer < delay) return;

        // Read input — cardinal only, no diagonal
        int x = 0;
        int y = 0;

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    y =  1;
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  y = -1;
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x = -1;
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x =  1;

        if (x == 0 && y == 0) return; // No input

        // Calculate target position in world space
        // Current position is already at a tile center, so we move by exactly 1 unit
        Vector2 currentPos = transform.position;
        Vector2 newPos = currentPos + new Vector2(x, y);

        // Wall check via SoundSystem (reads from premade tilemap)
        if (!soundSystem.IsWall(newPos))
        {
            transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);

            // Create a sound echo at the new grid position
            var (gx, gy) = soundSystem.WorldToGrid(newPos);
            soundSystem.CreateSound(gx, gy);

            // Play optional step sound (cosmetic only, not AI echo)
            bool soundEnabled = (GameController.Instance != null)
                ? GameController.Instance.enableStepSound
                : true;
            if (soundEnabled && audioSource != null && stepSound != null)
                audioSource.PlayOneShot(stepSound, stepVolume);
        }

        timer = 0f; // Reset cooldown even if blocked (prevents input queue buildup)
    }
}