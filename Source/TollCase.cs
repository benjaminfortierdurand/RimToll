using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimToll
{
	public class TollCase : IExposable
	{
		public const int Offered = 0;
		public const int Paying = 1;
		public const int Done = 2;

		public int id;
		public int state;
		public Lord lord;
		public Map map;
		public Faction faction;
		public Faction targetFaction;
		public bool isArmy;
		public bool isBeggars;
		public bool payInKind;
		public Building booth;
		public LordJob originalJob;
		public Pawn payer;
		public Pawn recruit;
		public int recruitDays;
		public TollPass pass;
		public bool letterSent;
		public int verdict;
		public List<TollStampMark> stamps = new List<TollStampMark>();
		public int baseAmount;
		public int amount;
		public int demandLevel;
		public int deadlineTick;

		public bool LordAlive => lord != null && lord.ownedPawns.Count > 0;

		public bool BoothOk => booth != null && booth.Spawned;

		public bool CanDemand => state == Offered && LordAlive && BoothOk;

		public void ExposeData()
		{
			Scribe_Values.Look(ref id, "id");
			Scribe_Values.Look(ref state, "state");
			Scribe_References.Look(ref lord, "lord");
			Scribe_References.Look(ref map, "map");
			Scribe_References.Look(ref faction, "faction");
			Scribe_References.Look(ref targetFaction, "targetFaction");
			Scribe_Values.Look(ref isArmy, "isArmy");
			Scribe_Values.Look(ref isBeggars, "isBeggars");
			Scribe_Values.Look(ref payInKind, "payInKind");
			Scribe_References.Look(ref booth, "booth");
			Scribe_Deep.Look(ref originalJob, "originalJob");
			Scribe_References.Look(ref payer, "payer");
			Scribe_References.Look(ref recruit, "recruit");
			Scribe_Values.Look(ref recruitDays, "recruitDays");
			Scribe_Deep.Look(ref pass, "pass");
			Scribe_Values.Look(ref letterSent, "letterSent");
			Scribe_Values.Look(ref verdict, "verdict");
			Scribe_Collections.Look(ref stamps, "stamps", LookMode.Deep);
			Scribe_Values.Look(ref baseAmount, "baseAmount");
			Scribe_Values.Look(ref amount, "amount");
			Scribe_Values.Look(ref demandLevel, "demandLevel");
			Scribe_Values.Look(ref deadlineTick, "deadlineTick");
			if (Scribe.mode == LoadSaveMode.PostLoadInit && stamps == null) stamps = new List<TollStampMark>();
		}
	}
}
