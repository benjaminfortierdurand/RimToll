using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimToll.HarmonyPatches
{
	[HarmonyPatch(typeof(LordMaker), nameof(LordMaker.MakeNewLord))]
	public static class Patch_LordMaker_MakeNewLord
	{
		private const string MarketShoppersJob = "RimMarket.Market.LordJob_MarketShoppers";

		public static void Postfix(Lord __result, Faction faction, LordJob lordJob, Map map)
		{
			if (Current.ProgramState != ProgramState.Playing) return;
			if (GameComponent_TollState.SuppressPatch) return;
			if (__result == null || map == null || !map.IsPlayerHome) return;
			RimTollSettings s = RimTollMod.Settings;
			if (lordJob is LordJob_TravelAndExit)
			{
				if (s != null && !s.tollTravelers) return;
			}
			else if (lordJob is LordJob_VisitColony)
			{
				if (s != null && !s.tollVisitors) return;
			}
			else if (lordJob is LordJob_TradeWithColony)
			{
				if (s != null && !s.tollTraders) return;
			}
			else if (lordJob is LordJob_BegForItems)
			{
				if (s != null && !s.tollBeggars) return;
			}
			else if (lordJob?.GetType().FullName == MarketShoppersJob)
			{
				if (s != null && !s.tollShoppers) return;
			}
			else
			{
				return;
			}
			if (faction == null || faction.IsPlayer || faction.HostileTo(Faction.OfPlayer)) return;
			if (__result.ownedPawns.Count == 0)
			{
				GameComponent_TollState.Instance?.QueueOpen(__result);
				return;
			}
			Building booth = TollUtility.NearestBooth(map, __result.ownedPawns[0].Position);
			if (booth == null) return;
			GameComponent_TollState.Instance?.Open(__result, booth);
		}
	}
}
