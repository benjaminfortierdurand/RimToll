using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimToll.AI
{
	public class LordJob_TollTheft : LordJob
	{
		public override bool GuiltyOnDowned => true;

		public override bool AddFleeToil => false;

		public override StateGraph CreateGraph()
		{
			var graph = new StateGraph();

			var theft = new LordToil_TollTheft();
			graph.StartingToil = theft;

			var fight = new LordToil_StealCover { useAvoidGrid = true };
			graph.AddToil(fight);

			var leave = new LordToil_ExitMap(LocomotionUrgency.Jog, canDig: true, interruptCurrentJob: true);
			graph.AddToil(leave);

			var harmed = new Transition(theft, fight);
			harmed.AddTrigger(new Trigger_PawnHarmed());
			harmed.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(harmed);

			var calm = new Transition(fight, theft);
			calm.AddTrigger(new Trigger_TicksPassedWithoutHarm(1800));
			calm.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(calm);

			var done = new Transition(theft, leave);
			done.AddSources(fight);
			done.AddTrigger(new Trigger_TicksPassed(30000));
			done.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
			done.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(done);

			return graph;
		}
	}

	public class LordToil_TollTheft : LordToil
	{
		private const float GrabRadius = 8f;

		public override bool AllowSatisfyLongNeeds => false;

		public override bool AllowSelfTend => false;

		public override void UpdateAllDuties()
		{
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p?.mindState == null) continue;
				DutyDef want;
				LocalTargetInfo focus = LocalTargetInfo.Invalid;

				if (p.carryTracker?.CarriedThing != null)
				{
					want = DutyDefOf.Steal;
				}
				else if (TollUtility.FindLoot(Map, p.Position, GrabRadius) != null)
				{
					want = DutyDefOf.Steal;
				}
				else
				{
					Thing loot = TollUtility.FindLoot(Map, p.Position, 9999f);
					if (loot != null)
					{
						want = DutyDefOf.TravelOrWait;
						focus = loot;
					}
					else
					{
						want = DutyDefOf.ExitMapBest;
					}
				}

				if (p.mindState.duty != null && p.mindState.duty.def == want) continue;
				p.mindState.duty = focus.IsValid ? new PawnDuty(want, focus) : new PawnDuty(want);
				p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
			}
		}

		public override void LordToilTick()
		{
			if (Find.TickManager.TicksGame % 181 != 0) return;
			UpdateAllDuties();
		}
	}
}
