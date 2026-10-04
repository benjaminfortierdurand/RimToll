using System.Collections.Generic;
using RimWorld;
using RimToll.AI;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace RimToll.Jobs
{
	public class JobDriver_PayToll : JobDriver
	{
		private const int CountDuration = 120;

		private Building Booth => (Building)job.GetTarget(TargetIndex.A).Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
			this.FailOn(() => pawn.mindState.duty?.def != RimTollDefOf.RimToll_DeliverToll);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			var count = new Toil
			{
				defaultCompleteMode = ToilCompleteMode.Delay,
				defaultDuration = CountDuration,
				handlingFacing = true,
			};
			count.tickAction = () => pawn.rotationTracker.FaceTarget(Booth);
			count.WithProgressBarToilDelay(TargetIndex.A);
			yield return count;
			var drop = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
			drop.initAction = DropSilver;
			yield return drop;
		}

		private void DropSilver()
		{
			Lord lord = pawn.GetLord();
			if (!(lord?.LordJob is LordJob_TollVisit lordJob)) return;
			TollCase c = GameComponent_TollState.Instance?.GetCase(lordJob.caseId);
			if (c == null || c.amount <= 0)
			{
				lord.ReceiveMemo(LordJob_TollVisit.MemoAborted);
				return;
			}
			List<Thing> payment = TollUtility.TakePayment(lord.ownedPawns, c.amount, c.payInKind);
			int shortfall = c.amount;
			for (int i = 0; i < payment.Count; i++)
			{
				Thing t = payment[i];
				shortfall -= Mathf.RoundToInt(t.MarketValue * t.stackCount);
				GenPlace.TryPlaceThing(t, Booth.Position, pawn.Map, ThingPlaceMode.Near);
			}
			while (shortfall > 0)
			{
				Thing coin = ThingMaker.MakeThing(ThingDefOf.Silver);
				coin.stackCount = Mathf.Min(coin.def.stackLimit, shortfall);
				shortfall -= coin.stackCount;
				GenPlace.TryPlaceThing(coin, Booth.Position, pawn.Map, ThingPlaceMode.Near);
			}
			RimTollDefOf.Silver_Drop?.PlayOneShot(new TargetInfo(Booth.Position, pawn.Map));
			MoteMaker.ThrowText(Booth.DrawPos, pawn.Map, "+" + c.amount, 3.9f);
			GameComponent_TollState.Instance?.Notify_TollPaid(lordJob.caseId);
		}
	}
}
