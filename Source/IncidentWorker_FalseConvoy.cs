using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimToll
{
	public class IncidentWorker_FalseConvoy : IncidentWorker
	{
		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			if (RimTollMod.Settings != null && !RimTollMod.Settings.falseConvoyEvent) return false;
			if (!TollUtility.AnyActiveBooth(map)) return false;
			return FindCover() != null;
		}

		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if (!(parms.target is Map map)) return false;
			Faction cover = FindCover();
			if (cover == null) return false;
			if (!RCellFinder.TryFindRandomPawnEntryCell(out IntVec3 entry, map, CellFinder.EdgeRoadChance_Neutral)) return false;
			Building booth = TollUtility.NearestBooth(map, entry);
			if (booth == null) return false;
			if (!RCellFinder.TryFindTravelDestFrom(entry, map, out IntVec3 dest)) return false;

			var groupParms = new PawnGroupMakerParms
			{
				groupKind = PawnGroupKindDefOf.Peaceful,
				tile = map.Tile,
				faction = cover,
				points = Rand.Range(180f, 420f),
			};
			List<Pawn> band = PawnGroupMakerUtility.GeneratePawns(groupParms, warnOnZeroResults: false).ToList();
			band.RemoveAll(p => p == null || !p.RaceProps.Humanlike);
			if (band.Count < 3)
			{
				for (int i = 0; i < band.Count; i++) band[i].Destroy();
				return false;
			}

			for (int i = 0; i < band.Count; i++)
			{
				GenSpawn.Spawn(band[i], CellFinder.RandomClosewalkCellNear(entry, map, 6), map);
			}

			GameComponent_TollState.SuppressPatch = true;
			Lord lord;
			try
			{
				lord = LordMaker.MakeNewLord(cover, new LordJob_TravelAndExit(dest), map, band);
			}
			finally
			{
				GameComponent_TollState.SuppressPatch = false;
			}
			GameComponent_TollState.Instance?.OpenCase(lord, booth, Rand.RangeInclusive(TollPass.FakeName, TollPass.FakeSeal));
			return true;
		}

		private Faction FindCover()
		{
			var candidates = new List<Faction>();
			foreach (Faction f in Find.FactionManager.AllFactionsVisible)
			{
				if (f.IsPlayer || f.defeated || f.Hidden) continue;
				if (f.HostileTo(Faction.OfPlayer)) continue;
				if (!f.def.humanlikeFaction || f.def.pawnGroupMakers == null) continue;
				if (!f.def.pawnGroupMakers.Any(g => g.kindDef == PawnGroupKindDefOf.Peaceful)) continue;
				candidates.Add(f);
			}
			return candidates.Count == 0 ? null : candidates.RandomElement();
		}
	}
}
