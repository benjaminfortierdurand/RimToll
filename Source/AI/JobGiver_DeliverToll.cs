using RimWorld;
using Verse;
using Verse.AI;

namespace RimToll.AI
{
	public class JobGiver_DeliverToll : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			PawnDuty duty = pawn.mindState?.duty;
			if (duty == null || duty.def != RimTollDefOf.RimToll_DeliverToll) return null;
			if (!(duty.focus.Thing is Building booth)) return null;
			if (!booth.Spawned) return null;
			if (!pawn.CanReach(booth, PathEndMode.Touch, Danger.Deadly)) return null;
			return JobMaker.MakeJob(RimTollDefOf.RimToll_PayToll, booth);
		}
	}
}
