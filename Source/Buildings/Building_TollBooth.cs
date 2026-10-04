using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimToll.Buildings
{
	public class Building_TollBooth : Building
	{
		private bool active = true;
		private bool markerHidden;

		public bool Active => active;

		public override void Print(SectionLayer layer)
		{
			if (markerHidden) return;
			base.Print(layer);
		}

		public override string GetInspectString()
		{
			string s = base.GetInspectString();
			if (!s.NullOrEmpty()) s += "\n";
			s += active ? "RimToll.BoothActive".Translate() : "RimToll.BoothSuspended".Translate();
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			if (comp != null) s += "\n" + "RimToll.BoothCollected".Translate(comp.TotalCollected);
			if (markerHidden) s += "\n" + "RimToll.MarkerHidden".Translate();
			return s;
		}

		public override IEnumerable<Gizmo> GetGizmos()
		{
			foreach (Gizmo g in base.GetGizmos()) yield return g;

			yield return new Command_Toggle
			{
				defaultLabel = "RimToll.GizmoToggle".Translate(),
				defaultDesc = "RimToll.GizmoToggleDesc".Translate(),
				icon = RimTollTex.Collect ?? TexCommand.DesirePower,
				isActive = () => active,
				toggleAction = delegate
				{
					active = !active;
					if (!active) GameComponent_TollState.Instance?.Notify_BoothSuspended(this);
				},
			};

			yield return new Command_Toggle
			{
				defaultLabel = "RimToll.GizmoShowMarker".Translate(),
				defaultDesc = "RimToll.GizmoShowMarkerDesc".Translate(),
				icon = RimTollTex.ShowMarker ?? TexCommand.ForbidOff,
				isActive = () => !markerHidden,
				toggleAction = delegate
				{
					markerHidden = !markerHidden;
					if (Spawned) DirtyMapMesh(Map);
				},
			};
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref active, "active", true);
			Scribe_Values.Look(ref markerHidden, "markerHidden");
		}
	}
}
