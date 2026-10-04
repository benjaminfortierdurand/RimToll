using RimWorld;
using UnityEngine;
using Verse;

namespace RimToll
{
	public class FactionTollMemory : IExposable
	{
		public const float DecayPerDay = 0.15f;

		public Faction faction;
		public float resentment;
		public int lastTollTick = -1;
		public int totalPaid;
		public int timesTolled;

		public float Current(int now)
		{
			if (lastTollTick < 0) return 0f;
			float days = (now - lastTollTick) / 60000f;
			return Mathf.Max(0f, resentment - days * DecayPerDay);
		}

		public void Add(float amount, int now)
		{
			resentment = Mathf.Max(0f, Current(now) + amount);
			lastTollTick = now;
		}

		public void ExposeData()
		{
			Scribe_References.Look(ref faction, "faction");
			Scribe_Values.Look(ref resentment, "resentment");
			Scribe_Values.Look(ref lastTollTick, "lastTollTick", -1);
			Scribe_Values.Look(ref totalPaid, "totalPaid");
			Scribe_Values.Look(ref timesTolled, "timesTolled");
		}
	}
}
