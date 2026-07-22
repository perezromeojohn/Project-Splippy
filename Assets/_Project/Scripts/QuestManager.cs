using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;

namespace projectsplippy
{
    public class QuestManager : MonoBehaviour
    {
        [Header("Quest Pool")]
        [SerializeField] private QuestDef[] questPool;
        [SerializeField] private RunStateController runState;

        [Header("Quest Slots (3)")]
        [SerializeField] private QuestSlotUI slot0;
        [SerializeField] private QuestSlotUI slot1;
        [SerializeField] private QuestSlotUI slot2;

        [Header("Progress Pop")]
        [SerializeField, Min(0.02f)] private float progressPopDuration = 0.15f;
        [SerializeField] private float progressPopScale = 1.1f;
        [SerializeField] private Ease progressPopEase = Ease.OutBack;

        [Header("Completion")]
        [SerializeField, Min(0.05f)] private float completePopDuration = 0.4f;
        [SerializeField] private float completePopScale = 1.18f;
        [SerializeField] private Ease completePopEase = Ease.OutBack;
        [SerializeField, Min(0.1f)] private float replaceDelay = 0.8f;

        [Header("Timeout Juice")]
        [SerializeField, Min(0.02f)] private float timeoutShrinkDuration = 0.2f;
        [SerializeField] private float timeoutShrinkScale = 0.85f;
        [SerializeField] private Ease timeoutShrinkEase = Ease.InBack;
        [SerializeField] private Vector3 timeoutShakeStrength = new Vector3(6f, 0f, 0f);
        [SerializeField, Min(0.02f)] private float timeoutShakeDuration = 0.25f;
        [SerializeField] private float timeoutShakeVibrato = 30f;
        [SerializeField, Min(0.1f)] private float timeoutReplaceDelay = 0.5f;

        [Header("Colors")]
        [SerializeField] private string progressColorHex = "8CD400FF"; // apple green, full RGBA for TMP
        [SerializeField] private string turnColorHex = "FF4444FF";    // red for turn countdown
        [SerializeField] private Color pointsColor = Color.white;

        private ActiveQuest[] active;
        private QuestSlotUI[] slots;

        /// <summary>
        /// Set by GameManager before HandleTurnResolved. When false, ClearLandfill quests
        /// won't be assigned because there are no Trash tiles on the board to clear.
        /// </summary>
        public bool ClearLandfillEligible { get; set; } = true;

        [System.Serializable]
        public class QuestSlotUI
        {
            public GameObject root;
            public TMP_Text descriptionText;
            public TMP_Text pointsText;
        }

        private class ActiveQuest
        {
            public QuestDef def;
            public int progress;
            public int turnsElapsed;
            public int scoreBaseline;
            public bool completed;
        }

        private void Awake()
        {
            active = new ActiveQuest[3];
            slots = new[] { slot0, slot1, slot2 };
        }

        private void Start()
        {
            for (int i = 0; i < 3; i++)
            {
                AssignRandomQuest(i);
            }
        }

        public void HandleTurnResolved(IReadOnlyList<TileStepResult> steps)
        {
            Debug.Log($"[QuestManager] HandleTurnResolved called. steps={(steps != null ? steps.Count.ToString() : "null")}, runState={(runState != null ? "wired" : "NULL")}, isGameOver={runState?.IsGameOver ?? false}");

            if (runState == null || runState.IsGameOver)
            {
                return;
            }

            int tilesCleared = steps?.Count ?? 0;
            int sanitationCleared = 0;
            int pathLength = tilesCleared;
            int maxCropStreak = 0;
            bool torrentActivated = runState.ConsumeTorrentActivationFlag();
            int landfillClears = runState.ConsumeHazardLandfillClears();

            if (steps != null && steps.Count > 0)
            {
                int currentStreak = 0;
                int? lastCrop = null;

                for (int i = 0; i < steps.Count; i++)
                {
                    TileType t = steps[i].EnteredType;
                    if (t == TileType.Sanitation || t == TileType.WorstSanitation)
                    {
                        sanitationCleared++;
                    }

                    int? crop = steps[i].EnteredCropVariantIndex >= 0
                        ? steps[i].EnteredCropVariantIndex
                        : (int?)null;

                    if (crop.HasValue && lastCrop.HasValue && crop.Value == lastCrop.Value)
                    {
                        currentStreak++;
                    }
                    else if (crop.HasValue)
                    {
                        currentStreak = 1;
                    }
                    else
                    {
                        currentStreak = 0;
                    }

                    lastCrop = crop;

                    if (currentStreak > maxCropStreak)
                    {
                        maxCropStreak = currentStreak;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                ActiveQuest q = active[i];
                if (q == null || q.def == null || q.completed)
                {
                    continue;
                }

                q.turnsElapsed++;
                int previousProgress = q.progress;

                switch (q.def.category)
                {
                    case QuestCategory.ClearTiles:
                        q.progress += tilesCleared;
                        break;
                    case QuestCategory.ClearSanitation:
                        q.progress += sanitationCleared;
                        break;
                    case QuestCategory.LongPath:
                        if (pathLength > q.progress)
                        {
                            q.progress = pathLength;
                        }
                        break;
                    case QuestCategory.ActivateTorrent:
                        if (torrentActivated)
                        {
                            q.progress++;
                        }
                        break;
                    case QuestCategory.ReachScore:
                        q.progress = runState.CurrentScore - q.scoreBaseline;
                        break;
                    case QuestCategory.CropStreak:
                        if (maxCropStreak > q.progress)
                        {
                            q.progress = maxCropStreak;
                        }
                        break;
                    case QuestCategory.ClearLandfill:
                        q.progress += landfillClears;
                        break;
                }

                bool progressChanged = q.progress != previousProgress;

                if (q.def.turnLimit > 0 && q.turnsElapsed > q.def.turnLimit)
                {
                    FailQuest(i);
                    continue;
                }

                int displayProgress = Mathf.Min(q.progress, q.def.targetCount);

                if (q.progress >= q.def.targetCount)
                {
                    CompleteQuest(i);
                }
                else
                {
                    RefreshSlot(i, displayProgress);

                    if (progressChanged)
                    {
                        QuestSlotUI slot = slots[i];
                        if (slot != null && slot.root != null)
                        {
                            Tween.CompleteAll(slot.root.transform);
                            PlayProgressPop(slot.root.transform);
                        }
                    }
                }
            }
        }

        private void PlayProgressPop(Transform target)
        {
            if (target == null || progressPopDuration <= 0f)
            {
                return;
            }

            Tween.Scale(
                target,
                progressPopScale,
                progressPopDuration,
                progressPopEase,
                cycles: 2,
                cycleMode: CycleMode.Yoyo);
        }

        private void CompleteQuest(int index)
        {
            ActiveQuest q = active[index];
            if (q == null || q.completed)
            {
                return;
            }

            q.completed = true;
            int reward = q.def.scoreReward;

            if (runState != null)
            {
                runState.ApplyEconomyAndScore(0, reward);
            }

            RefreshSlot(index, q.def.targetCount);

            QuestSlotUI slot = slots[index];
            if (slot != null && slot.root != null)
            {
                Tween.CompleteAll(slot.root.transform);
                Tween.Scale(
                    slot.root.transform,
                    completePopScale,
                    completePopDuration,
                    completePopEase,
                    cycles: 2,
                    cycleMode: CycleMode.Yoyo);
            }

            StartCoroutine(ReplaceAfterDelay(index, replaceDelay));
        }

        private IEnumerator ReplaceAfterDelay(int index, float delay)
        {
            yield return new WaitForSeconds(delay);
            AssignRandomQuest(index);
        }

        private void FailQuest(int index)
        {
            QuestSlotUI slot = slots[index];
            Transform target = slot != null ? slot.root?.transform : null;

            PlayTimeoutJuice(target);
            active[index] = null;

            StartCoroutine(FailQuestRoutine(index, slot));
        }

        private IEnumerator FailQuestRoutine(int index, QuestSlotUI slot)
        {
            // Let the juice play before hiding
            float juiceDuration = Mathf.Max(timeoutShakeDuration, timeoutShrinkDuration);
            yield return new WaitForSeconds(juiceDuration);

            if (slot != null && slot.root != null)
            {
                slot.root.SetActive(false);
            }

            if (timeoutReplaceDelay > juiceDuration)
            {
                yield return new WaitForSeconds(timeoutReplaceDelay - juiceDuration);
            }

            AssignRandomQuest(index);
        }

        private void PlayTimeoutJuice(Transform target)
        {
            if (target == null)
            {
                return;
            }

            Tween.CompleteAll(target);

            if (timeoutShakeDuration > 0f)
            {
                target.localPosition = Vector3.zero;
                Tween.ShakeLocalPosition(
                    target,
                    timeoutShakeStrength,
                    timeoutShakeDuration,
                    timeoutShakeVibrato);
            }

            if (timeoutShrinkDuration > 0f)
            {
                Tween.Scale(
                    target,
                    timeoutShrinkScale,
                    timeoutShrinkDuration,
                    timeoutShrinkEase);
            }
        }

        private void AssignRandomQuest(int index)
        {
            if (questPool == null || questPool.Length == 0)
            {
                active[index] = null;
                QuestSlotUI slot = slots[index];
                if (slot != null && slot.root != null)
                {
                    slot.root.SetActive(false);
                }
                return;
            }

            // Gather eligible defs: exclude quests already active in other slots + ClearLandfill if not eligible
            var eligible = new List<QuestDef>(questPool.Length);

            for (int p = 0; p < questPool.Length; p++)
            {
                QuestDef candidate = questPool[p];
                if (candidate == null)
                {
                    continue;
                }

                // Skip ClearLandfill if no trash tiles on board
                if (candidate.category == QuestCategory.ClearLandfill && !ClearLandfillEligible)
                {
                    continue;
                }

                // Uniqueness: skip if already active in another slot
                bool duplicate = false;

                for (int s = 0; s < 3; s++)
                {
                    if (s == index)
                    {
                        continue;
                    }

                    if (active[s] != null && active[s].def == candidate)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    eligible.Add(candidate);
                }
            }

            // Fallback: if filtering removed everything, use the full pool
            if (eligible.Count == 0)
            {
                eligible.AddRange(questPool);

                // Still respect ClearLandfill eligibility even in fallback
                if (!ClearLandfillEligible)
                {
                    eligible.RemoveAll(d => d != null && d.category == QuestCategory.ClearLandfill);
                }
            }

            QuestDef def = eligible[Random.Range(0, eligible.Count)];
            active[index] = new ActiveQuest
            {
                def = def,
                progress = 0,
                turnsElapsed = 0,
                scoreBaseline = runState != null ? runState.CurrentScore : 0,
                completed = false
            };

            QuestSlotUI ui = slots[index];
            if (ui != null && ui.root != null)
            {
                Tween.CompleteAll(ui.root.transform);
                ui.root.transform.localScale = Vector3.one;
                ui.root.SetActive(true);
            }

            RefreshSlot(index, 0);
        }

        private void RefreshSlot(int index, int progress)
        {
            QuestSlotUI slot = slots[index];
            ActiveQuest q = active[index];
            if (q == null || q.def == null || slot == null || slot.root == null)
            {
                return;
            }

            if (slot.descriptionText != null)
            {
                string label = q.def.GetLabel();
                string turnPart = q.def.turnLimit > 0
                    ? $"  <color=#{turnColorHex}>in {q.def.turnLimit - q.turnsElapsed + 1} turns</color>"
                    : "";
                slot.descriptionText.text =
                    $"<color=white>{label}</color>  <color=#{progressColorHex}>{progress}/{q.def.targetCount}</color>{turnPart}";
            }

            if (slot.pointsText != null)
            {
                slot.pointsText.text = $"{q.def.scoreReward}pts";
            }
        }
    }
}
