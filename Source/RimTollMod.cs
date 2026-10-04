using HarmonyLib;
using UnityEngine;
using Verse;

namespace RimToll
{
	public class RimTollMod : Mod
	{
		public const string HarmonyId = "forti.rimtoll";

		public static RimTollSettings Settings { get; private set; }

		public RimTollMod(ModContentPack content) : base(content)
		{
			Settings = GetSettings<RimTollSettings>();
			new Harmony(HarmonyId).PatchAll();
			Log.Message("[RimToll] Loaded. The road pays.");
		}

		public override string SettingsCategory() => "RimToll";

		public override void WriteSettings()
		{
			base.WriteSettings();
			RimTollStartup.ApplyBoothCost();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			var l = new Listing_Standard();
			l.Begin(inRect);
			l.Label("RimToll.Settings.TollMultiplier".Translate() + ": x" + Settings.tollMultiplier.ToString("F1"));
			Settings.tollMultiplier = Mathf.Round(l.Slider(Settings.tollMultiplier, 0.5f, 3f) * 10f) / 10f;
			l.Label("RimToll.Settings.LetterHours".Translate() + ": " + Settings.letterHours);
			Settings.letterHours = Mathf.RoundToInt(l.Slider(Settings.letterHours, 1f, 8f));
			l.Label("RimToll.Settings.BoothCost".Translate() + ": " + Settings.boothCost);
			Settings.boothCost = 25 * Mathf.RoundToInt(l.Slider(Settings.boothCost, 0f, 2000f) / 25f);
			l.GapLine();
			l.CheckboxLabeled("RimToll.Settings.TollTravelers".Translate(), ref Settings.tollTravelers);
			l.CheckboxLabeled("RimToll.Settings.TollVisitors".Translate(), ref Settings.tollVisitors);
			l.CheckboxLabeled("RimToll.Settings.TollTraders".Translate(), ref Settings.tollTraders);
			l.CheckboxLabeled("RimToll.Settings.TollBeggars".Translate(), ref Settings.tollBeggars);
			l.GapLine();
			l.CheckboxLabeled("RimToll.Settings.ArmyEvent".Translate(), ref Settings.armyEvent);
			l.CheckboxLabeled("RimToll.Settings.PunitiveEvent".Translate(), ref Settings.punitiveEvent);
			l.CheckboxLabeled("RimToll.Settings.FalseConvoyEvent".Translate(), ref Settings.falseConvoyEvent);
			l.GapLine();
			l.CheckboxLabeled("RimToll.Settings.RequireOfficer".Translate(), ref Settings.requireOfficer,
				"RimToll.Settings.RequireOfficerTip".Translate());
			l.GapLine();
			if (l.ButtonText("RimToll.Settings.Reset".Translate()))
			{
				Settings.ResetToDefaults();
			}
			l.End();
		}
	}
}
