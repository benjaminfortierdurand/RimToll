using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimToll.Buildings;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimToll
{
	public enum TollReaction
	{
		Pay,
		TurnBack,
		Force,
	}

	public static class TollUtility
	{
		private static readonly AccessTools.FieldRef<LordJob_TravelAndExit, IntVec3> TravelDestRef =
			AccessTools.FieldRefAccess<LordJob_TravelAndExit, IntVec3>("travelDest");

		public const float MinLootValue = 320f;

		public static Thing FindLoot(Map map, IntVec3 from, float maxDist)
		{
			if (map == null) return null;
			Thing best = null;
			float bestDist = float.MaxValue;
			float maxSq = maxDist * maxDist;
			TraverseParms tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly);
			List<Thing> haulables = map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver);
			for (int i = 0; i < haulables.Count; i++)
			{
				Thing t = haulables[i];
				if (!t.Spawned || !t.def.stealable || t.IsBurning()) continue;
				if (t.MarketValue * t.stackCount < MinLootValue) continue;
				float d = from.DistanceToSquared(t.Position);
				if (d > maxSq || d >= bestDist) continue;
				if (!map.reachability.CanReach(from, t, PathEndMode.ClosestTouch, tp)) continue;
				bestDist = d;
				best = t;
			}
			return best;
		}

		public static bool AnyActiveBooth(Map map)
		{
			List<Thing> booths = map.listerThings.ThingsOfDef(RimTollDefOf.RimToll_TollBooth);
			for (int i = 0; i < booths.Count; i++)
			{
				if (booths[i] is Building_TollBooth b && b.Spawned && b.Active) return true;
			}
			return false;
		}

		public static Building NearestBooth(Map map, IntVec3 from)
		{
			Building best = null;
			float bestDist = float.MaxValue;
			TraverseParms tp = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly);
			List<Thing> booths = map.listerThings.ThingsOfDef(RimTollDefOf.RimToll_TollBooth);
			for (int i = 0; i < booths.Count; i++)
			{
				if (!(booths[i] is Building_TollBooth b) || !b.Spawned || !b.Active) continue;
				if (!map.reachability.CanReach(from, b, PathEndMode.Touch, tp)) continue;
				float d = from.DistanceToSquared(b.Position);
				if (d < bestDist)
				{
					bestDist = d;
					best = b;
				}
			}
			return best;
		}

		public static int TollFor(List<Pawn> pawns, float cap = 400f)
		{
			float wealth = 0f;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p.equipment != null)
				{
					List<ThingWithComps> eq = p.equipment.AllEquipmentListForReading;
					for (int j = 0; j < eq.Count; j++)
					{
						wealth += eq[j].MarketValue * eq[j].stackCount;
					}
				}
				if (p.apparel != null)
				{
					List<Apparel> worn = p.apparel.WornApparel;
					for (int j = 0; j < worn.Count; j++)
					{
						wealth += worn[j].MarketValue;
					}
				}
				if (p.inventory != null)
				{
					foreach (Thing t in p.inventory.innerContainer)
					{
						wealth += t.MarketValue * t.stackCount;
					}
				}
			}
			float raw = Mathf.Clamp(wealth * 0.05f, 25f, cap) * (RimTollMod.Settings?.tollMultiplier ?? DefMult());
			return 5 * Mathf.Max(1, Mathf.RoundToInt(raw / 5f));
		}

		private static float DefMult() => RimTollSettings.DefTollMultiplier;

		public static int AmountForLevel(int baseAmount, int level)
		{
			float f = level == 0 ? 0.6f : (level == 2 ? 1.6f : 1f);
			return 5 * Mathf.Max(1, Mathf.RoundToInt(baseAmount * f / 5f));
		}

		public static float GroupStrength(List<Pawn> pawns)
		{
			float s = 0f;
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p == null || p.Dead || p.Downed || !p.RaceProps.Humanlike) continue;
				s += p.equipment?.Primary != null ? 1f : 0.4f;
			}
			return s;
		}

		private const float GarrisonRadius = 22f;

		private static float NearFactor(IntVec3 pos, IntVec3 focus)
		{
			if (!focus.IsValid) return 1f;
			return pos.DistanceToSquared(focus) <= GarrisonRadius * GarrisonRadius ? 2f : 1f;
		}

		public static float ColonyStrength(Map map, IntVec3 focus)
		{
			float s = 0f;
			List<Pawn> pawns = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer);
			for (int i = 0; i < pawns.Count; i++)
			{
				Pawn p = pawns[i];
				if (p == null || p.Dead || p.Downed) continue;
				float part;
				if (p.RaceProps.Humanlike)
				{
					if (!p.IsFreeColonist || p.InMentalState) continue;
					part = p.equipment?.Primary != null ? 1f : 0.4f;
				}
				else if (p.RaceProps.IsMechanoid)
				{
					part = Mathf.Clamp(p.kindDef.combatPower / 100f, 0.3f, 3f);
				}
				else
				{
					if (p.training == null || !p.training.HasLearned(TrainableDefOf.Release)) continue;
					part = 0.5f;
				}
				s += part * NearFactor(p.Position, focus);
			}
			List<Building> buildings = map.listerBuildings.allBuildingsColonist;
			for (int i = 0; i < buildings.Count; i++)
			{
				if (!(buildings[i] is Building_Turret turret)) continue;
				CompPowerTrader power = turret.TryGetComp<CompPowerTrader>();
				if (power != null && !power.PowerOn) continue;
				s += NearFactor(turret.Position, focus);
			}
			return s;
		}

		private const float TerrorPerPrisoner = 15f;
		private const float TerrorForFullFear = 100f;
		private const float FearNoticeThreshold = 0.15f;

		private static StatDef terrorStat;
		private static bool terrorLooked;

		private static StatDef TerrorStat
		{
			get
			{
				if (!terrorLooked)
				{
					terrorStat = DefDatabase<StatDef>.GetNamedSilentFail("TerrorSource");
					terrorLooked = true;
				}
				return terrorStat;
			}
		}

		public static float FearAt(Map map, IntVec3 focus)
		{
			if (map == null || !focus.IsValid) return 0f;
			float radiusSq = GarrisonRadius * GarrisonRadius;
			float terror = 0f;
			StatDef stat = TerrorStat;
			if (stat != null)
			{
				List<Building> buildings = map.listerBuildings.allBuildingsColonist;
				for (int i = 0; i < buildings.Count; i++)
				{
					Building b = buildings[i];
					if (b.def.statBases == null || !b.def.statBases.StatListContains(stat)) continue;
					if (b.Position.DistanceToSquared(focus) > radiusSq) continue;
					terror += b.GetStatValue(stat);
				}
			}
			List<Pawn> prisoners = map.mapPawns.PrisonersOfColonySpawned;
			for (int i = 0; i < prisoners.Count; i++)
			{
				if (prisoners[i].Position.DistanceToSquared(focus) <= radiusSq) terror += TerrorPerPrisoner;
			}
			return Mathf.Clamp01(terror / TerrorForFullFear);
		}

		public static string FearDesc(float fear)
		{
			return fear >= FearNoticeThreshold ? "RimToll.FearNotice".Translate().ToString() : null;
		}

		public static float StrengthRatio(List<Pawn> pawns, Map map, IntVec3 focus)
		{
			if (map == null) return 1f;
			return GroupStrength(pawns) / Mathf.Max(1f, ColonyStrength(map, focus));
		}

		public static string StrengthDesc(float ratio)
		{
			if (ratio < 0.6f) return "RimToll.StrengthWeak".Translate();
			if (ratio < 1.2f) return "RimToll.StrengthEven".Translate();
			return "RimToll.StrengthStrong".Translate();
		}

		public static string ResentmentDesc(float resentment)
		{
			if (resentment < 0.2f) return null;
			if (resentment < 0.6f) return "RimToll.MemoryPaidBefore".Translate();
			return "RimToll.MemoryBitter".Translate();
		}

		public static TollReaction DecideDemand(int level, float ratio, int goodwill, float resentment, float fear)
		{
			float defiance = level * 0.25f;
			defiance += Mathf.Clamp(ratio - 1f, -0.5f, 1f) * 0.35f;
			defiance -= goodwill / 100f * 0.2f;
			defiance += Mathf.Min(resentment, 1.5f) * 0.3f;
			defiance -= fear * 0.3f;
			defiance += Rand.Range(-0.15f, 0.15f);
			if (defiance < 0.35f) return TollReaction.Pay;
			return ratio > 1f + fear * 0.6f ? TollReaction.Force : TollReaction.TurnBack;
		}

		public static TollReaction DecideRefusal(float ratio, float fear)
		{
			return ratio > 1f + fear * 0.6f + Rand.Range(-0.2f, 0.2f) ? TollReaction.Force : TollReaction.TurnBack;
		}

		public static bool PayerOk(Pawn p, Lord lord)
		{
			return p != null && p.Spawned && !p.Dead && !p.Downed && !p.InMentalState && p.GetLord() == lord;
		}

		public static List<Pawn> Humans(Lord lord)
		{
			var list = new List<Pawn>();
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p != null && p.RaceProps.Humanlike && !p.Dead) list.Add(p);
			}
			return list;
		}

		private static string BlendedName(List<Pawn> pool, Pawn avoid)
		{
			for (int attempt = 0; attempt < 8; attempt++)
			{
				Pawn a = pool.RandomElement();
				Pawn b = pool.RandomElement();
				if (a.Name is NameTriple na && b.Name is NameTriple nb && na.First != nb.Last)
				{
					string blended = na.First + " " + nb.Last;
					if (avoid?.Name == null || blended != avoid.Name.ToStringShort) return blended;
				}
			}
			return null;
		}

		public static TollPass MakePass(Lord lord, Pawn bearer, int forgery, bool military = false)
		{
			List<Pawn> humans = Humans(lord);
			var pass = new TollPass
			{
				holderName = bearer?.Name?.ToStringShort ?? bearer?.LabelShortCap ?? "?",
				portrait = bearer,
				claimedCount = humans.Count,
				reason = military ? Rand.RangeInclusive(5, 8) : Rand.RangeInclusive(1, 4),
				forgery = TollPass.Genuine,
			};
			if (forgery == TollPass.Genuine) return pass;

			switch (forgery)
			{
				case TollPass.FakeName:
					if (humans.Count < 2) return pass;
					string blended = BlendedName(humans, bearer);
					if (blended == null) return pass;
					pass.holderName = blended;
					pass.forgery = TollPass.FakeName;
					break;
				case TollPass.FakeCount:
					int shift = Rand.Chance(0.5f) ? -2 : 2;
					if (pass.claimedCount + shift < 1) shift = 2;
					pass.claimedCount += shift;
					pass.forgery = TollPass.FakeCount;
					break;
				case TollPass.FakePortrait:
					Pawn other = humans.Where(p => p != bearer).RandomElementWithFallback();
					if (other == null) return pass;
					pass.portrait = other;
					pass.forgery = TollPass.FakePortrait;
					break;
				case TollPass.FakeMonth:
					pass.fakeMonth = Rand.RangeInclusive(1, 3);
					pass.forgery = TollPass.FakeMonth;
					break;
				case TollPass.FakeReason:
					if (military) return pass;
					pass.reason = Rand.RangeInclusive(5, 8);
					pass.forgery = TollPass.FakeReason;
					break;
				default:
					pass.forgery = TollPass.FakeSeal;
					break;
			}
			return pass;
		}

		public static Pawn PickRecruit(Lord lord, Pawn payer, float chance)
		{
			if (lord.ownedPawns.Count < 2) return null;
			if (!Rand.Chance(chance)) return null;
			var candidates = new List<Pawn>();
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (p == payer || !p.RaceProps.Humanlike) continue;
				if (p.Dead || p.Downed || p.InMentalState || !p.Spawned) continue;
				if (p.DevelopmentalStage != DevelopmentalStage.Adult) continue;
				if (p.GetTraderCaravanRole() == TraderCaravanRole.Trader) continue;
				if (p.IsPrisoner || p.IsSlave) continue;
				candidates.Add(p);
			}
			return candidates.Count == 0 ? null : candidates.RandomElement();
		}

		public static Pawn PickPayer(Lord lord)
		{
			Pawn trader = null;
			for (int i = 0; i < lord.ownedPawns.Count; i++)
			{
				Pawn p = lord.ownedPawns[i];
				if (!p.RaceProps.Humanlike || !PayerOk(p, lord)) continue;
				if (p.GetTraderCaravanRole() == TraderCaravanRole.Trader)
				{
					if (trader == null) trader = p;
					continue;
				}
				return p;
			}
			return trader;
		}

		public static bool Tradeable(Thing t)
		{
			return t != null && t.def != null && t.def.stealable && t.MarketValue > 0f && !t.IsBurning();
		}

		public static float PayCapacity(List<Pawn> pawns, out int silver)
		{
			silver = 0;
			float goods = 0f;
			for (int i = 0; i < pawns.Count; i++)
			{
				ThingOwner inv = pawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				for (int j = 0; j < inv.Count; j++)
				{
					Thing t = inv[j];
					if (t.def == ThingDefOf.Silver) { silver += t.stackCount; continue; }
					if (Tradeable(t)) goods += t.MarketValue * t.stackCount;
				}
			}
			return silver + goods;
		}

		public static List<Thing> TakePayment(List<Pawn> pawns, int amount, bool takeGoods)
		{
			var taken = new List<Thing>();
			float got = 0f;
			for (int i = 0; i < pawns.Count && got < amount; i++)
			{
				ThingOwner inv = pawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				List<Thing> carried = inv.ToList();
				for (int j = 0; j < carried.Count && got < amount; j++)
				{
					Thing t = carried[j];
					if (t.def != ThingDefOf.Silver) continue;
					int take = Mathf.Min(t.stackCount, Mathf.CeilToInt(amount - got));
					Thing split = inv.Take(t, take);
					if (split == null) continue;
					taken.Add(split);
					got += take;
				}
			}
			for (int i = 0; takeGoods && i < pawns.Count && got < amount; i++)
			{
				ThingOwner inv = pawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				List<Thing> carried = inv.ToList();
				for (int j = 0; j < carried.Count && got < amount; j++)
				{
					Thing t = carried[j];
					if (t.def == ThingDefOf.Silver || !Tradeable(t)) continue;
					int need = Mathf.Max(1, Mathf.CeilToInt((amount - got) / t.MarketValue));
					int take = Mathf.Min(t.stackCount, need);
					Thing split = inv.Take(t, take);
					if (split == null) continue;
					taken.Add(split);
					got += split.MarketValue * split.stackCount;
				}
			}
			return taken;
		}

		public static IntVec3 OriginalDest(Lord lord)
		{
			if (lord?.LordJob is LordJob_TravelAndExit job)
			{
				try
				{
					return TravelDestRef(job);
				}
				catch
				{
				}
			}
			return IntVec3.Invalid;
		}

		public static void PickOfficer(int caseId)
		{
			TollCase c = GameComponent_TollState.Instance?.GetCase(caseId);
			if (c == null || !c.CanDemand) return;
			var options = new List<FloatMenuOption>();
			List<Pawn> colonists = c.map.mapPawns.FreeColonistsSpawned;
			for (int i = 0; i < colonists.Count; i++)
			{
				Pawn p = colonists[i];
				if (p == null || p.Dead || p.Downed || p.InMentalState) continue;
				string label = p.LabelShortCap;
				if (!p.CanReach(c.booth, PathEndMode.Touch, Danger.Deadly))
				{
					options.Add(new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null));
					continue;
				}
				Pawn officer = p;
				options.Add(new FloatMenuOption(label, delegate
				{
					Job job = JobMaker.MakeJob(RimTollDefOf.RimToll_InspectPapers, c.booth);
					job.count = caseId;
					job.playerForced = true;
					officer.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
					Messages.Message("RimToll.MessageOfficerSent".Translate(officer.LabelShortCap),
						new LookTargets(officer), MessageTypeDefOf.NeutralEvent, historical: false);
				}));
			}
			if (options.Count == 0)
			{
				Messages.Message("RimToll.MessageNoOfficer".Translate(),
					MessageTypeDefOf.RejectInput, historical: false);
				return;
			}
			options.SortBy(o => o.Label);
			Find.WindowStack.Add(new FloatMenu(options));
		}

		public static IntVec3 FindWaitSpot(Building booth, IntVec3 groupPos, Map map)
		{
			TraverseParms tp = TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Deadly);
			IntVec3 best = IntVec3.Invalid;
			float bestDist = float.MaxValue;
			foreach (IntVec3 c in GenRadial.RadialCellsAround(booth.Position, 14f, useCenter: false))
			{
				if (!c.InBounds(map) || !c.Standable(map)) continue;
				if (c.DistanceToSquared(booth.Position) < 64f) continue;
				if (!map.reachability.CanReach(groupPos, c, PathEndMode.OnCell, tp)) continue;
				float d = c.DistanceToSquared(groupPos);
				if (d < bestDist)
				{
					bestDist = d;
					best = c;
				}
			}
			if (best.IsValid) return best;
			return groupPos;
		}
	}
}
