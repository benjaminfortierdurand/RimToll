using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace RimToll.Jobs
{
	public class JobDriver_InspectPapers : JobDriver
	{
		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		protected override IEnumerable<Toil> MakeNewToils()
		{
			this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
			this.FailOn(() => GameComponent_TollState.Instance?.GetCase(job.count) == null);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			var open = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
			open.initAction = delegate
			{
				TollCase c = GameComponent_TollState.Instance?.GetCase(job.count);
				if (c == null || !c.CanDemand) return;
				UI.Dialog_TollPass.OpenFor(job.count);
			};
			yield return open;
		}
	}
}
