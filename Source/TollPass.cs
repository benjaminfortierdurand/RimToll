using Verse;

namespace RimToll
{
	public class TollPass : IExposable
	{
		public const int Genuine = 0;
		public const int FakeName = 1;
		public const int FakeCount = 2;
		public const int FakePortrait = 3;
		public const int FakeMonth = 4;
		public const int FakeReason = 5;
		public const int FakeSeal = 6;

		public string holderName;
		public Pawn portrait;
		public int claimedCount;
		public int reason;
		public int forgery;
		public int fakeMonth;

		public bool Forged => forgery != Genuine;

		public void ExposeData()
		{
			Scribe_Values.Look(ref holderName, "holderName");
			Scribe_References.Look(ref portrait, "portrait");
			Scribe_Values.Look(ref claimedCount, "claimedCount");
			Scribe_Values.Look(ref reason, "reason");
			Scribe_Values.Look(ref forgery, "forgery");
			Scribe_Values.Look(ref fakeMonth, "fakeMonth");
		}
	}
}
