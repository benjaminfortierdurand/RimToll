using RimWorld;
using Verse;
using Verse.AI;

namespace RimToll.AI
{
	public class JobGiver_StandAtToll : ThinkNode_JobGiver
	{
		protected override Job TryGiveJob(Pawn pawn)
		{
			PawnDuty duty = pawn.mindState?.duty;
			if (duty == null || !duty.focus.IsValid) return null;
			IntVec3 cell = duty.focus.Cell;
			if (!CellFree(cell, pawn) || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly))
			{
				cell = BestNearby(duty.focus.Cell, pawn);
				if (!cell.IsValid) return null;
			}
			if (pawn.Position == cell)
			{
				Job wait = JobMaker.MakeJob(JobDefOf.Wait);
				wait.expiryInterval = 400;
				return wait;
			}
			Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
			job.expiryInterval = 400;
			return job;
		}

		private static bool CellFree(IntVec3 cell, Pawn pawn)
		{
			Pawn other = cell.GetFirstPawn(pawn.Map);
			return other == null || other == pawn;
		}

		private static IntVec3 BestNearby(IntVec3 center, Pawn pawn)
		{
			for (int i = 0; i < GenAdj.AdjacentCells.Length; i++)
			{
				IntVec3 c = center + GenAdj.AdjacentCells[i];
				if (!c.InBounds(pawn.Map) || !c.Standable(pawn.Map)) continue;
				if (!CellFree(c, pawn)) continue;
				if (!pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly)) continue;
				return c;
			}
			return IntVec3.Invalid;
		}
	}
}
