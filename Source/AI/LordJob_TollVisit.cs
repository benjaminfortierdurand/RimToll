using System;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimToll.AI
{
	public class LordJob_TollVisit : LordJob
	{
		public const string MemoPaid = "RimToll_Paid";
		public const string MemoAborted = "RimToll_Aborted";
		public const string MemoTurnBack = "RimToll_TurnBack";

		public int caseId;
		private IntVec3 waitSpot;
		private IntVec3 originalDest;

		public LordJob_TollVisit()
		{
		}

		public LordJob_TollVisit(int caseId, IntVec3 waitSpot, IntVec3 originalDest)
		{
			this.caseId = caseId;
			this.waitSpot = waitSpot;
			this.originalDest = originalDest;
		}

		public override string GetReport(Pawn pawn) => "RimToll.ReportToll".Translate();

		public override bool CanOpenAnyDoor(Pawn p)
		{
			TollCase c = GameComponent_TollState.Instance?.GetCase(caseId);
			return c != null && p == c.payer;
		}

		public override StateGraph CreateGraph()
		{
			var graph = new StateGraph();
			StateGraph travelSub = new LordJob_Travel(waitSpot).CreateGraph();
			LordToil travel = graph.AttachSubgraph(travelSub).StartingToil;
			graph.StartingToil = travel;

			var pay = new LordToil_TollPay(caseId, waitSpot);
			graph.AddToil(pay);

			StateGraph leaveSub = new LordJob_TravelAndExit(originalDest).CreateGraph();
			LordToil leave = graph.AttachSubgraph(leaveSub).StartingToil;

			var exitDirect = new LordToil_ExitMapDefending(LocomotionUrgency.Jog, canDig: true, interruptCurrentJob: true);
			graph.AddToil(exitDirect);

			var arrive = new Transition(travel, pay);
			arrive.AddTrigger(new Trigger_Memo("TravelArrived"));
			arrive.AddTrigger(new Trigger_TicksPassed(5000));
			arrive.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(arrive);

			var paid = new Transition(pay, leave);
			paid.AddTrigger(new Trigger_Memo(MemoPaid));
			paid.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
			paid.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(paid);

			var aborted = new Transition(pay, leave);
			aborted.AddSources(travelSub.lordToils);
			aborted.AddTrigger(new Trigger_Memo(MemoAborted));
			aborted.AddTrigger(new Trigger_TicksPassed(40000));
			aborted.AddPreAction(new TransitionAction_Custom((Action)delegate
			{
				GameComponent_TollState.Instance?.Notify_TollAborted(caseId);
			}));
			aborted.AddPreAction(new TransitionAction_EnsureHaveExitDestination());
			aborted.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(aborted);

			var turnBack = new Transition(pay, exitDirect);
			turnBack.AddSources(travelSub.lordToils);
			turnBack.AddTrigger(new Trigger_Memo(MemoTurnBack));
			turnBack.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(turnBack);

			var hostile = new Transition(pay, exitDirect);
			hostile.AddSources(travelSub.lordToils);
			hostile.AddSources(leaveSub.lordToils);
			hostile.AddTrigger(new Trigger_BecamePlayerEnemy());
			hostile.AddPreAction(new TransitionAction_Custom((Action)delegate
			{
				GameComponent_TollState.Instance?.Notify_TollAborted(caseId);
			}));
			hostile.AddPostAction(new TransitionAction_WakeAll());
			hostile.AddPostAction(new TransitionAction_EndAllJobs());
			graph.AddTransition(hostile);

			return graph;
		}

		public override void ExposeData()
		{
			Scribe_Values.Look(ref caseId, "caseId");
			Scribe_Values.Look(ref waitSpot, "waitSpot");
			Scribe_Values.Look(ref originalDest, "originalDest");
		}
	}

	public class LordToil_ExitMapDefending : LordToil_ExitMap
	{
		public override DutyDef ExitDuty => DutyDefOf.ExitMapBestAndDefendSelf;

		public LordToil_ExitMapDefending(LocomotionUrgency locomotion, bool canDig, bool interruptCurrentJob)
			: base(locomotion, canDig, interruptCurrentJob)
		{
		}
	}

	public class LordToilData_TollPay : LordToilData
	{
		public int stuckSince = -1;

		public override void ExposeData()
		{
			Scribe_Values.Look(ref stuckSince, "stuckSince", -1);
		}
	}

	public class LordToil_TollPay : LordToil
	{
		private const int StuckTimeout = 1500;

		private readonly int caseId;
		private readonly IntVec3 waitSpot;

		private LordToilData_TollPay Data => (LordToilData_TollPay)data;

		public override bool AllowSatisfyLongNeeds => false;

		public override IntVec3 FlagLoc => waitSpot;

		public LordToil_TollPay(int caseId, IntVec3 waitSpot)
		{
			this.caseId = caseId;
			this.waitSpot = waitSpot;
			data = new LordToilData_TollPay();
		}

		private void TryAnnounce()
		{
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			TollCase c = comp?.GetCase(caseId);
			if (c == null || c.letterSent || !c.BoothOk) return;
			bool atWindow = TollUtility.PayerOk(c.payer, lord)
				&& c.payer.Position.InHorDistOf(c.booth.Position, 3f);
			if (atWindow || lord.ticksInToil > 3000) comp.Notify_AtBooth(caseId);
		}

		public override void UpdateAllDuties()
		{
			TollCase c = GameComponent_TollState.Instance?.GetCase(caseId);
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p.mindState == null) continue;
				if (c != null && c.state == TollCase.Paying && p == c.payer && c.BoothOk)
				{
					p.mindState.duty = new PawnDuty(RimTollDefOf.RimToll_DeliverToll, c.booth);
				}
				else if (c != null && p == c.payer && c.BoothOk)
				{
					p.mindState.duty = new PawnDuty(RimTollDefOf.RimToll_StandAtToll, c.booth);
				}
				else
				{
					p.mindState.duty = new PawnDuty(RimTollDefOf.RimToll_WaitAtToll, waitSpot) { radius = 4f };
				}
			}
		}

		public override void LordToilTick()
		{
			int now = Find.TickManager.TicksGame;
			if (now % 30 == 0) TryAnnounce();
			if (now % 251 != 0) return;
			TollCase c = GameComponent_TollState.Instance?.GetCase(caseId);
			if (c == null || !c.BoothOk)
			{
				lord.ReceiveMemo(LordJob_TollVisit.MemoAborted);
				return;
			}
			if (c.state != TollCase.Paying) return;
			if (!TollUtility.PayerOk(c.payer, lord))
			{
				c.payer = TollUtility.PickPayer(lord);
				if (c.payer == null)
				{
					lord.ReceiveMemo(LordJob_TollVisit.MemoAborted);
					return;
				}
				UpdateAllDuties();
			}
			if (c.payer.CanReach(c.booth, PathEndMode.Touch, Danger.Deadly))
			{
				Data.stuckSince = -1;
				return;
			}
			if (Data.stuckSince < 0)
			{
				Data.stuckSince = now;
				return;
			}
			if (now - Data.stuckSince >= StuckTimeout) lord.ReceiveMemo(LordJob_TollVisit.MemoAborted);
		}
	}
}
