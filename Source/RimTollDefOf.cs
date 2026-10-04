using RimWorld;
using Verse;
using Verse.AI;

namespace RimToll
{
	[DefOf]
	public static class RimTollDefOf
	{
		public static ThingDef RimToll_TollBooth;

		public static JobDef RimToll_PayToll;

		public static JobDef RimToll_InspectPapers;

		public static DutyDef RimToll_DeliverToll;

		public static DutyDef RimToll_WaitAtToll;

		public static DutyDef RimToll_StandAtToll;

		public static LetterDef RimToll_TollDemand;

		public static HistoryEventDef RimToll_TurnedAway;

		public static HistoryEventDef RimToll_Tolled;

		public static HistoryEventDef RimToll_Overcharged;

		public static HistoryEventDef RimToll_Accused;

		public static HistoryEventDef RimToll_ArmyPassage;

		public static HistoryEventDef RimToll_WavedThrough;

		public static IncidentDef RimToll_MarchingArmy;

		public static SoundDef Silver_Drop;

		public static HediffDef RimToll_UnderContract;

		static RimTollDefOf()
		{
			DefOfHelper.EnsureInitializedInCtor(typeof(RimTollDefOf));
		}
	}
}
