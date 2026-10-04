using RimWorld;
using Verse;

namespace RimToll
{
	public class TollThreat : IExposable
	{
		public Map map;
		public Faction cover;
		public int fireTick;

		public void ExposeData()
		{
			Scribe_References.Look(ref map, "map");
			Scribe_References.Look(ref cover, "cover");
			Scribe_Values.Look(ref fireTick, "fireTick");
		}
	}
}
