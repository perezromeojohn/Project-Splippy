using UnityEngine;

namespace projectsplippy
{
    public enum QuestCategory
    {
        ClearTiles,      // Clear X tiles total
        ClearSanitation, // Clear X sanitation/worst-sanitation tiles
        ReachScore,      // Gain X score from quest start
        LongPath,        // Draw a path of X+ tiles in a single turn
        ActivateTorrent, // Activate torrent mode
        CropStreak,      // Chain X same-crop farmland tiles consecutively in one turn
        ClearLandfill    // Clear X trash/landfill tiles via marine or splash effects
    }

    [CreateAssetMenu(menuName = "Splippy/Quest")]
    public class QuestDef : ScriptableObject
    {
        public QuestCategory category;
        [Min(1)] public int targetCount = 5;
        [Min(0)] public int turnLimit; // 0 = no time limit
        [Min(0)] public int scoreReward = 100;

        public string GetLabel()
        {
            switch (category)
            {
                case QuestCategory.ClearTiles:
                    return $"Clear {targetCount} tiles from the land";
                case QuestCategory.ClearSanitation:
                    return $"Purify {targetCount} polluted tiles";
                case QuestCategory.ReachScore:
                    return turnLimit > 0
                        ? $"Earn {targetCount} points in {turnLimit} turns"
                        : $"Earn {targetCount} points";
                case QuestCategory.LongPath:
                    return $"Draw a path through {targetCount} tiles in one move";
                case QuestCategory.ActivateTorrent:
                    return targetCount > 1
                        ? $"Unleash torrent {targetCount}&#x00D7;"
                        : "Unleash the torrent";
                case QuestCategory.CropStreak:
                    return $"Chain {targetCount} of the same crop in a row";
                case QuestCategory.ClearLandfill:
                    return $"Clear {targetCount} landfill tiles with effects";
                default:
                    return "Quest";
            }
        }
    }
}
