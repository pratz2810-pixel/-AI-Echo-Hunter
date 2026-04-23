using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grid-based A* pathfinding for the stealth echo game.
/// Static utility — no MonoBehaviour needed.
///
/// Uses SoundSystem.IsWallGrid() for obstacle data so wall logic
/// stays in one place (no duplication).
/// </summary>
public static class AStarPathfinder
{
    /// <summary>
    /// Find the shortest walkable path from (startX,startY) to (goalX,goalY)
    /// on the grid managed by the given SoundSystem.
    /// Returns a list of grid positions from start to goal (inclusive).
    /// Returns an empty list if no path exists.
    /// </summary>
    public static List<Vector2Int> FindPath(SoundSystem ss, int startX, int startY, int goalX, int goalY)
    {
        if (ss == null) return new List<Vector2Int>();

        // If start or goal is a wall, no path
        if (ss.IsWallGrid(startX, startY) || ss.IsWallGrid(goalX, goalY))
            return new List<Vector2Int>();

        // Already there
        if (startX == goalX && startY == goalY)
            return new List<Vector2Int> { new Vector2Int(startX, startY) };

        var start = new Vector2Int(startX, startY);
        var goal  = new Vector2Int(goalX, goalY);

        // Open set — using a simple list as priority queue (grid is small)
        var openSet  = new List<Vector2Int> { start };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();

        var gScore = new Dictionary<Vector2Int, int> { { start, 0 } };
        var fScore = new Dictionary<Vector2Int, int> { { start, Heuristic(start, goal) } };

        // Cardinal directions only (matches grid movement)
        Vector2Int[] neighbors = {
            new Vector2Int( 0,  1),  // Up
            new Vector2Int( 0, -1),  // Down
            new Vector2Int(-1,  0),  // Left
            new Vector2Int( 1,  0)   // Right
        };

        int maxIterations = ss.GetGridWidth() * ss.GetGridHeight() * 2; // safety cap
        int iterations = 0;

        while (openSet.Count > 0 && iterations < maxIterations)
        {
            iterations++;

            // Find node in openSet with lowest fScore
            int bestIdx = 0;
            int bestF   = GetScore(fScore, openSet[0]);
            for (int i = 1; i < openSet.Count; i++)
            {
                int f = GetScore(fScore, openSet[i]);
                if (f < bestF)
                {
                    bestF   = f;
                    bestIdx = i;
                }
            }

            var current = openSet[bestIdx];

            // Reached goal — reconstruct path
            if (current == goal)
                return ReconstructPath(cameFrom, current);

            openSet.RemoveAt(bestIdx);

            foreach (var dir in neighbors)
            {
                var neighbor = current + dir;

                // Skip walls and out-of-bounds (IsWallGrid handles both)
                if (ss.IsWallGrid(neighbor.x, neighbor.y))
                    continue;

                int tentativeG = GetScore(gScore, current) + 1;

                if (tentativeG < GetScore(gScore, neighbor))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor]   = tentativeG;
                    fScore[neighbor]   = tentativeG + Heuristic(neighbor, goal);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        // No path found
        return new List<Vector2Int>();
    }

    /// <summary>Manhattan distance heuristic (admissible for 4-directional grid).</summary>
    private static int Heuristic(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    /// <summary>Get score with int.MaxValue default for unvisited nodes.</summary>
    private static int GetScore(Dictionary<Vector2Int, int> scores, Vector2Int node) =>
        scores.TryGetValue(node, out int val) ? val : int.MaxValue;

    /// <summary>Reconstruct path by walking cameFrom links back to start.</summary>
    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Add(current);
        }
        path.Reverse();
        return path;
    }
}
