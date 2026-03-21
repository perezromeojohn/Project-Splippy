using UnityEngine;
using PrimeTween;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace projectsplippy
{
    public class GameManager : MonoBehaviour
    {
        private enum GroundType
        {
            Farmland,
            Ecosystem,
            Sanitation,
            Marine
        }

        [System.Serializable]
        private struct GroundTypeMaterial
        {
            public GroundType type;
            public Material material;
        }

        [Header("Grid")]
        [SerializeField] private int gridSize = 7;
        [SerializeField] private float cellSize = 1f;
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string playerMapName = "Player";
        [SerializeField] private string moveTowardsActionName = "MoveTowards";

        [Header("Ground")]
        [SerializeField] private bool buildGroundOnAwake = true;
        [SerializeField] private Transform groundParent;
        [SerializeField] private GameObject groundTilePrefab;
        [SerializeField] private float tileHeight = 0.2f;
        [SerializeField] private float tileYOffset = -0.1f;
        [SerializeField] private GroundTypeMaterial[] groundMaterials;

        [Header("Player")]
        [SerializeField] private Transform splippy;
        [SerializeField] private float moveDurationPerCell = 0.12f;
        [SerializeField] private float splippyHeightOffset = 0.6f;
        [SerializeField] private float hopHeight = 0.3f;
        [SerializeField] private Ease hopUpEase = Ease.OutQuad;
        [SerializeField] private Ease hopDownEase = Ease.InQuad;
        [SerializeField] private float splippyStretchAmount = 0.12f;
        [SerializeField] private float splippySquashAmount = 0.14f;
        [SerializeField] private float landingSettleDuration = 0.06f;
        [SerializeField] private Ease landingSettleEase = Ease.OutBack;

        [Header("Feedback")]
        [SerializeField] private float tileTapScaleMultiplier = 1.08f;
        [SerializeField] private float tileTapDuration = 0.1f;
        [SerializeField] private float tileLandingScaleMultiplier = 0.9f;
        [SerializeField] private float tileLandingDuration = 0.08f;

        private Camera mainCamera;
        private InputAction moveTowardsAction;
        private Vector2Int currentCell;
        private bool isMoving;
        private readonly Dictionary<GroundType, Material> materialByType = new Dictionary<GroundType, Material>();
        private readonly Dictionary<Vector2Int, Transform> tileByCell = new Dictionary<Vector2Int, Transform>();
        private readonly Dictionary<Vector2Int, Vector3> tileBaseScaleByCell = new Dictionary<Vector2Int, Vector3>();
        private Vector3 splippyBaseScale = Vector3.one;

        private void Awake()
        {
            mainCamera = Camera.main;

            if (mainCamera == null)
            {
                enabled = false;
                return;
            }

            if (splippy == null)
            {
                splippy = transform;
            }

            splippyBaseScale = splippy.localScale;

            gridSize = Mathf.Max(1, gridSize);
            cellSize = Mathf.Max(0.1f, cellSize);
            tileHeight = Mathf.Max(0.05f, tileHeight);

            CacheGroundMaterials();
            SetupInput();

            currentCell = new Vector2Int(gridSize / 2, gridSize / 2);
            splippy.position = CellToWorldForSplippy(currentCell);

            if (buildGroundOnAwake)
            {
                BuildGroundGrid();
            }
        }

        private void OnEnable()
        {
            moveTowardsAction?.Enable();
        }

        private void OnDisable()
        {
            moveTowardsAction?.Disable();
        }

        private void Update()
        {
            if (isMoving)
            {
                return;
            }

            if (moveTowardsAction == null || !moveTowardsAction.WasPressedThisFrame())
            {
                return;
            }

            if (!TryGetPointerScreenPosition(out Vector2 pointerScreenPosition))
            {
                return;
            }

            if (!TryGetClickedCell(pointerScreenPosition, out Vector2Int clickedCell))
            {
                return;
            }

            PlayTileTapFeedback(clickedCell);

            if (clickedCell == currentCell)
            {
                return;
            }

            MoveToCell(clickedCell);
        }

        private void SetupInput()
        {
            if (inputActions == null)
            {
                return;
            }

            InputActionMap map = inputActions.FindActionMap(playerMapName, throwIfNotFound: false);

            if (map == null)
            {
                return;
            }

            moveTowardsAction = map.FindAction(moveTowardsActionName, throwIfNotFound: false);

            if (moveTowardsAction == null)
            {
                return;
            }

            moveTowardsAction.Enable();
        }

        private bool TryGetPointerScreenPosition(out Vector2 screenPosition)
        {
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }

            screenPosition = default;
            return false;
        }

        private bool TryGetClickedCell(Vector2 screenPosition, out Vector2Int cell)
        {
            cell = default;

            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            Vector3 gridCenter = GetGridCenterWorld();
            Plane gridPlane = new Plane(Vector3.up, gridCenter);

            if (!gridPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            Vector3 worldHit = ray.GetPoint(enter);
            Vector3 local = worldHit - gridCenter;
            float halfSpan = (gridSize - 1) * 0.5f;

            int x = Mathf.RoundToInt((local.x / cellSize) + halfSpan);
            int y = Mathf.RoundToInt((local.z / cellSize) + halfSpan);

            if (!IsInBounds(x, y))
            {
                return false;
            }

            cell = new Vector2Int(x, y);
            return true;
        }

        private void CacheGroundMaterials()
        {
            materialByType.Clear();

            for (int i = 0; i < groundMaterials.Length; i++)
            {
                GroundTypeMaterial entry = groundMaterials[i];

                if (entry.material == null)
                {
                    continue;
                }

                materialByType[entry.type] = entry.material;
            }
        }

        public void BuildGroundGrid()
        {
            tileByCell.Clear();
            tileBaseScaleByCell.Clear();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    GroundType type = GetInitialGroundType(cell);
                    Material material = GetMaterialForType(type);

                    GameObject tile = CreateGroundTile();
                    tile.name = $"Tile_{x}_{y}_{type}";
                    tile.transform.SetParent(groundParent, true);

                    Vector3 tilePosition = CellToWorld(cell) + new Vector3(0f, tileYOffset, 0f);
                    tile.transform.position = tilePosition;
                    tile.transform.localScale = new Vector3(cellSize, tileHeight, cellSize);
                    tileByCell[cell] = tile.transform;
                    tileBaseScaleByCell[cell] = tile.transform.localScale;

                    Renderer renderer = tile.GetComponent<Renderer>();

                    if (renderer != null && material != null)
                    {
                        renderer.sharedMaterial = material;
                    }
                }
            }
        }

        private GameObject CreateGroundTile()
        {
            if (groundTilePrefab != null)
            {
                return Instantiate(groundTilePrefab);
            }

            return GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        private GroundType GetInitialGroundType(Vector2Int cell)
        {
            int pick = Mathf.Abs((cell.x * 73856093) ^ (cell.y * 19349663)) % 4;
            return (GroundType)pick;
        }

        private Material GetMaterialForType(GroundType type)
        {
            materialByType.TryGetValue(type, out Material material);
            return material;
        }

        private void MoveToCell(Vector2Int targetCell)
        {
            if (!GridPathfinder.TryFindPathBfs(gridSize, currentCell, targetCell, IsCellWalkable, out List<Vector2Int> path))
            {
                return;
            }

            if (path.Count <= 1)
            {
                return;
            }

            isMoving = true;
            MoveAlongPath(path, 1);
        }

        private void MoveAlongPath(List<Vector2Int> path, int index)
        {
            if (index >= path.Count)
            {
                isMoving = false;
                return;
            }

            Vector2Int nextCell = path[index];
            Vector3 start = splippy.position;
            Vector3 end = CellToWorldForSplippy(nextCell);
            Vector3 apex = Vector3.Lerp(start, end, 0.5f);
            apex.y = Mathf.Max(start.y, end.y) + hopHeight;

            float halfDuration = Mathf.Max(0.01f, moveDurationPerCell * 0.5f);
            Vector3 stretchScale = new Vector3(
                splippyBaseScale.x * (1f - splippyStretchAmount * 0.5f),
                splippyBaseScale.y * (1f + splippyStretchAmount),
                splippyBaseScale.z * (1f - splippyStretchAmount * 0.5f));
            Vector3 squashScale = new Vector3(
                splippyBaseScale.x * (1f + splippySquashAmount),
                splippyBaseScale.y * (1f - splippySquashAmount),
                splippyBaseScale.z * (1f + splippySquashAmount));

            Tween.Scale(splippy, stretchScale, halfDuration, hopUpEase);
            Tween.Position(splippy, apex, halfDuration, hopUpEase)
                .OnComplete(() =>
                {
                    Tween.Scale(splippy, squashScale, halfDuration, hopDownEase);
                    Tween.Position(splippy, end, halfDuration, hopDownEase)
                        .OnComplete(() =>
                        {
                            currentCell = nextCell;
                            PlayTileLandingFeedback(nextCell);
                            Tween.Scale(splippy, splippyBaseScale, landingSettleDuration, landingSettleEase);
                            MoveAlongPath(path, index + 1);
                        });
                });
        }

        private void PlayTileTapFeedback(Vector2Int cell)
        {
            if (!tileByCell.TryGetValue(cell, out Transform tileTransform))
            {
                return;
            }

            if (!tileBaseScaleByCell.TryGetValue(cell, out Vector3 baseScale))
            {
                baseScale = tileTransform.localScale;
                tileBaseScaleByCell[cell] = baseScale;
            }

            float clampedDuration = Mathf.Max(0.01f, tileTapDuration * 0.5f);
            Vector3 targetScale = baseScale * tileTapScaleMultiplier;

            Tween.Scale(tileTransform, targetScale, clampedDuration, cycles: 2, cycleMode: CycleMode.Yoyo);
        }

        private void PlayTileLandingFeedback(Vector2Int cell)
        {
            if (!tileByCell.TryGetValue(cell, out Transform tileTransform))
            {
                return;
            }

            if (!tileBaseScaleByCell.TryGetValue(cell, out Vector3 baseScale))
            {
                baseScale = tileTransform.localScale;
                tileBaseScaleByCell[cell] = baseScale;
            }

            float clampedDuration = Mathf.Max(0.01f, tileLandingDuration * 0.5f);
            Vector3 targetScale = baseScale * tileLandingScaleMultiplier;
            Tween.Scale(tileTransform, targetScale, clampedDuration, cycles: 2, cycleMode: CycleMode.Yoyo);
        }

        private bool IsCellWalkable(Vector2Int cell)
        {
            return IsInBounds(cell.x, cell.y);
        }

        private bool IsInBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < gridSize && y < gridSize;
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            float halfSpan = (gridSize - 1) * 0.5f;
            float worldX = (cell.x - halfSpan) * cellSize;
            float worldZ = (cell.y - halfSpan) * cellSize;
            return GetGridCenterWorld() + new Vector3(worldX, 0f, worldZ);
        }

        private Vector3 CellToWorldForSplippy(Vector2Int cell)
        {
            Vector3 basePos = CellToWorld(cell);
            return basePos + new Vector3(0f, tileYOffset + (tileHeight * 0.5f) + splippyHeightOffset, 0f);
        }

        private Vector3 GetGridCenterWorld()
        {
            return transform.position + gridOrigin;
        }

        // just for visuals so I can see stuff
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 1f);
            float halfSpan = (gridSize - 1) * 0.5f;
            Vector3 gridCenter = GetGridCenterWorld();

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    float worldX = (x - halfSpan) * cellSize;
                    float worldZ = (y - halfSpan) * cellSize;
                    Vector3 center = gridCenter + new Vector3(worldX, 0f, worldZ);
                    Gizmos.DrawWireCube(center, new Vector3(cellSize, 0.02f, cellSize));
                }
            }
        }
    }
}
