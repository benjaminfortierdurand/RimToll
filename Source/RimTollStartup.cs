using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimToll
{
	[StaticConstructorOnStartup]
	internal static class RimTollStartup
	{
		static RimTollStartup()
		{
			ApplyBoothCost();
		}

		public static void ApplyBoothCost()
		{
			ThingDef def = RimTollDefOf.RimToll_TollBooth;
			if (def == null) return;
			int cost = RimTollMod.Settings?.boothCost ?? RimTollSettings.DefBoothCost;
			if (def.costList == null) def.costList = new List<ThingDefCountClass>();
			def.costList.Clear();
			if (cost > 0) def.costList.Add(new ThingDefCountClass(ThingDefOf.Silver, cost));
			def.ClearCachedData();
		}
	}
}
