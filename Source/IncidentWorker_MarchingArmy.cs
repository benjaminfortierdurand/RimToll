using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimToll
{
	public class IncidentWorker_MarchingArmy : IncidentWorker
	{
		private const int MaxSoldiers = 16;

		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			if (RimTollMod.Settings != null && !RimTollMod.Settings.armyEvent) return false;
			if (!TollUtility.AnyActiveBooth(map)) return false;
			return TryFindFactions(map, out _, out _);
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			if (!TryFindFactions(map, out Faction army, out Faction target)) return false;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Neutral)) return false;
			Building booth = TollUtility.NearestBooth(map, entry);
			if (booth == null) return false;
			if (!RCellFinder.TryFindTravelDestFrom(entry, map, out IntVec3 dest)) return false;

			var groupParms = new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Combat,
				tile = map.Tile,
				faction = army,
				points = Rand.Range(1000f, 2200f),
				generateFightersOnly = true,
			};
			List<Pawn> soldiers = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
			if (soldiers.Count == 0) return false;
			if (soldiers.Count > MaxSoldiers)
			{
				for (int i = soldiers.Count - 1; i >= MaxSoldiers; i--)
				{
					soldiers[i].Destroy();
					soldiers.RemoveAt(i);
				}
			}

			Pawn general = soldiers[0];
			for (int i = 1; i < soldiers.Count; i++)
			{
				if (soldiers[i].kindDef.combatPower > general.kindDef.combatPower) general = soldiers[i];
			}

			for (int i = 0; i < soldiers.Count; i++)
			{
				GenSpawn.Spawn(soldiers[i], CellFinder.RandomClosewalkCellNear(entry, map, 8), map);
			}

			GameComponent_TollState.SuppressPatch = true;
			Lord lord;
			try
			{
				lord = LordMaker.MakeNewLord(army, new LordJob_TravelAndExit(dest), map, soldiers);
			}
			finally
			{
				GameComponent_TollState.SuppressPatch = false;
			}
			GameComponent_TollState.Instance?.OpenArmyCase(lord, booth, target, general);
			return true;
		}

		private bool TryFindFactions(Map map, out Faction army, out Faction target)
		{
			army = null;
			target = null;
			var candidates = new List<Faction>();
			foreach (Faction f in Find.FactionManager.AllFactionsVisible)
			{
				if (f.IsPlayer || f.defeated || f.Hidden) continue;
				if (f.HostileTo(Faction.OfPlayer)) continue;
				if (f.def.pawnGroupMakers == null) continue;
				if (!f.def.humanlikeFaction) continue;
				if (!f.def.pawnGroupMakers.Any(g => g.kindDef == PawnGroupKindDefOf.Combat)) continue;
				candidates.Add(f);
			}
			candidates.Shuffle();
			for (int i = 0; i < candidates.Count; i++)
			{
				Faction enemy = FindEnemyOf(candidates[i]);
				if (enemy == null) continue;
				army = candidates[i];
				target = enemy;
				return true;
			}
			return false;
		}

		private Faction FindEnemyOf(Faction f)
		{
			var enemies = new List<Faction>();
			foreach (Faction other in Find.FactionManager.AllFactionsVisible)
			{
				if (other == f || other.IsPlayer || other.defeated || other.Hidden) continue;
				if (!f.HostileTo(other)) continue;
				enemies.Add(other);
			}
			return enemies.Count == 0 ? null : enemies.RandomElement();
		}
	}
}
