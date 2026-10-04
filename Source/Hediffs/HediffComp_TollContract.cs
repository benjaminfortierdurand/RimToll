using RimWorld;
using Verse;

namespace RimToll.Hediffs
{
	public class HediffCompProperties_TollContract : HediffCompProperties
	{
		public HediffCompProperties_TollContract()
		{
			compClass = typeof(HediffComp_TollContract);
		}
	}

	public class HediffComp_TollContract : HediffComp
	{
		public override string CompTipStringExtra
		{
			get
			{
				int end = GameComponent_TollState.Instance?.ContractEndFor(parent.pawn) ?? -1;
				if (end < 0) return null;
				int left = end - Find.TickManager.TicksGame;
				if (left < 0) left = 0;
				return "RimToll.ContractLeft".Translate(left.ToStringTicksToPeriod());
			}
		}
	}
}
