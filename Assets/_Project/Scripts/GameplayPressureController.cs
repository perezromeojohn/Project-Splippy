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

        private Vignette gameplayVignette;
        private float baseVignetteIntensity;
        private float targetVignetteIntensity;

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
            SetLowMoveVignetteActive(false, immediate: true);
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
                return;
            }

            float next = Mathf.MoveTowards(
                gameplayVignette.intensity.value,
                targetVignetteIntensity,
                blend * Mathf.Max(0f, deltaTime));

            gameplayVignette.intensity.Override(next);
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

            if (validNeighborMoves <= 0)
            {
                runState.TriggerSoftLockGameOver();
                SetLowMoveVignetteActive(true, immediate: true);
            }
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
            targetVignetteIntensity = baseVignetteIntensity;
        }

        private void SetLowMoveVignetteActive(bool active, bool immediate = false)
        {
            targetVignetteIntensity = active
                ? Mathf.Clamp01(lowMoveVignetteIntensity)
                : baseVignetteIntensity;

            if (immediate && gameplayVignette != null)
            {
                gameplayVignette.intensity.Override(targetVignetteIntensity);
            }
        }
    }
}
