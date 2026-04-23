using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Central sound system for the echo-based stealth game.
/// Single source of truth for: sound echoes, grid boundaries, coordinate conversion, wall data.
///
/// Reads the premade Tilemap at startup to auto-detect grid size and wall positions.
/// No hardcoded dimensions — everything adapts to whatever tilemap is in the scene.
/// </summary>
public class SoundSystem : MonoBehaviour
{
    [Header("Sound Settings")]
    [Tooltip("Initial intensity when a sound echo is created")]
    [SerializeField] private int initialIntensity = 5;

    [Header("Tilemap Reference")]
    [Tooltip("The Tilemap containing wall and floor tiles. Auto-detected if not assigned.")]
    [SerializeField] private Tilemap tilemap;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    // Grid dimensions — derived from the tilemap at runtime
    private int gridWidth;
    private int gridHeight;
    private int originX;
    private int originY;

    private int[,] soundGrid;
    private bool[,] wallMap;

    void Awake()
    {
        // Auto-find the tilemap if not assigned in Inspector
        if (tilemap == null)
            tilemap = FindObjectOfType<Tilemap>();

        if (tilemap == null)
        {
            Debug.LogError("[SoundSystem] No Tilemap found in scene! Add one or assign it in Inspector.");
            return;
        }

        // Read grid dimensions directly from the premade tilemap
        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;

        gridWidth  = bounds.size.x;
        gridHeight = bounds.size.y;
        originX    = bounds.xMin;
        originY    = bounds.yMin;

        soundGrid = new int[gridWidth, gridHeight];
        wallMap   = new bool[gridWidth, gridHeight];

        // Scan every cell in the tilemap to build the wall map
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector3Int cellPos = new Vector3Int(x + originX, y + originY, 0);
                TileBase tile = tilemap.GetTile(cellPos);

                if (tile == null)
                {
                    // No tile = impassable
                    wallMap[x, y] = true;
                }
                else
                {
                    // Identify walls by tile name (matches WallTile.asset)
                    wallMap[x, y] = tile.name.Contains("Wall");
                }
            }
        }

        Debug.Log($"[SoundSystem] Grid loaded from tilemap: {gridWidth}x{gridHeight}, origin ({originX},{originY})");
    }

    // ── Sound Creation & Decay ──────────────────────────────

    /// <summary>Create a sound echo at the given grid position.</summary>
    public void CreateSound(int gx, int gy)
    {
        if (InBounds(gx, gy) && !wallMap[gx, gy])
        {
            int strength = (GameController.Instance != null)
                ? GameController.Instance.soundStrength
                : initialIntensity;
            soundGrid[gx, gy] = strength;
        }
    }

    /// <summary>All echoes decay by 1 intensity per tick.</summary>
    public void DecaySound()
    {
        int decay = (GameController.Instance != null)
            ? GameController.Instance.decaySpeed
            : 1;

        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                if (soundGrid[x, y] > 0)
                {
                    soundGrid[x, y] -= decay;
                    if (soundGrid[x, y] < 0)
                        soundGrid[x, y] = 0;
                }
    }

    // ── Queries (used by EnemyAI) ───────────────────────────

    public int GetSound(int gx, int gy) =>
        InBounds(gx, gy) ? soundGrid[gx, gy] : 0;

    public int GetGridWidth()  => gridWidth;
    public int GetGridHeight() => gridHeight;

    /// <summary>
    /// Returns all cells with sound intensity > 0, sorted loudest first.
    /// </summary>
    public List<(int x, int y, int intensity)> GetActiveEchoes()
    {
        var echoes = new List<(int x, int y, int intensity)>();
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                if (soundGrid[x, y] > 0)
                    echoes.Add((x, y, soundGrid[x, y]));
        echoes.Sort((a, b) => b.intensity.CompareTo(a.intensity));
        return echoes;
    }

    /// <summary>
    /// Returns the position of the loudest echo, or null if silent.
    /// </summary>
    public (int x, int y)? GetLoudestEcho()
    {
        int best = 0;
        (int x, int y)? pos = null;
        for (int x = 0; x < gridWidth; x++)
            for (int y = 0; y < gridHeight; y++)
                if (soundGrid[x, y] > best)
                {
                    best = soundGrid[x, y];
                    pos = (x, y);
                }
        return pos;
    }

    // ── Boundary & Wall Checks (single source of truth) ─────

    /// <summary>Is a grid index within the array bounds?</summary>
    public bool InBounds(int gx, int gy) =>
        gx >= 0 && gx < gridWidth && gy >= 0 && gy < gridHeight;

    /// <summary>
    /// Grid-coordinate wall check — returns true if out of bounds OR on a wall tile.
    /// </summary>
    public bool IsWallGrid(int gx, int gy)
    {
        if (!InBounds(gx, gy)) return true;
        return wallMap[gx, gy];
    }

    /// <summary>
    /// Returns true if the grid cell is walkable floor.
    /// </summary>
    public bool IsFloor(int gx, int gy)
    {
        return InBounds(gx, gy) && !wallMap[gx, gy];
    }

    /// <summary>
    /// World-coordinate wall check — converts to grid indices, then checks wallMap.
    /// Used by PlayerMovement.
    /// </summary>
    public bool IsWall(Vector2 worldPos)
    {
        var (gx, gy) = WorldToGrid(worldPos);
        return IsWallGrid(gx, gy);
    }

    // ── Coordinate Conversion ───────────────────────────────
    //
    // Tilemap cells span integer ranges: cell (cx, cy) covers world area [cx, cx+1) × [cy, cy+1).
    // Tile sprites are drawn centered (anchor 0.5, 0.5), so tile center = (cx + 0.5, cy + 0.5).
    // We place game objects at tile centers so they visually align with tiles.
    //
    // Grid array indices: gx = cx - originX,  gy = cy - originY
    // World center of cell:  wx = cx + 0.5,  wy = cy + 0.5
    //
    // WorldToGrid: from world center → array index
    // GridToWorld: from array index → world center

    /// <summary>Convert world position to grid array indices.</summary>
    public (int gx, int gy) WorldToGrid(Vector2 worldPos) =>
        (Mathf.FloorToInt(worldPos.x) - originX,
         Mathf.FloorToInt(worldPos.y) - originY);

    /// <summary>Convert grid array indices to world position (tile center).</summary>
    public Vector3 GridToWorld(int gx, int gy) =>
        new Vector3(gx + originX + 0.5f, gy + originY + 0.5f, 0f);

    // ── Debug ───────────────────────────────────────────────

    public void PrintSoundGrid()
    {
        string output = "Sound Grid:\n";
        for (int y = gridHeight - 1; y >= 0; y--)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (wallMap[x, y])
                    output += "# ";
                else
                    output += soundGrid[x, y] + " ";
            }
            output += "\n";
        }
        Debug.Log(output);
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showGizmos || wallMap == null) return;

        // Draw walls as dark cubes
        Gizmos.color = new Color(0.3f, 0.15f, 0.05f, 0.4f);
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (wallMap[x, y])
                {
                    Vector3 center = GridToWorld(x, y);
                    Gizmos.DrawCube(center, Vector3.one * 0.9f);
                }
            }
        }

        // Draw active echoes as yellow-to-red spheres
        if (soundGrid == null) return;
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (soundGrid[x, y] > 0)
                {
                    float t = soundGrid[x, y] / (float)initialIntensity;
                    Gizmos.color = Color.Lerp(Color.yellow, Color.red, t) * new Color(1, 1, 1, 0.5f);
                    Vector3 center = GridToWorld(x, y);
                    Gizmos.DrawSphere(center, 0.2f + 0.2f * t);
                }
            }
        }
    }
#endif
}
