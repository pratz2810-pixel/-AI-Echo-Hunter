using UnityEngine;

/// <summary>
/// Scale-based pulse effect using sine oscillation.
/// Reads speed/amount from GameController (player vs. enemy).
/// Attach to Player and/or Enemy GameObjects.
/// </summary>
public class PulseEffect : MonoBehaviour
{
    [Tooltip("Check this for Player pulse settings, uncheck for Enemy pulse settings")]
    [SerializeField] private bool isPlayer = true;

    private Vector3 baseScale;

    void Start()
    {
        baseScale = transform.localScale;
    }

    void Update()
    {
        float speed  = 2f;  // default
        float amount = 0.05f;

        // Read from GameController if available
        if (GameController.Instance != null)
        {
            if (isPlayer)
            {
                speed  = GameController.Instance.playerPulseSpeed;
                amount = GameController.Instance.playerPulseAmount;
            }
            else
            {
                speed  = GameController.Instance.enemyPulseSpeed;
                amount = GameController.Instance.enemyPulseAmount;
            }
        }

        float pulse = 1f + Mathf.Sin(Time.time * speed) * amount;
        transform.localScale = baseScale * pulse;
    }
}
