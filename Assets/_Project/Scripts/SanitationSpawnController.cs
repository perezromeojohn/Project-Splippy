using System.Collections.Generic;
using UnityEngine;

namespace projectsplippy
{
    public class SanitationSpawnController : MonoBehaviour
    {
        [Header("Score Tier Thresholds")]
        [SerializeField, Min(0)] private int flowThreshold = 501;
        [SerializeField, Min(0)] private int surgeThreshold = 1501;
        [SerializeField, Min(0)] private int chaosThreshold = 3001;

        [Header("Spawn Cadence")]
        [SerializeField, Min(1)] private int sproutSpawnEveryTurns = 3;
        [SerializeField, Min(1)] private int sproutSpawnAmount = 1;
        [SerializeField, Min(1)] private int flowSpawnEveryTurns = 3;
        [SerializeField, Min(1)] private int flowSpawnAmount = 2;
        [SerializeField, Min(1)] private int surgeSpawnEveryTurns = 2;
        [SerializeField, Min(1)] private int surgeSpawnAmount = 2;
        [SerializeField, Min(1)] private int chaosSpawnEveryTurns = 1;
        [SerializeField, Min(1)] private int chaosSpawnAmount = 3;

        private int turnsSinceSanitationSpawn;

        public void ResetTracker(RunStateController runState)
        {
            turnsSinceSanitationSpawn = 0;
            UpdateSanitationSpawnTracker(runState);
        }

        public void HandleResolvedTurn(
            TileBoardSystem tileBoardSystem,
            TileBoardView boardView,
            RunStateController runState,
            Vector2Int protectedCell)
        {
            if (tileBoardSystem == null || boardView == null || runState == null)
            {
                return;
            }

            GetSanitationSpawnConfig(runState.CurrentScore, out int everyTurns, out int spawnAmount);
            turnsSinceSanitationSpawn++;

            if (turnsSinceSanitationSpawn < everyTurns)
            {
                UpdateSanitationSpawnTracker(runState);
                return;
            }

            turnsSinceSanitationSpawn = 0;
            Dictionary<Vector2Int, TileType> spawned = tileBoardSystem.SpawnSanitationTiles(spawnAmount, protectedCell);

            foreach (KeyValuePair<Vector2Int, TileType> replacement in spawned)
            {
                int farmlandVariantIndex = -1;
                int sanitationTurns = -1;

                if (tileBoardSystem.TryGetTile(replacement.Key, out TileData tile))
                {
                    if (replacement.Value == TileType.Farmland)
                    {
                        farmlandVariantIndex = tile.CropVariantIndex;
                    }
                    else if (replacement.Value == TileType.Sanitation)
                    {
                        sanitationTurns = tile.SanitationTimer;
                    }
                }

                boardView.PlayTileReplacementFlip(
                    replacement.Key,
                    replacement.Value,
                    pulseAfterReplace: true,
                    forcedFarmlandCropVariantIndex: farmlandVariantIndex,
                    forcedSanitationTurns: sanitationTurns);
            }

            if (spawned.Count > 0)
            {
                boardView.RefreshProgressVisuals(tileBoardSystem);
            }

            UpdateSanitationSpawnTracker(runState);
        }

        private void UpdateSanitationSpawnTracker(RunStateController runState)
        {
            if (runState == null)
            {
                return;
            }

            GetSanitationSpawnConfig(runState.CurrentScore, out int everyTurns, out _);
            int turnsLeft = Mathf.Max(1, everyTurns - turnsSinceSanitationSpawn);
            runState.SetNextSanitationSpawnIn(turnsLeft);
        }

        private void GetSanitationSpawnConfig(int score, out int everyTurns, out int spawnAmount)
        {
            if (score >= chaosThreshold)
            {
                everyTurns = Mathf.Max(1, chaosSpawnEveryTurns);
                spawnAmount = Mathf.Max(1, chaosSpawnAmount);
                return;
            }

            if (score >= surgeThreshold)
            {
                everyTurns = Mathf.Max(1, surgeSpawnEveryTurns);
                spawnAmount = Mathf.Max(1, surgeSpawnAmount);
                return;
            }

            if (score >= flowThreshold)
            {
                everyTurns = Mathf.Max(1, flowSpawnEveryTurns);
                spawnAmount = Mathf.Max(1, flowSpawnAmount);
                return;
            }

            everyTurns = Mathf.Max(1, sproutSpawnEveryTurns);
            spawnAmount = Mathf.Max(1, sproutSpawnAmount);
        }
    }
}
