using System.Collections.Generic;
using System.Linq;
using RimToll.AI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimToll
{
	public class IncidentWorker_PunitiveRaid : IncidentWorker
	{
		public const float ResentmentThreshold = 0.9f;

		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			if (RimTollMod.Settings != null && !RimTollMod.Settings.punitiveEvent) return false;
			if (!TollUtility.AnyActiveBooth(map)) return false;
			if (GameComponent_TollState.Instance?.MostResentfulFaction(ResentmentThreshold) == null) return false;
			return FindRaiderFaction() != null;
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			Faction client = comp?.MostResentfulFaction(ResentmentThreshold);
			if (client == null) return false;
			Faction raiders = FindRaiderFaction();
			if (raiders == null) return false;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Hostile)) return false;
			if (TollUtility.FindLoot(map, entry, 9999f) == null) return false;

			float resentment = comp.ResentmentFor(client);
			float points = StorytellerUtility.DefaultThreatPointsNow(map)
				* Mathf.Clamp(0.25f + resentment * 0.2f, 0.25f, 0.6f);

			var groupParms = new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Combat,
				tile = map.Tile,
				faction = raiders,
				points = Mathf.Max(points, 120f),
				generateFightersOnly = true,
			};
			List<Pawn> thieves = PawnGroupMakerUtility.GeneratePawns(groupParms).ToList();
			if (thieves.Count == 0) return false;

			for (int i = 0; i < thieves.Count; i++)
			{
				GenSpawn.Spawn(thieves[i], CellFinder.RandomClosewalkCellNear(entry, map, 8), map);
			}
			LordMaker.MakeNewLord(raiders, new LordJob_TollTheft(), map, thieves);

			SendStandardLetter("RimToll.PunitiveLabel".Translate(client.Name),
				"RimToll.PunitiveText".Translate(client.Name, raiders.Name),
				LetterDefOf.ThreatSmall, parms, new LookTargets(thieves[0]));

			comp.Notify_PunitiveSent(client);
			Find.TickManager.Notify_GeneratedPotentiallyHostileMap();
			return true;
		}

		private Faction FindRaiderFaction()
		{
			var candidates = new List<Faction>();
			foreach (Faction f in Find.FactionManager.AllFactionsVisible)
			{
				if (f.IsPlayer || f.defeated || f.Hidden) continue;
				if (!f.HostileTo(Faction.OfPlayer)) continue;
				if (!f.def.humanlikeFaction || f.def.pawnGroupMakers == null) continue;
				if (!f.def.pawnGroupMakers.Any(g => g.kindDef == PawnGroupKindDefOf.Combat)) continue;
				candidates.Add(f);
			}
			return candidates.Count == 0 ? null : candidates.RandomElement();
		}
	}
}
