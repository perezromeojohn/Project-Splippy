using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace projectsplippy
{
    public class PathResolutionController : MonoBehaviour
    {
        [Header("Marine Resolve Timing")]
        [SerializeField] private float marinePauseDuration = 0.14f;
        [SerializeField] private float marineRippleStepDelay = 0.045f;

        [Header("Floating Event Text")]
        [SerializeField] private Color torrentTextColor = new Color(0.25f, 0.86f, 1f, 1f);
        [SerializeField] private Color marineTextColor = new Color(0.35f, 0.72f, 1f, 1f);
        [SerializeField] private float torrentTextWorldYOffset = 0.85f;

        public IEnumerator ResolvePath(
            TileBoardSystem tileBoardSystem,
            TileBoardView boardView,
            RunStateController runState,
            List<TileStepResult> deferredPathStepResults,
            Vector2Int currentCell,
            Vector3 splippyWorldPosition)
        {
            if (tileBoardSystem == null || boardView == null || deferredPathStepResults == null)
            {
                yield break;
            }

            if (deferredPathStepResults.Count == 0)
            {
                boardView.RefreshProgressVisuals(tileBoardSystem);
                yield break;
            }

            bool torrentWasActiveForPath = runState != null && runState.IsTorrentActive;
            List<string> collisionOrder = BuildCollisionOrderDebug(deferredPathStepResults, boardView);
            runState?.ApplyPathResolution(deferredPathStepResults, collisionOrder);
            bool torrentActivated = runState != null && runState.ConsumeTorrentActivationFlag();
            var traversedCells = new List<Vector2Int>(deferredPathStepResults.Count);
            var marineCenters = new List<Vector2Int>();
            var forceFarmlandCells = new HashSet<Vector2Int>();

            if (torrentActivated)
            {
                Vector3 burstPosition = splippyWorldPosition + new Vector3(0f, torrentTextWorldYOffset, 0f);
                boardView.PlayFloatingTextAtWorld(burstPosition, "TORRENT!", torrentTextColor);
            }

            for (int i = 0; i < deferredPathStepResults.Count; i++)
            {
                TileStepResult step = deferredPathStepResults[i];
                traversedCells.Add(step.Cell);

                if (torrentWasActiveForPath && step.EnteredType == TileType.Trash)
                {
                    forceFarmlandCells.Add(step.Cell);
                }

                if (step.EnteredType == TileType.Marine && !marineCenters.Contains(step.Cell))
                {
                    marineCenters.Add(step.Cell);
                }

                if (step.LandingResult != null)
                {
                    for (int e = 0; e < step.LandingResult.ExpiredToTrashCells.Count; e++)
                    {
                        Vector2Int expiredCell = step.LandingResult.ExpiredToTrashCells[e];
                        boardView.PlayTileReplacementFlip(expiredCell, TileType.Trash, pulseAfterReplace: true);
                    }
                }
            }

            Dictionary<Vector2Int, TileType> traversedReplacements = tileBoardSystem.ReplaceTraversedTiles(
                traversedCells,
                currentCell,
                forceFarmlandCells.Count > 0 ? forceFarmlandCells : null);

            PlayReplacementFlips(tileBoardSystem, boardView, traversedReplacements);

            if (marineCenters.Count > 0)
            {
                for (int i = 0; i < marineCenters.Count; i++)
                {
                    Vector2Int marineCenter = marineCenters[i];
                    boardView.PlayFloatingText(marineCenter, "MARINE!!", marineTextColor);

                    float pause = Mathf.Max(0f, marinePauseDuration);

                    if (pause > 0f)
                    {
                        yield return new WaitForSeconds(pause);
                    }

                    tileBoardSystem.ClearHazardsInCross(marineCenter);
                    Dictionary<Vector2Int, TileType> crossVisualFlips = BuildCrossVisualFlips(tileBoardSystem, marineCenter);

                    yield return StartCoroutine(PlayReplacementFlipsOutward(
                        tileBoardSystem,
                        boardView,
                        crossVisualFlips,
                        marineCenter));
                }
            }

            boardView.RefreshProgressVisuals(tileBoardSystem);
            deferredPathStepResults.Clear();
        }

        private static void PlayReplacementFlips(
            TileBoardSystem tileBoardSystem,
            TileBoardView boardView,
            IReadOnlyDictionary<Vector2Int, TileType> replacements)
        {
            if (replacements == null)
            {
                return;
            }

            foreach (KeyValuePair<Vector2Int, TileType> replacement in replacements)
            {
                PlayReplacementFlip(tileBoardSystem, boardView, replacement.Key, replacement.Value);
            }
        }

        private IEnumerator PlayReplacementFlipsOutward(
            TileBoardSystem tileBoardSystem,
            TileBoardView boardView,
            IReadOnlyDictionary<Vector2Int, TileType> replacements,
            Vector2Int center)
        {
            if (replacements == null || replacements.Count == 0)
            {
                yield break;
            }

            var rings = new SortedDictionary<int, List<KeyValuePair<Vector2Int, TileType>>>();

            foreach (KeyValuePair<Vector2Int, TileType> replacement in replacements)
            {
                int distance = Mathf.Abs(replacement.Key.x - center.x) + Mathf.Abs(replacement.Key.y - center.y);

                if (!rings.TryGetValue(distance, out List<KeyValuePair<Vector2Int, TileType>> ring))
                {
                    ring = new List<KeyValuePair<Vector2Int, TileType>>();
                    rings[distance] = ring;
                }

                ring.Add(replacement);
            }

            float rippleDelay = Mathf.Max(0f, marineRippleStepDelay);
            int ringIndex = 0;
            int ringCount = rings.Count;

            foreach (KeyValuePair<int, List<KeyValuePair<Vector2Int, TileType>>> ringEntry in rings)
            {
                List<KeyValuePair<Vector2Int, TileType>> ring = ringEntry.Value;

                for (int i = 0; i < ring.Count; i++)
                {
                    KeyValuePair<Vector2Int, TileType> replacement = ring[i];
                    PlayReplacementFlip(tileBoardSystem, boardView, replacement.Key, replacement.Value);
                }

                ringIndex++;

                if (rippleDelay > 0f && ringIndex < ringCount)
                {
                    yield return new WaitForSeconds(rippleDelay);
                }
            }
        }

        private static void PlayReplacementFlip(
            TileBoardSystem tileBoardSystem,
            TileBoardView boardView,
            Vector2Int cell,
            TileType tileType)
        {
            int farmlandVariantIndex = -1;
            int sanitationTurns = -1;

            if (tileBoardSystem.TryGetTile(cell, out TileData tile))
            {
                if (tileType == TileType.Farmland)
                {
                    farmlandVariantIndex = tile.CropVariantIndex;
                }
                else if (tileType == TileType.Sanitation || tileType == TileType.WorstSanitation)
                {
                    sanitationTurns = tile.SanitationTimer;
                }
            }

            boardView.PlayTileReplacementFlip(
                cell,
                tileType,
                pulseAfterReplace: true,
                forcedFarmlandCropVariantIndex: farmlandVariantIndex,
                forcedSanitationTurns: sanitationTurns);
        }

        private static Dictionary<Vector2Int, TileType> BuildCrossVisualFlips(
            TileBoardSystem tileBoardSystem,
            Vector2Int center)
        {
            var flips = new Dictionary<Vector2Int, TileType>();

            if (tileBoardSystem == null)
            {
                return flips;
            }

            int gridSize = tileBoardSystem.GridSize;

            for (int x = 0; x < gridSize; x++)
            {
                AddCrossVisualFlipForCell(tileBoardSystem, new Vector2Int(x, center.y), flips);
            }

            for (int y = 0; y < gridSize; y++)
            {
                AddCrossVisualFlipForCell(tileBoardSystem, new Vector2Int(center.x, y), flips);
            }

            return flips;
        }

        private static void AddCrossVisualFlipForCell(
            TileBoardSystem tileBoardSystem,
            Vector2Int cell,
            Dictionary<Vector2Int, TileType> flips)
        {
            if (flips.ContainsKey(cell))
            {
                return;
            }

            if (!tileBoardSystem.TryGetTile(cell, out TileData tile) || tile == null)
            {
                return;
            }

            if (tile.Type == TileType.Rock || tile.Type == TileType.Filler)
            {
                return;
            }

            flips[cell] = tile.Type;
        }

        private static List<string> BuildCollisionOrderDebug(IReadOnlyList<TileStepResult> steps, TileBoardView boardView)
        {
            var labels = new List<string>(steps != null ? steps.Count : 0);

            if (steps == null)
            {
                return labels;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                TileStepResult step = steps[i];

                if (step.EnteredType == TileType.Farmland)
                {
                    labels.Add(boardView != null
                        ? boardView.GetCropVariantLabel(step.EnteredCropVariantIndex)
                        : $"Crop[{step.EnteredCropVariantIndex}]");
                }
                else
                {
                    labels.Add(step.EnteredType.ToString());
                }
            }

            return labels;
        }
    }
}
