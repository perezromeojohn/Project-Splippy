using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace projectsplippy
{
    public class GameplayPressureController : MonoBehaviour
    {
        [Header("Vignette")]
        [SerializeField] private Volume globalVolume;
        [SerializeField, Min(0)] private int lowMoveVignetteThreshold = 1;
        [SerializeField, Range(0f, 1f)] private float lowMoveVignetteIntensity = 0.28f;
        [SerializeField] private float vignetteBlendSpeed = 2.5f;

        [Header("Game Over")]
        [SerializeField, Range(0f, 1f)] private float trashCorruptionGameOverThreshold = 0.7f;

        [Header("Torrent Vignette")]
        [SerializeField] private bool enableTorrentBlueVignette = true;
        [SerializeField] private Color torrentVignetteColor = new Color(0.24f, 0.52f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float torrentVignetteIntensity = 0.9f;

        [Header("Torrent Particle Hook")]
        [SerializeField] private ParticleSystem torrentParticleEmitter;
        [SerializeField] private bool enableTorrentParticleEmitter = true;

        private Vignette gameplayVignette;
        private float baseVignetteIntensity;
        private float targetVignetteIntensity;
        private Color baseVignetteColor = Color.black;
        private Color targetVignetteColor = Color.black;
        private bool lowMoveVignetteActive;
        private bool torrentVisualActive;

        private static readonly Vector2Int[] CardinalDirections =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        public void Initialize()
        {
            SetupGameplayVignette();
        }

        public void SetInactiveImmediate()
        {
            lowMoveVignetteActive = false;
            torrentVisualActive = false;
            RecomputeVignetteTargets();

            if (gameplayVignette != null)
            {
                gameplayVignette.intensity.Override(targetVignetteIntensity);
                gameplayVignette.color.Override(targetVignetteColor);
            }

            UpdateTorrentParticleState();
        }

        public void SyncRunStateVisuals(GameManager.GamePhase currentPhase, RunStateController runState)
        {
            bool shouldBeActive =
                currentPhase == GameManager.GamePhase.Gameplay &&
                runState != null &&
                runState.IsTorrentActive;

            if (torrentVisualActive == shouldBeActive)
            {
                return;
            }

            torrentVisualActive = shouldBeActive;
            RecomputeVignetteTargets();
            UpdateTorrentParticleState();
        }

        public void UpdateVignette(float deltaTime)
        {
            if (gameplayVignette == null)
            {
                return;
            }

            float blend = Mathf.Max(0f, vignetteBlendSpeed);

            if (blend <= 0f)
            {
                gameplayVignette.intensity.Override(targetVignetteIntensity);
                gameplayVignette.color.Override(targetVignetteColor);
                return;
            }

            float lerpT = 1f - Mathf.Exp(-blend * Mathf.Max(0f, deltaTime));
            float next = Mathf.MoveTowards(
                gameplayVignette.intensity.value,
                targetVignetteIntensity,
                blend * Mathf.Max(0f, deltaTime));
            Color nextColor = Color.Lerp(gameplayVignette.color.value, targetVignetteColor, lerpT);

            gameplayVignette.intensity.Override(next);
            gameplayVignette.color.Override(nextColor);
        }

        public void Evaluate(
            GameManager.GamePhase currentPhase,
            TileBoardSystem tileBoardSystem,
            RunStateController runState,
            Vector2Int currentCell,
            int gridSize)
        {
            if (currentPhase != GameManager.GamePhase.Gameplay || tileBoardSystem == null)
            {
                SetLowMoveVignetteActive(false);
                return;
            }

            int validNeighborMoves = CountValidNeighborMoves(tileBoardSystem, currentCell, gridSize);
            SetLowMoveVignetteActive(validNeighborMoves <= Mathf.Max(0, lowMoveVignetteThreshold));

            if (runState == null || runState.IsGameOver)
            {
                return;
            }

            if (IsTrashCorruptionGameOver(tileBoardSystem, gridSize))
            {
                runState.TriggerSoftLockGameOver();
                SetLowMoveVignetteActive(true, immediate: true);
                return;
            }

            if (validNeighborMoves <= 0)
            {
                runState.TriggerSoftLockGameOver();
                SetLowMoveVignetteActive(true, immediate: true);
            }
        }

        private bool IsTrashCorruptionGameOver(TileBoardSystem tileBoardSystem, int gridSize)
        {
            float threshold = Mathf.Clamp01(trashCorruptionGameOverThreshold);

            if (threshold <= 0f)
            {
                return false;
            }

            int activeCells = 0;
            int trashCells = 0;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    TileType type = tileBoardSystem.GetTileType(new Vector2Int(x, y));

                    if (type == TileType.Rock || type == TileType.Filler)
                    {
                        continue;
                    }

                    activeCells++;

                    if (type == TileType.Trash)
                    {
                        trashCells++;
                    }
                }
            }

            if (activeCells <= 0)
            {
                return false;
            }

            float trashRatio = (float)trashCells / activeCells;
            return trashRatio >= threshold;
        }

        private int CountValidNeighborMoves(
            TileBoardSystem tileBoardSystem,
            Vector2Int cell,
            int gridSize)
        {
            int valid = 0;

            for (int i = 0; i < CardinalDirections.Length; i++)
            {
                Vector2Int next = cell + CardinalDirections[i];

                if (!IsInBounds(next, gridSize))
                {
                    continue;
                }

                if (tileBoardSystem.IsWalkable(next))
                {
                    valid++;
                }
            }

            return valid;
        }

        private static bool IsInBounds(Vector2Int cell, int gridSize)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < gridSize && cell.y < gridSize;
        }

        private void SetupGameplayVignette()
        {
            if (globalVolume == null)
            {
                Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);

                for (int i = 0; i < volumes.Length; i++)
                {
                    Volume volume = volumes[i];

                    if (volume != null && volume.isGlobal)
                    {
                        globalVolume = volume;
                        break;
                    }
                }
            }

            if (globalVolume == null)
            {
                return;
            }

            if (globalVolume.profile == null)
            {
                if (globalVolume.sharedProfile == null)
                {
                    return;
                }

                globalVolume.profile = Instantiate(globalVolume.sharedProfile);
            }

            VolumeProfile profile = globalVolume.profile;

            if (profile == null)
            {
                return;
            }

            if (!profile.TryGet(out gameplayVignette))
            {
                gameplayVignette = profile.Add<Vignette>(overrides: true);
                gameplayVignette.intensity.Override(0f);
            }

            gameplayVignette.active = true;
            baseVignetteIntensity = Mathf.Clamp01(gameplayVignette.intensity.value);
            baseVignetteColor = gameplayVignette.color.value;
            targetVignetteIntensity = baseVignetteIntensity;
            targetVignetteColor = baseVignetteColor;
            gameplayVignette.color.Override(baseVignetteColor);
        }

        private void SetLowMoveVignetteActive(bool active, bool immediate = false)
        {
            lowMoveVignetteActive = active;
            RecomputeVignetteTargets();

            if (immediate && gameplayVignette != null)
            {
                gameplayVignette.intensity.Override(targetVignetteIntensity);
                gameplayVignette.color.Override(targetVignetteColor);
            }
        }

        private void RecomputeVignetteTargets()
        {
            float lowMoveTarget = lowMoveVignetteActive
                ? Mathf.Clamp01(lowMoveVignetteIntensity)
                : baseVignetteIntensity;

            if (enableTorrentBlueVignette && torrentVisualActive)
            {
                targetVignetteIntensity = Mathf.Max(lowMoveTarget, Mathf.Clamp01(torrentVignetteIntensity));
                targetVignetteColor = torrentVignetteColor;
            }
            else
            {
                targetVignetteIntensity = lowMoveTarget;
                targetVignetteColor = baseVignetteColor;
            }
        }

        private void UpdateTorrentParticleState()
        {
            if (torrentParticleEmitter == null)
            {
                return;
            }

            var emission = torrentParticleEmitter.emission;
            bool shouldEnable = enableTorrentParticleEmitter && torrentVisualActive;
            emission.enabled = shouldEnable;

            if (shouldEnable)
            {
                if (!torrentParticleEmitter.isPlaying)
                {
                    torrentParticleEmitter.Play();
                }
            }
            else if (torrentParticleEmitter.isPlaying)
            {
                torrentParticleEmitter.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
