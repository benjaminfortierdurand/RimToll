using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimToll.AI;
using RimToll.Letters;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimToll
{
	public class GameComponent_TollState : GameComponent
	{
		private const int CheckInterval = 250;

		public static GameComponent_TollState Instance { get; private set; }

		public static bool SuppressPatch;

		private List<TollCase> cases = new List<TollCase>();
		private List<FactionTollMemory> memories = new List<FactionTollMemory>();
		private List<TollWorker> workers = new List<TollWorker>();
		private List<TollThreat> threats = new List<TollThreat>();
		private List<int> pendingSwaps = new List<int>();
		private readonly List<Lord> pendingOpens = new List<Lord>();
		private List<int> pendingRestores = new List<int>();
		private int nextId = 1;
		private int totalCollected;
		private int lastRecruitOfferTick = -999999;

		private const int RecruitOfferCooldown = 15 * 60000;

		public int TotalCollected => totalCollected;

		public GameComponent_TollState(Game game)
		{
			Instance = this;
		}

		public override void FinalizeInit()
		{
			base.FinalizeInit();
			Instance = this;
		}

		public TollCase GetCase(int id)
		{
			for (int i = 0; i < cases.Count; i++)
			{
				if (cases[i].id == id) return cases[i];
			}
			return null;
		}

		public List<FactionTollMemory> AllMemories => memories;

		public FactionTollMemory MemoryFor(Faction faction, bool createIfMissing)
		{
			if (faction == null) return null;
			for (int i = 0; i < memories.Count; i++)
			{
				if (memories[i].faction == faction) return memories[i];
			}
			if (!createIfMissing) return null;
			var m = new FactionTollMemory { faction = faction };
			memories.Add(m);
			return m;
		}

		public float ResentmentFor(Faction faction)
		{
			return MemoryFor(faction, createIfMissing: false)?.Current(Find.TickManager.TicksGame) ?? 0f;
		}

		private bool RecruitAllowed()
		{
			if (workers.Count > 0) return false;
			return Find.TickManager.TicksGame - lastRecruitOfferTick >= RecruitOfferCooldown;
		}

		public int ContractEndFor(Pawn p)
		{
			for (int i = 0; i < workers.Count; i++)
			{
				if (workers[i].pawn == p) return workers[i].departTick;
			}
			return -1;
		}

		private void ReleaseWorker(TollWorker w)
		{
			Pawn p = w.pawn;
			if (p == null) return;
			if (p.health?.hediffSet != null)
			{
				Hediff h = p.health.hediffSet.GetFirstHediffOfDef(RimTollDefOf.RimToll_UnderContract);
				if (h != null) p.health.RemoveHediff(h);
			}
			if (p.Dead || p.Faction != Faction.OfPlayer) return;
			bool canGoHome = w.origin != null && !w.origin.defeated && !w.origin.HostileTo(Faction.OfPlayer);
			if (!canGoHome)
			{
				Messages.Message("RimToll.MessageContractStays".Translate(p.LabelShortCap),
					new LookTargets(p), MessageTypeDefOf.PositiveEvent);
				return;
			}
			p.ownership?.UnclaimAll();
			if (p.drafter != null) p.drafter.Drafted = false;
			p.jobs?.StopAll();
			int left = DropColonyGear(p, w.arrivedWith);
			p.SetFaction(w.origin);
			if (p.Spawned && !p.Downed && RCellFinder.TryFindBestExitSpot(p, out IntVec3 spot))
			{
				Job job = JobMaker.MakeJob(JobDefOf.Goto, spot);
				job.exitMapOnArrival = true;
				p.jobs?.StartJob(job, JobCondition.InterruptForced);
			}
			string msg = left > 0
				? "RimToll.MessageContractOverGear".Translate(p.LabelShortCap, left).Resolve()
				: "RimToll.MessageContractOver".Translate(p.LabelShortCap).Resolve();
			Messages.Message(msg, new LookTargets(p), MessageTypeDefOf.NeutralEvent);
		}

		private static List<int> GearIds(Pawn p)
		{
			var ids = new List<int>();
			if (p.apparel != null)
				foreach (Apparel a in p.apparel.WornApparel) ids.Add(a.thingIDNumber);
			if (p.equipment != null)
				foreach (ThingWithComps t in p.equipment.AllEquipmentListForReading) ids.Add(t.thingIDNumber);
			if (p.inventory != null)
				foreach (Thing t in p.inventory.innerContainer) ids.Add(t.thingIDNumber);
			return ids;
		}

		private static int DropColonyGear(Pawn p, List<int> keep)
		{
			if (keep == null || !p.Spawned) return 0;
			int count = 0;
			IntVec3 at = p.Position;
			if (p.apparel != null)
			{
				foreach (Apparel a in p.apparel.WornApparel.ToList())
				{
					if (!keep.Contains(a.thingIDNumber) && p.apparel.TryDrop(a, out _, at, forbid: false)) count++;
				}
			}
			if (p.equipment != null)
			{
				foreach (ThingWithComps t in p.equipment.AllEquipmentListForReading.ToList())
				{
					if (!keep.Contains(t.thingIDNumber) && p.equipment.TryDropEquipment(t, out _, at, forbid: false)) count++;
				}
			}
			if (p.inventory != null)
			{
				foreach (Thing t in p.inventory.innerContainer.ToList())
				{
					if (!keep.Contains(t.thingIDNumber) && p.inventory.innerContainer.TryDrop(t, at, p.Map, ThingPlaceMode.Near, out _)) count++;
				}
			}
			return count;
		}

		private void WarnContractEnding(TollWorker w)
		{
			w.warned = true;
			Pawn p = w.pawn;
			string text = "RimToll.LetterContractEndingText".Translate(p.LabelShortCap).Resolve();
			if (w.arrivedWith != null) text += "\n\n" + "RimToll.LetterContractEndingGear".Translate(p.LabelShortCap).Resolve();
			Find.LetterStack.ReceiveLetter("RimToll.LetterContractEndingLabel".Translate(p.LabelShortCap),
				text, LetterDefOf.NeutralEvent, new LookTargets(p));
		}

		public int EndContractsTomorrow()
		{
			int now = Find.TickManager.TicksGame;
			for (int i = 0; i < workers.Count; i++)
			{
				workers[i].departTick = now + 60000;
				workers[i].warned = false;
			}
			return workers.Count;
		}

		public Faction MostResentfulFaction(float minimum)
		{
			int now = Find.TickManager.TicksGame;
			Faction best = null;
			float bestValue = minimum;
			for (int i = 0; i < memories.Count; i++)
			{
				FactionTollMemory m = memories[i];
				if (m.faction == null || m.faction.defeated) continue;
				float v = m.Current(now);
				if (v >= bestValue)
				{
					bestValue = v;
					best = m.faction;
				}
			}
			return best;
		}

		public void Notify_PunitiveSent(Faction faction)
		{
			FactionTollMemory m = MemoryFor(faction, createIfMissing: false);
			if (m == null) return;
			m.resentment = 0.15f;
			m.lastTollTick = Find.TickManager.TicksGame;
		}

		private void Remember(TollCase c, float amount)
		{
			if (c.faction == null || amount <= 0f) return;
			MemoryFor(c.faction, createIfMissing: true).Add(amount, Find.TickManager.TicksGame);
		}

		public void QueueOpen(Lord lord)
		{
			if (lord != null && !pendingOpens.Contains(lord)) pendingOpens.Add(lord);
		}

		private void ProcessPendingOpens()
		{
			for (int i = pendingOpens.Count - 1; i >= 0; i--)
			{
				Lord lord = pendingOpens[i];
				pendingOpens.RemoveAt(i);
				if (lord == null || lord.Map == null || lord.ownedPawns.Count == 0) continue;
				if (lord.faction == null || lord.faction.IsPlayer) continue;
				if (lord.faction.HostileTo(Faction.OfPlayer)) continue;
				if (lord.LordJob is AI.LordJob_TollVisit) continue;
				Building booth = TollUtility.NearestBooth(lord.Map, lord.ownedPawns[0].Position);
				if (booth == null) continue;
				Open(lord, booth);
			}
		}

		public void Open(Lord lord, Building booth)
		{
			if (lord.LordJob is LordJob_BegForItems) OpenBeggarCase(lord, booth);
			else OpenCase(lord, booth);
		}

		public void OpenBeggarCase(Lord lord, Building booth)
		{
			Pawn spokesman = TollUtility.PickPayer(lord);
			TollCase c = CreateCase(lord, booth, spokesman, TollUtility.TollFor(lord.ownedPawns), allowRecruit: false, swap: false);
			if (c == null) return;
			c.isBeggars = true;
			c.recruit = TollUtility.PickRecruit(lord, null, 1f);
			if (c.recruit == null)
			{
				cases.Remove(c);
				return;
			}
			c.recruitDays = Rand.RangeInclusive(8, 30);
			c.letterSent = true;
			int timeout = (RimTollMod.Settings?.letterHours ?? RimTollSettings.DefLetterHours) * 2500;
			c.deadlineTick = Find.TickManager.TicksGame + timeout;
			string text = "RimToll.BeggarLetterText".Translate(c.faction.Name, c.recruit.LabelShortCap, c.recruitDays);
			SendLetter(c, "RimToll.BeggarLetterLabel".Translate(c.faction.Name), text);
		}

		public void OpenArmyCase(Lord lord, Building booth, Faction target, Pawn general)
		{
			TollCase c = CreateCase(lord, booth, general, TollUtility.TollFor(lord.ownedPawns, 1200f), allowRecruit: false);
			if (c == null) return;
			c.isArmy = true;
			c.payInKind = false;
			c.targetFaction = target;
			c.pass = TollUtility.MakePass(lord, general, TollPass.Genuine, military: true);
			Approaching(c, "RimToll.MessageArmyApproaching", MessageTypeDefOf.ThreatSmall);
		}

		public void OpenCase(Lord lord, Building booth, int forgery = TollPass.Genuine)
		{
			Pawn payer = TollUtility.PickPayer(lord);
			TollCase c = CreateCase(lord, booth, payer, TollUtility.TollFor(lord.ownedPawns), allowRecruit: true);
			if (c == null) return;
			c.pass = TollUtility.MakePass(lord, payer, forgery);
			Approaching(c, "RimToll.MessageApproaching", MessageTypeDefOf.NeutralEvent);
		}

		private void Approaching(TollCase c, string key, MessageTypeDef type)
		{
			if (c.faction == null || !c.LordAlive) return;
			Messages.Message(key.Translate(c.faction.Name),
				new LookTargets(c.lord.ownedPawns[0]), type, historical: false);
		}

		public void Notify_AtBooth(int id)
		{
			TollCase c = GetCase(id);
			if (c == null || c.letterSent || c.state != TollCase.Offered) return;
			if (!c.LordAlive || !c.BoothOk) return;
			c.letterSent = true;
			int timeout = (RimTollMod.Settings?.letterHours ?? RimTollSettings.DefLetterHours) * 2500;
			c.deadlineTick = Find.TickManager.TicksGame + timeout;
			if (c.isArmy) SendArmyLetter(c);
			else SendTollLetter(c);
		}

		private void SendTollLetter(TollCase c)
		{
			float ratio = TollUtility.StrengthRatio(c.lord.ownedPawns, c.map, c.booth.Position);
			string text = "RimToll.LetterText".Translate(c.faction.Name, TollUtility.StrengthDesc(ratio));
			string fear = TollUtility.FearDesc(TollUtility.FearAt(c.map, c.booth.Position));
			if (fear != null) text += "\n\n" + fear;
			if (c.payInKind) text += "\n\n" + "RimToll.PayInKind".Translate();
			if (c.recruit != null) text += "\n\n" + "RimToll.RecruitOffer".Translate(c.recruit.LabelShortCap, c.recruitDays);
			if (c.pass != null) text += "\n\n" + "RimToll.PassShownShort".Translate(c.pass.holderName);
			string memory = TollUtility.ResentmentDesc(ResentmentFor(c.faction));
			if (memory != null) text += "\n\n" + memory;
			SendLetter(c, "RimToll.LetterLabel".Translate(c.faction.Name), text);
		}

		private void SendArmyLetter(TollCase c)
		{
			string text = "RimToll.ArmyLetterText".Translate(c.faction.Name, c.targetFaction?.Name ?? "?",
				c.payer?.LabelShortCap ?? "?", c.lord.ownedPawns.Count);
			string fear = TollUtility.FearDesc(TollUtility.FearAt(c.map, c.booth.Position));
			if (fear != null) text += "\n\n" + fear;
			string memory = TollUtility.ResentmentDesc(ResentmentFor(c.faction));
			if (memory != null) text += "\n\n" + memory;
			SendLetter(c, "RimToll.ArmyLetterLabel".Translate(c.faction.Name), text);
		}

		private TollCase CreateCase(Lord lord, Building booth, Pawn payer, int baseAmount, bool allowRecruit, bool swap = true)
		{
			for (int i = 0; i < cases.Count; i++)
			{
				if (cases[i].lord == lord) return null;
			}
			if (payer == null || baseAmount <= 0 || lord.faction == null) return null;
			float capacity = TollUtility.PayCapacity(lord.ownedPawns, out int purse);
			bool inKind = purse < baseAmount && capacity - purse >= baseAmount * 0.5f && Rand.Chance(0.35f);
			int timeout = (RimTollMod.Settings?.letterHours ?? RimTollSettings.DefLetterHours) * 2500;
			var c = new TollCase
			{
				id = nextId++,
				state = TollCase.Offered,
				lord = lord,
				map = lord.Map,
				faction = lord.faction,
				booth = booth,
				originalJob = lord.LordJob,
				payer = payer,
				baseAmount = baseAmount,
				amount = baseAmount,
				payInKind = inKind,
				recruit = allowRecruit && RecruitAllowed()
					? TollUtility.PickRecruit(lord, payer, purse < baseAmount ? 0.08f : 0.03f)
					: null,
				recruitDays = Rand.RangeInclusive(5, 30),
				deadlineTick = Find.TickManager.TicksGame + timeout,
			};
			if (c.recruit != null) lastRecruitOfferTick = Find.TickManager.TicksGame;
			cases.Add(c);
			if (swap) pendingSwaps.Add(c.id);
			return c;
		}

		private void SendLetter(TollCase c, string label, string text)
		{
			int timeout = Mathf.Max(1, c.deadlineTick - Find.TickManager.TicksGame);
			LookTargets targets = c.LordAlive
				? new LookTargets(c.lord.ownedPawns.Cast<Thing>())
				: new LookTargets(c.booth);
			var letter = (ChoiceLetter_TollDemand)LetterMaker.MakeLetter(label, text,
				RimTollDefOf.RimToll_TollDemand, targets, c.faction);
			letter.caseId = c.id;
			letter.StartTimeout(timeout);
			Find.LetterStack.ReceiveLetter(letter);
		}

		private void TrySwapLord(TollCase c)
		{
			if (c == null) return;
			if (!c.LordAlive || !c.BoothOk || c.lord.LordJob is LordJob_TollVisit)
			{
				return;
			}
			Lord old = c.lord;
			Map map = old.Map;
			if (map == null) return;
			IntVec3 dest = TollUtility.OriginalDest(old);
			IntVec3 waitSpot = TollUtility.FindWaitSpot(c.booth, old.ownedPawns[0].Position, map);
			List<Pawn> pawns = old.ownedPawns.ToList();
			old.RemoveAllPawns();
			map.lordManager.RemoveLord(old);
			c.lord = LordMaker.MakeNewLord(c.faction, new LordJob_TollVisit(c.id, waitSpot, dest), map, pawns);
		}

		public void DemandToll(int id, int level)
		{
			TollCase c = GetCase(id);
			if (c == null) return;
			RemoveLetterFor(id);
			EnsureSwapped(c);
			if (!c.LordAlive || !c.BoothOk)
			{
				CancelCase(c);
				return;
			}
			if (!TollUtility.PayerOk(c.payer, c.lord))
			{
				c.payer = TollUtility.PickPayer(c.lord);
				if (c.payer == null)
				{
					CancelCase(c);
					return;
				}
			}
			c.demandLevel = level;
			c.amount = TollUtility.AmountForLevel(c.baseAmount, level);
			float ratio = TollUtility.StrengthRatio(c.lord.ownedPawns, c.map, c.booth?.Position ?? IntVec3.Invalid);
			int goodwill = c.faction?.PlayerGoodwill ?? 0;
			float resentment = ResentmentFor(c.faction);
			c.fear = TollUtility.FearAt(c.map, c.booth?.Position ?? IntVec3.Invalid);
			switch (TollUtility.DecideDemand(level, ratio, goodwill, resentment, c.fear))
			{
				case TollReaction.Pay:
					c.state = TollCase.Paying;
					if (c.lord.LordJob is LordJob_TollVisit)
					{
						c.lord.CurLordToil?.UpdateAllDuties();
						c.payer.jobs?.EndCurrentJob(JobCondition.InterruptForced);
					}
					break;
				case TollReaction.TurnBack:
					Announce(c, "RimToll.MessageRefusedPrice");
					Sour(c, Mathf.RoundToInt(3 + level * 2 + resentment * 3f), RimTollDefOf.RimToll_Overcharged);
					Remember(c, 0.25f);
					ArmyOutcome(c, crossed: false, targetDelta: 10, armyDelta: 0);
					CancelCase(c, restore: false);
					break;
				default:
					Announce(c, "RimToll.MessageForcedPrice");
					ArmyOutcome(c, crossed: true, targetDelta: -8, armyDelta: 0);
					ForgedSlipsThrough(c);
					CancelCase(c);
					break;
			}
		}

		public void RefusePassage(int id)
		{
			TollCase c = GetCase(id);
			if (c == null) return;
			RemoveLetterFor(id);
			EnsureSwapped(c);
			if (!c.LordAlive)
			{
				CancelCase(c);
				return;
			}
			float ratio = TollUtility.StrengthRatio(c.lord.ownedPawns, c.map, c.booth?.Position ?? IntVec3.Invalid);
			float fear = TollUtility.FearAt(c.map, c.booth?.Position ?? IntVec3.Invalid);
			if (TollUtility.DecideRefusal(ratio, fear) == TollReaction.Force)
			{
				Announce(c, "RimToll.MessageForcedBarrier");
				ArmyOutcome(c, crossed: true, targetDelta: -8, armyDelta: 0);
				ForgedSlipsThrough(c);
				CancelCase(c);
				return;
			}
			Announce(c, "RimToll.MessageTurnedAway");
			Sour(c, Mathf.RoundToInt(8 + ResentmentFor(c.faction) * 3f), RimTollDefOf.RimToll_TurnedAway);
			Remember(c, 0.3f);
			ArmyOutcome(c, crossed: false, targetDelta: 10, armyDelta: 0);
			CancelCase(c, restore: false);
		}

		public void AcceptRecruit(int id)
		{
			TollCase c = GetCase(id);
			if (c == null) return;
			RemoveLetterFor(id);
			EnsureSwapped(c);
			Pawn joiner = c.recruit;
			if (joiner == null || joiner.Dead || !joiner.Spawned || joiner.Faction == Faction.OfPlayer)
			{
				CancelCase(c);
				return;
			}
			Faction origin = joiner.Faction;
			joiner.jobs?.StopAll();
			joiner.GetLord()?.Notify_PawnLost(joiner, PawnLostCondition.LeftVoluntarily);
			joiner.SetFaction(Faction.OfPlayer);
			joiner.ClearMind_NewTemp(ifLayingKeepLaying: false, clearInspiration: false);
			int days = Mathf.Max(1, c.recruitDays);
			workers.Add(new TollWorker
			{
				pawn = joiner,
				origin = origin,
				departTick = Find.TickManager.TicksGame + days * 60000,
				arrivedWith = GearIds(joiner),
			});
			if (RimTollDefOf.RimToll_UnderContract != null && joiner.health?.hediffSet != null
				&& !joiner.health.hediffSet.HasHediff(RimTollDefOf.RimToll_UnderContract))
			{
				joiner.health.AddHediff(RimTollDefOf.RimToll_UnderContract);
			}
			Messages.Message("RimToll.MessageRecruited".Translate(joiner.LabelShortCap, days),
				new LookTargets(joiner), MessageTypeDefOf.PositiveEvent);
			CancelCase(c);
		}

		public void AccusePass(int id)
		{
			TollCase c = GetCase(id);
			if (c == null) return;
			RemoveLetterFor(id);
			EnsureSwapped(c);
			if (c.pass != null && c.pass.Forged)
			{
				int loot = DropTheirPacks(c);
				string key = loot > 0 ? "RimToll.MessageUnmasked" : "RimToll.MessageUnmaskedEmpty";
				string text = key.Translate(loot).ToString() + " "
					+ ("RimToll.Reveal" + c.pass.forgery).Translate();
				Messages.Message(text, new LookTargets(c.booth), MessageTypeDefOf.PositiveEvent);
				CancelCase(c, restore: false);
				return;
			}
			Announce(c, "RimToll.MessageWronglyAccused");
			Sour(c, 12, RimTollDefOf.RimToll_Accused);
			Remember(c, 0.35f);
			CancelCase(c, restore: false);
		}

		public void LetPass(int id)
		{
			TollCase c = GetCase(id);
			if (c == null) return;
			RemoveLetterFor(id);
			if (c.isBeggars)
			{
				CancelCase(c);
				return;
			}
			ArmyOutcome(c, crossed: true, targetDelta: -15, armyDelta: 8);
			if (!c.isArmy) Thank(c);
			ForgedSlipsThrough(c);
			CancelCase(c);
		}

		private void Thank(TollCase c)
		{
			if (c.faction == null) return;
			FactionTollMemory m = MemoryFor(c.faction, createIfMissing: false);
			int now = Find.TickManager.TicksGame;
			float resentment = m?.Current(now) ?? 0f;
			if (m != null && resentment > 0f) m.Add(-0.2f, now);
			if (c.faction.PlayerGoodwill >= 90) return;
			int gain = resentment > 0.5f ? 1 : 2;
			c.faction.TryAffectGoodwillWith(Faction.OfPlayer, gain, canSendMessage: true,
				canSendHostilityLetter: false, RimTollDefOf.RimToll_WavedThrough);
		}

		private int DropTheirPacks(TollCase c)
		{
			if (!c.LordAlive || !c.BoothOk) return 0;
			int worth = 0;
			List<Pawn> pawns = c.lord.ownedPawns;
			for (int i = 0; i < pawns.Count; i++)
			{
				ThingOwner inv = pawns[i]?.inventory?.innerContainer;
				if (inv == null) continue;
				List<Thing> carried = inv.ToList();
				for (int j = 0; j < carried.Count; j++)
				{
					Thing t = carried[j];
					if (!TollUtility.Tradeable(t)) continue;
					Thing split = inv.Take(t, t.stackCount);
					if (split == null) continue;
					worth += Mathf.RoundToInt(split.MarketValue * split.stackCount);
					GenPlace.TryPlaceThing(split, c.booth.Position, c.map, ThingPlaceMode.Near);
				}
			}
			return worth;
		}

		private void ForgedSlipsThrough(TollCase c)
		{
			if (c.pass == null || !c.pass.Forged || c.map == null) return;
			threats.Add(new TollThreat
			{
				map = c.map,
				cover = c.faction,
				fireTick = Find.TickManager.TicksGame + Rand.Range(90000, 210000),
			});
		}

		public int ForceThreatsNow()
		{
			int fired = 0;
			for (int i = threats.Count - 1; i >= 0; i--)
			{
				FireThreat(threats[i]);
				threats.RemoveAt(i);
				fired++;
			}
			return fired;
		}

		private void FireThreat(TollThreat t)
		{
			if (t.map == null || !Find.Maps.Contains(t.map)) return;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, t.map);
			parms.forced = true;
			if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms)) return;
			Messages.Message("RimToll.MessageBetrayed".Translate(t.cover?.Name ?? "?"),
				MessageTypeDefOf.ThreatBig);
		}

		private void EnsureSwapped(TollCase c)
		{
			if (!pendingSwaps.Contains(c.id)) return;
			pendingSwaps.Remove(c.id);
			TrySwapLord(c);
		}

		private void Announce(TollCase c, string key)
		{
			if (c.faction == null) return;
			LookTargets targets = c.LordAlive ? new LookTargets(c.lord.ownedPawns[0]) : null;
			Messages.Message(key.Translate(c.faction.Name), targets, MessageTypeDefOf.NeutralEvent);
		}

		private void ArmyOutcome(TollCase c, bool crossed, int targetDelta, int armyDelta)
		{
			if (!c.isArmy) return;
			if (armyDelta != 0)
			{
				c.faction?.TryAffectGoodwillWith(Faction.OfPlayer, armyDelta, canSendMessage: true,
					canSendHostilityLetter: true, RimTollDefOf.RimToll_ArmyPassage);
			}
			if (c.targetFaction == null || targetDelta == 0) return;
			c.targetFaction.TryAffectGoodwillWith(Faction.OfPlayer, targetDelta, canSendMessage: true,
				canSendHostilityLetter: true, RimTollDefOf.RimToll_ArmyPassage);
			Messages.Message((crossed ? "RimToll.MessageArmyCrossed" : "RimToll.MessageArmyStopped")
				.Translate(c.targetFaction.Name, c.faction?.Name ?? "?"), MessageTypeDefOf.NeutralEvent);
		}

		private void Sour(TollCase c, int amount, HistoryEventDef reason)
		{
			c.faction?.TryAffectGoodwillWith(Faction.OfPlayer, -amount, canSendMessage: true,
				canSendHostilityLetter: true, reason);
		}

		private void CancelCase(TollCase c, bool restore = true)
		{
			pendingSwaps.Remove(c.id);
			if (!c.LordAlive || !(c.lord.LordJob is LordJob_TollVisit))
			{
				cases.Remove(c);
				return;
			}
			if (!restore)
			{
				cases.Remove(c);
				c.lord.ReceiveMemo(LordJob_TollVisit.MemoTurnBack);
				return;
			}
			c.state = TollCase.Done;
			if (!pendingRestores.Contains(c.id)) pendingRestores.Add(c.id);
		}

		private void RestoreLord(TollCase c)
		{
			if (c == null) return;
			cases.Remove(c);
			if (!c.LordAlive || !(c.lord.LordJob is LordJob_TollVisit)) return;
			Lord old = c.lord;
			Map map = old.Map;
			if (c.originalJob == null || map == null)
			{
				old.ReceiveMemo(LordJob_TollVisit.MemoAborted);
				return;
			}
			List<Pawn> pawns = old.ownedPawns.ToList();
			old.RemoveAllPawns();
			map.lordManager.RemoveLord(old);
			SuppressPatch = true;
			try
			{
				LordMaker.MakeNewLord(c.faction, c.originalJob, map, pawns);
			}
			finally
			{
				SuppressPatch = false;
			}
		}

		public void Notify_TollPaid(int id)
		{
			TollCase c = GetCase(id);
			if (c == null) return;
			Messages.Message((c.payInKind ? "RimToll.MessagePaidInKind" : "RimToll.MessagePaid")
					.Translate(c.faction?.Name ?? "?", c.amount),
				new LookTargets(c.booth), MessageTypeDefOf.PositiveEvent);
			float resentment = ResentmentFor(c.faction);
			int malus = Mathf.RoundToInt(c.demandLevel * 1.5f + resentment * 4f);
			if (malus > 0) Sour(c, malus, RimTollDefOf.RimToll_Tolled);
			Remember(c, 0.08f + c.demandLevel * 0.14f + c.fear * 0.15f);
			totalCollected += c.amount;
			FactionTollMemory m = MemoryFor(c.faction, createIfMissing: false);
			if (m != null)
			{
				m.totalPaid += c.amount;
				m.timesTolled++;
			}
			ArmyOutcome(c, crossed: true, targetDelta: -8, armyDelta: 0);
			ForgedSlipsThrough(c);
			CancelCase(c);
		}

		public void Notify_BoothSuspended(Building booth)
		{
			for (int i = cases.Count - 1; i >= 0; i--)
			{
				TollCase c = cases[i];
				if (c.booth != booth || c.state == TollCase.Done) continue;
				RemoveLetterFor(c.id);
				CancelCase(c);
			}
		}

		public void Notify_TollAborted(int id)
		{
			TollCase c = GetCase(id);
			if (c == null) return;
			RemoveLetterFor(id);
			if (c.faction != null && c.letterSent)
			{
				Messages.Message("RimToll.MessageAborted".Translate(c.faction.Name), MessageTypeDefOf.NeutralEvent);
			}
			cases.Remove(c);
		}

		public override void GameComponentTick()
		{
			if (pendingOpens.Count > 0) ProcessPendingOpens();
			if (pendingSwaps.Count > 0)
			{
				for (int i = 0; i < pendingSwaps.Count; i++)
				{
					TrySwapLord(GetCase(pendingSwaps[i]));
				}
				pendingSwaps.Clear();
			}
			if (pendingRestores.Count > 0)
			{
				for (int i = 0; i < pendingRestores.Count; i++)
				{
					RestoreLord(GetCase(pendingRestores[i]));
				}
				pendingRestores.Clear();
			}
			int now = Find.TickManager.TicksGame;
			if (now % CheckInterval != 0) return;
			for (int i = threats.Count - 1; i >= 0; i--)
			{
				if (now < threats[i].fireTick) continue;
				FireThreat(threats[i]);
				threats.RemoveAt(i);
			}
			for (int i = workers.Count - 1; i >= 0; i--)
			{
				TollWorker w = workers[i];
				if (!w.Valid) { workers.RemoveAt(i); continue; }
				if (!w.warned && now >= w.departTick - 60000) WarnContractEnding(w);
				if (now >= w.departTick)
				{
					if (!w.pawn.Spawned) continue;
					ReleaseWorker(w);
					workers.RemoveAt(i);
				}
			}
			if (cases.Count == 0) return;
			for (int i = cases.Count - 1; i >= 0; i--)
			{
				TollCase c = cases[i];
				if (c.state == TollCase.Done) continue;
				if (!c.LordAlive || !c.BoothOk)
				{
					RemoveLetterFor(c.id);
					CancelCase(c);
				}
				else if (c.state == TollCase.Offered && c.letterSent && now >= c.deadlineTick)
				{
					LetPass(c.id);
				}
			}
		}

		private void RemoveLetterFor(int id)
		{
			ChoiceLetter_TollDemand letter = Find.LetterStack.LettersListForReading
				.OfType<ChoiceLetter_TollDemand>().FirstOrDefault(l => l.caseId == id);
			if (letter != null) Find.LetterStack.RemoveLetter(letter);
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Collections.Look(ref cases, "cases", LookMode.Deep);
			Scribe_Collections.Look(ref memories, "memories", LookMode.Deep);
			Scribe_Collections.Look(ref workers, "workers", LookMode.Deep);
			Scribe_Collections.Look(ref threats, "threats", LookMode.Deep);
			Scribe_Collections.Look(ref pendingSwaps, "pendingSwaps", LookMode.Value);
			Scribe_Collections.Look(ref pendingRestores, "pendingRestores", LookMode.Value);
			Scribe_Values.Look(ref nextId, "nextId", 1);
			Scribe_Values.Look(ref totalCollected, "totalCollected");
			Scribe_Values.Look(ref lastRecruitOfferTick, "lastRecruitOfferTick", -999999);
			if (Scribe.mode == LoadSaveMode.PostLoadInit)
			{
				if (cases == null) cases = new List<TollCase>();
				if (memories == null) memories = new List<FactionTollMemory>();
				if (workers == null) workers = new List<TollWorker>();
				if (threats == null) threats = new List<TollThreat>();
				threats.RemoveAll(t => t == null || t.map == null);
				memories.RemoveAll(m => m == null || m.faction == null);
				workers.RemoveAll(w => w == null || w.pawn == null);
				if (pendingSwaps == null) pendingSwaps = new List<int>();
				if (pendingRestores == null) pendingRestores = new List<int>();
				cases.RemoveAll(c => c == null || c.map == null);
			}
		}
	}
}
