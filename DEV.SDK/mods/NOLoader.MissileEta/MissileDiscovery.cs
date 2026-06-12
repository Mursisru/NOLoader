using System.Collections.Generic;
using NOLoader.HudCommon;
using Mirage.Serialization;
using UnityEngine;

namespace NOLoader.MissileEta
{
    internal static class MissileDiscovery
    {
        internal static void CollectOwnMissiles(Aircraft aircraft, List<Missile> buffer)
        {
            buffer.Clear();
            if (aircraft == null || aircraft.disabled)
                return;

            PersistentID ownerId = aircraft.persistentID;
            List<Unit> units = UnitRegistry.allUnits;
            for (int i = 0; i < units.Count; i++)
            {
                if (!(units[i] is Missile missile))
                    continue;
                if (missile.disabled || missile.rb == null)
                    continue;
                if (missile.ownerID != ownerId && missile.owner != aircraft)
                    continue;
                TryAdd(buffer, missile);
            }

            MissileEtaDiagLog.VerboseLine($"CollectOwn: matched={buffer.Count} aircraftId={ownerId}");
        }

        /// <summary>
        /// Incoming only from aircraft MWS (СПО) — same list as vanilla missile warning / RWR track.
        /// No UnitRegistry scan (anti-wallhack).
        /// </summary>
        internal static void CollectIncomingMissiles(
            Aircraft aircraft,
            MissileWarning warning,
            List<Missile> buffer)
        {
            buffer.Clear();
            if (aircraft == null || aircraft.disabled)
                return;

            if (warning == null || warning.knownMissiles == null)
            {
                MissileEtaDiagLog.VerboseLine("CollectIncoming: no MWS or knownMissiles=null");
                return;
            }

            PersistentID selfId = aircraft.persistentID;
            for (int i = 0; i < warning.knownMissiles.Count; i++)
            {
                Missile m = warning.knownMissiles[i];
                if (IsIncoming(m, selfId))
                    TryAdd(buffer, m);
            }

            MissileEtaDiagLog.VerboseLine(
                $"CollectIncoming: mwsKnown={warning.knownMissiles.Count} tracked={buffer.Count} (MWS-only)");
        }

        internal static void LogRegistrySnapshot(Aircraft aircraft)
        {
            if (!MissileEtaDiagLog.Verbose || aircraft == null)
                return;

            PersistentID selfId = aircraft.persistentID;
            MissileWarning warning = aircraft.GetMissileWarningSystem();
            int mws = warning?.knownMissiles?.Count ?? 0;
            MissileEtaDiagLog.VerboseLine($"RegistrySnapshot: mwsKnown={mws} (incoming uses MWS only)");
        }

        private static bool IsIncoming(Missile missile, PersistentID selfId)
        {
            return missile != null
                && !missile.disabled
                && missile.rb != null
                && missile.targetID == selfId;
        }

        private static void TryAdd(List<Missile> buffer, Missile missile)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                if (buffer[i] == missile)
                    return;
            }
            buffer.Add(missile);
        }
    }
}
