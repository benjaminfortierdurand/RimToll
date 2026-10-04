using RimWorld;
using Verse;

namespace RimToll
{
	public class TollWorker : IExposable
	{
		public Pawn pawn;
		public Faction origin;
		public int departTick;

		public bool Valid => pawn != null && !pawn.Dead && pawn.Faction == Faction.OfPlayer;

		public void ExposeData()
		{
			Scribe_References.Look(ref pawn, "pawn");
			Scribe_References.Look(ref origin, "origin");
			Scribe_Values.Look(ref departTick, "departTick");
		}
	}
}
