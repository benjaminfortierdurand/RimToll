using Verse;

namespace RimToll
{
	public class RimTollSettings : ModSettings
	{
		public const float DefTollMultiplier = 1f;
		public const int DefLetterHours = 4;
		public const int DefBoothCost = 700;

		public float tollMultiplier = DefTollMultiplier;
		public int letterHours = DefLetterHours;
		public int boothCost = DefBoothCost;
		public bool tollTravelers = true;
		public bool tollVisitors = true;
		public bool tollTraders = true;
		public bool tollBeggars = true;
		public bool tollShoppers = true;
		public bool armyEvent = true;
		public bool punitiveEvent = true;
		public bool falseConvoyEvent = true;
		public bool requireOfficer;

		public void ResetToDefaults()
		{
			tollMultiplier = DefTollMultiplier;
			letterHours = DefLetterHours;
			boothCost = DefBoothCost;
			tollTravelers = true;
			tollVisitors = true;
			tollTraders = true;
			tollBeggars = true;
			tollShoppers = true;
			armyEvent = true;
			punitiveEvent = true;
			falseConvoyEvent = true;
			requireOfficer = false;
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref tollMultiplier, "tollMultiplier", DefTollMultiplier);
			Scribe_Values.Look(ref letterHours, "letterHours", DefLetterHours);
			Scribe_Values.Look(ref boothCost, "boothCost", DefBoothCost);
			Scribe_Values.Look(ref tollTravelers, "tollTravelers", true);
			Scribe_Values.Look(ref tollVisitors, "tollVisitors", true);
			Scribe_Values.Look(ref tollTraders, "tollTraders", true);
			Scribe_Values.Look(ref tollBeggars, "tollBeggars", true);
			Scribe_Values.Look(ref tollShoppers, "tollShoppers", true);
			Scribe_Values.Look(ref armyEvent, "armyEvent", true);
			Scribe_Values.Look(ref punitiveEvent, "punitiveEvent", true);
			Scribe_Values.Look(ref falseConvoyEvent, "falseConvoyEvent", true);
			Scribe_Values.Look(ref requireOfficer, "requireOfficer");
		}
	}
}
