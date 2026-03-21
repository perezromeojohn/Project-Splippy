using System;
using System.Collections.Generic;
using UnityEngine;

namespace projectsplippy
{
    public static class GridPathfinder
    {
        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        public static bool TryFindPathBfs(
            int gridSize,
            Vector2Int start,
            Vector2Int goal,
            Func<Vector2Int, bool> isWalkable,
            out List<Vector2Int> path)
        {
            path = null;

            if (!IsInBounds(gridSize, start) || !IsInBounds(gridSize, goal))
            {
                return false;
            }

            if (start == goal)
            {
                path = new List<Vector2Int> { start };
                return true;
            }

            bool[,] visited = new bool[gridSize, gridSize];
            Vector2Int[,] cameFrom = new Vector2Int[gridSize, gridSize];
            Queue<Vector2Int> frontier = new Queue<Vector2Int>();

            frontier.Enqueue(start);
            visited[start.x, start.y] = true;

            bool foundGoal = false;

            while (frontier.Count > 0)
            {
                Vector2Int current = frontier.Dequeue();

                for (int i = 0; i < CardinalDirections.Length; i++)
                {
                    Vector2Int next = current + CardinalDirections[i];

                    if (!IsInBounds(gridSize, next) || visited[next.x, next.y])
                    {
                        continue;
                    }

                    if (isWalkable != null && !isWalkable(next))
                    {
                        continue;
                    }

                    visited[next.x, next.y] = true;
                    cameFrom[next.x, next.y] = current;

                    if (next == goal)
                    {
                        foundGoal = true;
                        frontier.Clear();
                        break;
                    }

                    frontier.Enqueue(next);
                }
            }

            if (!foundGoal)
            {
                return false;
            }

            path = new List<Vector2Int>();
            Vector2Int step = goal;
            path.Add(step);

            while (step != start)
            {
                step = cameFrom[step.x, step.y];
                path.Add(step);
            }

            path.Reverse();
            return true;
        }

        private static bool IsInBounds(int gridSize, Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < gridSize && cell.y < gridSize;
        }
    }
}