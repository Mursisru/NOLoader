using System.Collections.Generic;
using NOLoader.HudCommon;

namespace NOLoader.MissileEta
{
    /// <summary>
    /// Distinguishes missiles launched without a target (NOTR) from those that lost track later (LOST).
    /// </summary>
    internal static class MissileTargetTracker
    {
        private static readonly Dictionary<int, bool> EverHadTarget = new Dictionary<int, bool>();

        internal static void Note(Missile missile)
        {
            if (missile == null)
                return;

            if (missile.targetID.IsValid)
                EverHadTarget[missile.GetInstanceID()] = true;
        }

        internal static bool WasLaunchedWithoutTarget(Missile missile)
        {
            if (missile == null || missile.targetID.IsValid)
                return false;

            int id = missile.GetInstanceID();
            return !EverHadTarget.TryGetValue(id, out bool hadTarget) || !hadTarget;
        }

        internal static void Prune(HashSet<int> alive)
        {
            var remove = new List<int>();
            foreach (int key in EverHadTarget.Keys)
            {
                if (!alive.Contains(key))
                    remove.Add(key);
            }

            for (int i = 0; i < remove.Count; i++)
                EverHadTarget.Remove(remove[i]);
        }
    }
}
