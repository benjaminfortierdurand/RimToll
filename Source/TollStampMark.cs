using Verse;

namespace RimToll
{
	public class TollStampMark : IExposable
	{
		public int type;
		public float x;
		public float y;
		public float angle;

		public void ExposeData()
		{
			Scribe_Values.Look(ref type, "type");
			Scribe_Values.Look(ref x, "x");
			Scribe_Values.Look(ref y, "y");
			Scribe_Values.Look(ref angle, "angle");
		}
	}
}
