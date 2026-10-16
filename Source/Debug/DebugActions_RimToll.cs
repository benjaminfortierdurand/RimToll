using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using Verse;

namespace RimToll.Debug
{
	public static class DebugActions_RimToll
	{
		[DebugAction("RimToll", "Send travelers through",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnTravelers()
		{
			FireIncident("TravelerGroup");
		}

		[DebugAction("RimToll", "Send visitors",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnVisitors()
		{
			FireIncident("VisitorGroup");
		}

		[DebugAction("RimToll", "Send trade caravan",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnTraders()
		{
			FireIncident("TraderCaravanArrival");
		}

		[DebugAction("RimToll", "Send beggars (Ideology)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnBeggars()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			QuestScriptDef def = DefDatabase<QuestScriptDef>.GetNamedSilentFail("Beggars");
			if (def == null)
			{
				Log.Warning("[RimToll] Beggars quest script not found, Ideology is probably not active.");
				return;
			}
			Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(def, StorytellerUtility.DefaultThreatPointsNow(map));
			if (def.autoAccept) quest.Accept(null);
			else QuestUtility.SendLetterQuestAvailable(quest);
		}

		[DebugAction("RimToll", "Send marching army",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnArmy()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			IncidentDef def = RimTollDefOf.RimToll_MarchingArmy;
			if (def == null) return;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			if (!def.Worker.TryExecute(parms))
			{
				Log.Warning("[RimToll] no army: needs an active toll post and two visible factions at war.");
			}
		}

		[DebugAction("RimToll", "Send suspect convoy",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnFalseConvoy()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail("RimToll_FalseConvoy");
			if (def == null) return;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			if (!def.Worker.TryExecute(parms))
			{
				Log.Warning("[RimToll] no suspect convoy: needs an active toll post and a neutral humanlike faction.");
			}
		}

		[DebugAction("RimToll", "Send punitive expedition",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void SpawnPunitive()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail("RimToll_PunitiveRaid");
			if (def == null) return;
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			if (!def.Worker.TryExecute(parms))
			{
				Log.Warning("[RimToll] no expedition: needs an active toll post, a resentful faction (>= 0.9) and a faction hostile to you.");
			}
		}

		[DebugAction("RimToll", "Fire pending scout raids",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void FireScoutRaids()
		{
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			if (comp == null) return;
			int fired = comp.ForceThreatsNow();
			if (fired == 0)
			{
				Log.Warning("[RimToll] nothing pending: first let a convoy with forged papers through (paid, waved through or forced).");
				return;
			}
			Messages.Message("RimToll: fired " + fired + " scout raid(s).",
				MessageTypeDefOf.NeutralEvent, historical: false);
		}

		[DebugAction("RimToll", "End worker contracts tomorrow",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void EndContractsTomorrow()
		{
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			if (comp == null) return;
			int n = comp.EndContractsTomorrow();
			if (n == 0) Log.Warning("[RimToll] no worker under contract.");
		}

		[DebugAction("RimToll", "Log fear at toll posts",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void LogFear()
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			List<Thing> booths = map.listerThings.ThingsOfDef(RimTollDefOf.RimToll_TollBooth);
			var sb = new StringBuilder("[RimToll] fear at toll posts:");
			if (booths.Count == 0) sb.Append("\n  (no toll post on this map)");
			for (int i = 0; i < booths.Count; i++)
			{
				float fear = TollUtility.FearAt(map, booths[i].Position);
				sb.Append("\n  " + booths[i].Position + " | fear " + fear.ToString("F2")
					+ (TollUtility.FearDesc(fear) != null ? " | shown in letters" : ""));
			}
			Log.Message(sb.ToString());
		}

		[DebugAction("RimToll", "Log toll reputation",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void ShowReputation()
		{
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			if (comp == null) return;
			int now = Find.TickManager.TicksGame;
			var sb = new StringBuilder("[RimToll] toll reputation:");
			if (comp.AllMemories.Count == 0) sb.Append("\n  (no toll collected yet)");
			foreach (FactionTollMemory m in comp.AllMemories)
			{
				sb.Append("\n  " + (m.faction?.Name ?? "?")
					+ " | resentment " + m.Current(now).ToString("F2")
					+ " | tolls " + m.timesTolled
					+ " | total " + m.totalPaid
					+ " | goodwill " + (m.faction?.PlayerGoodwill ?? 0));
			}
			Log.Message(sb.ToString());
		}

		[DebugAction("RimToll", "Add resentment (+0.3)",
			actionType = DebugActionType.Action,
			allowedGameStates = AllowedGameStates.PlayingOnMap)]
		private static void AddResentment()
		{
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			if (comp == null) return;
			var options = new List<DebugMenuOption>();
			foreach (Faction f in Find.FactionManager.AllFactionsVisible)
			{
				if (f.IsPlayer) continue;
				Faction faction = f;
				options.Add(new DebugMenuOption(faction.Name, DebugMenuOptionMode.Action, delegate
				{
					comp.MemoryFor(faction, createIfMissing: true).Add(0.3f, Find.TickManager.TicksGame);
					Messages.Message("RimToll: " + faction.Name + " resentment = "
						+ comp.ResentmentFor(faction).ToString("F2"), MessageTypeDefOf.NeutralEvent, historical: false);
				}));
			}
			options.SortBy(o => o.label);
			Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
		}

		private static void FireIncident(string defName)
		{
			Map map = Find.CurrentMap;
			if (map == null) return;
			IncidentDef def = DefDatabase<IncidentDef>.GetNamedSilentFail(defName);
			if (def == null)
			{
				Log.Warning("[RimToll] incident not found: " + defName);
				return;
			}
			IncidentParms parms = StorytellerUtility.DefaultParmsNow(def.category, map);
			parms.forced = true;
			if (parms.faction == null)
			{
				bool trader = defName == "TraderCaravanArrival";
				parms.faction = Find.FactionManager.AllFactionsVisible.Where(f =>
					!f.IsPlayer && !f.defeated && !f.Hidden && !f.HostileTo(Faction.OfPlayer)
					&& f.def.humanlikeFaction
					&& (!trader || (f.def.caravanTraderKinds != null && f.def.caravanTraderKinds.Count > 0)))
					.RandomElementWithFallback();
			}
			if (!def.Worker.TryExecute(parms))
			{
				Log.Warning("[RimToll] incident " + defName + " refused to fire "
					+ "(faction picked: " + (parms.faction?.Name ?? "none") + ").");
			}
		}
	}
}
