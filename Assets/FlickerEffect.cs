using UnityEngine;

/// <summary>
/// Random alpha flicker effect for the enemy sprite.
/// Uses Perlin noise for smooth, organic flickering.
/// Reads strength from GameController.
/// Attach to the Enemy GameObject (requires SpriteRenderer).
/// </summary>
public class FlickerEffect : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float noiseOffset;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning("[FlickerEffect] No SpriteRenderer found — disabling flicker.");
            enabled = false;
            return;
        }

        // Random offset so multiple enemies don't flicker in sync
        noiseOffset = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (spriteRenderer == null) return;

        float strength = 0.15f; // default
        if (GameController.Instance != null)
            strength = GameController.Instance.enemyFlickerStrength;

        // Perlin noise gives smooth 0–1 range; remap to alpha variation
        float noise = Mathf.PerlinNoise(Time.time * 5f + noiseOffset, 0f);
        float alpha = 1f - noise * strength;

        Color c = spriteRenderer.color;
        c.a = Mathf.Clamp01(alpha);
        spriteRenderer.color = c;
    }
}
