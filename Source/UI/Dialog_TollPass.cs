using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimToll.UI
{
	public class Dialog_TollPass : Window
	{
		private static readonly Color Parchment = new Color(0.867f, 0.812f, 0.663f);
		private static readonly Color ParchmentDeep = new Color(0.780f, 0.714f, 0.545f);
		private static readonly Color ParchmentEdge = new Color(0.541f, 0.478f, 0.322f);
		private static readonly Color Ink = new Color(0.161f, 0.129f, 0.078f);
		private static readonly Color InkFaint = new Color(0.451f, 0.384f, 0.243f);
		private static readonly Color Wax = new Color(0.494f, 0.153f, 0.125f);
		private static readonly Color Desk = new Color(0.153f, 0.118f, 0.09f);
		private static readonly Color SheetShadow = new Color(0f, 0f, 0f, 0.35f);
		private static readonly Color StampInkGreen = new Color(0.16f, 0.42f, 0.19f);
		private static readonly Color StampInkRed = new Color(0.55f, 0.13f, 0.10f);

		private const float PortraitZoom = 2.3f;
		private const float PortraitRise = 0.42f;

		private static readonly Color NoteInk = new Color(0.85f, 0.79f, 0.64f);
		private const float NoteLineH = 18f;

		public readonly int caseId;
		private int armedStamp;
		private string[] notes;

		public override Vector2 InitialSize
		{
			get
			{
				TollCase c = GameComponent_TollState.Instance?.GetCase(caseId);
				int n = c == null ? 0 : Notes(c).Length;
				return new Vector2(600f, 570f + (n > 0 ? n * NoteLineH + 8f : 0f));
			}
		}

		private string[] Notes(TollCase c)
		{
			if (notes != null) return notes;
			var list = new List<string>();
			if (c.LordAlive && c.map != null && c.BoothOk)
			{
				float ratio = TollUtility.StrengthRatio(c.lord.ownedPawns, c.map, c.booth.Position);
				list.Add(TollUtility.StrengthDesc(ratio));
				string fear = TollUtility.FearDesc(TollUtility.FearAt(c.map, c.booth.Position));
				if (fear != null) list.Add(fear);
			}
			if (c.payInKind) list.Add("RimToll.WindowPayInKind".Translate());
			if (c.recruit != null && !c.recruit.Dead && c.recruit.Spawned)
			{
				list.Add("RimToll.WindowRecruit".Translate(c.recruit.LabelShortCap, c.recruitDays));
			}
			notes = list.ToArray();
			return notes;
		}

		public Dialog_TollPass(int caseId)
		{
			this.caseId = caseId;
			forcePause = true;
			absorbInputAroundWindow = true;
			doCloseX = true;
		}

		public static void OpenFor(int caseId)
		{
			List<Window> open = Find.WindowStack.Windows.ToList();
			for (int i = 0; i < open.Count; i++)
			{
				if (open[i] is Dialog_TollPass d && d.caseId == caseId) return;
			}
			Find.WindowStack.Add(new Dialog_TollPass(caseId));
		}

		public override void DoWindowContents(Rect inRect)
		{
			GameComponent_TollState comp = GameComponent_TollState.Instance;
			TollCase c = comp?.GetCase(caseId);
			if (c?.pass == null || !c.CanDemand)
			{
				Close();
				return;
			}

			Widgets.DrawBoxSolid(inRect, Desk);
			float rowH = 34f;
			int rows = c.recruit != null ? 2 : 1;
			float buttonsH = rows * rowH + (rows - 1) * 6f;
			float toolsH = 62f;
			string[] lines = Notes(c);
			float notesH = lines.Length > 0 ? lines.Length * NoteLineH + 8f : 0f;
			Rect sheet = new Rect(inRect.x + 12f, inRect.y + 22f, inRect.width - 24f,
				inRect.height - toolsH - buttonsH - notesH - 52f);

			Matrix4x4 matrix = GUI.matrix;
			Verse.UI.RotateAroundPivot(-1.2f, sheet.center);
			if (RimTollTex.Sheet != null)
			{
				GUI.color = Color.white;
				float ex = sheet.width * (20f / 1080f);
				float ey = sheet.height * (20f / 760f);
				GUI.DrawTexture(new Rect(sheet.x - ex, sheet.y - ey, sheet.width + ex * 2f, sheet.height + ey * 2f),
					RimTollTex.Sheet);
			}
			else
			{
				Widgets.DrawBoxSolid(new Rect(sheet.x + 4f, sheet.y + 6f, sheet.width, sheet.height), SheetShadow);
			}
			DrawSheet(sheet, c);
			GUI.matrix = matrix;

			if (lines.Length > 0)
			{
				Text.Font = GameFont.Tiny;
				GUI.color = NoteInk;
				float ny = sheet.yMax + 12f;
				for (int i = 0; i < lines.Length; i++)
				{
					Widgets.Label(new Rect(inRect.x + 18f, ny, inRect.width - 36f, NoteLineH), lines[i]);
					ny += NoteLineH;
				}
				GUI.color = Color.white;
				Text.Font = GameFont.Small;
			}

			Rect tools = new Rect(inRect.x, sheet.yMax + 12f + notesH, inRect.width, toolsH);
			DrawStampTools(tools);
			if (c.verdict != 0)
			{
				DrawButtons(new Rect(inRect.x, tools.yMax + 4f, inRect.width, buttonsH), c, comp, rowH);
			}
			HandleStampInput(sheet, c);
		}

		private void DrawStampTools(Rect area)
		{
			float size = 54f;
			float gap = 46f;
			float cx = area.x + area.width / 2f;
			DrawStampTool(new Rect(cx - gap / 2f - size, area.y + 4f, size, size), 1,
				RimTollTex.StampGreen, StampInkGreen, "RimToll.StampApproveTip");
			DrawStampTool(new Rect(cx + gap / 2f, area.y + 4f, size, size), 2,
				RimTollTex.StampRed, StampInkRed, "RimToll.StampDenyTip");
		}

		private void DrawStampTool(Rect r, int type, Texture2D tex, Color fallback, string tipKey)
		{
			if (armedStamp == type) Widgets.DrawBoxSolid(r.ExpandedBy(4f), new Color(1f, 1f, 1f, 0.14f));
			else if (Mouse.IsOver(r)) Widgets.DrawBoxSolid(r.ExpandedBy(2f), new Color(1f, 1f, 1f, 0.07f));
			GUI.color = Color.white;
			if (tex != null) GUI.DrawTexture(r, tex);
			else Widgets.DrawBoxSolid(r.ContractedBy(10f), fallback);
			TooltipHandler.TipRegion(r, tipKey.Translate());
			if (Widgets.ButtonInvisible(r)) armedStamp = armedStamp == type ? 0 : type;
		}

		private void HandleStampInput(Rect sheet, TollCase c)
		{
			if (armedStamp == 0) return;
			Vector2 m = Event.current.mousePosition;
			Rect zone = sheet.ContractedBy(26f);
			if (zone.Contains(m))
			{
				DrawMarkAt(sheet, Mathf.InverseLerp(sheet.x, sheet.xMax, m.x),
					Mathf.InverseLerp(sheet.y, sheet.yMax, m.y), -12f, armedStamp, 0.35f);
			}
			else
			{
				Texture2D tex = armedStamp == 1 ? RimTollTex.StampGreen : RimTollTex.StampRed;
				if (tex != null)
				{
					GUI.color = new Color(1f, 1f, 1f, 0.85f);
					GUI.DrawTexture(new Rect(m.x - 20f, m.y - 34f, 40f, 40f), tex);
					GUI.color = Color.white;
				}
			}
			if (Event.current.type != EventType.MouseDown) return;
			if (Event.current.button == 1)
			{
				armedStamp = 0;
				Event.current.Use();
				return;
			}
			if (Event.current.button != 0) return;
			if (!zone.Contains(m))
			{
				armedStamp = 0;
				return;
			}
			var mark = new TollStampMark
			{
				type = armedStamp,
				x = Mathf.Clamp(Mathf.InverseLerp(sheet.x, sheet.xMax, m.x), 0.14f, 0.86f),
				y = Mathf.Clamp(Mathf.InverseLerp(sheet.y, sheet.yMax, m.y), 0.14f, 0.86f),
				angle = -12f + Rand.Range(-5f, 5f),
			};
			c.stamps.Add(mark);
			if (c.stamps.Count > 6) c.stamps.RemoveAt(0);
			c.verdict = mark.type;
			armedStamp = 0;
			SoundDefOf.Designate_PlaceBuilding.PlayOneShotOnCamera();
			Event.current.Use();
		}

		private void DrawMarkAt(Rect sheet, float nx, float ny, float angle, int type, float alpha)
		{
			float w = 168f;
			float h = 58f;
			Rect r = new Rect(sheet.x + nx * sheet.width - w / 2f, sheet.y + ny * sheet.height - h / 2f, w, h);
			Matrix4x4 mtx = GUI.matrix;
			Verse.UI.RotateAroundPivot(angle, r.center);
			Color ink = type == 1 ? StampInkGreen : StampInkRed;
			ink.a = alpha;
			GUI.color = ink;
			if (RimTollTex.StampMark != null) GUI.DrawTexture(r, RimTollTex.StampMark);
			Text.Anchor = TextAnchor.MiddleCenter;
			Text.Font = GameFont.Medium;
			Widgets.Label(r, (type == 1 ? "RimToll.StampApproved" : "RimToll.StampDenied").Translate());
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;
			GUI.color = Color.white;
			GUI.matrix = mtx;
		}

		private void DrawSheet(Rect sheet, TollCase c)
		{
			if (RimTollTex.Sheet == null)
			{
				Widgets.DrawBoxSolid(sheet, Parchment);
				Widgets.DrawBox(sheet, 2, SolidColorMaterials.NewSolidColorTexture(ParchmentEdge));
				Widgets.DrawBox(sheet.ContractedBy(7f), 1, SolidColorMaterials.NewSolidColorTexture(ParchmentDeep));
			}

			Rect body = sheet.ContractedBy(24f);
			float y = body.y + 2f;

			Text.Anchor = TextAnchor.UpperCenter;
			Text.Font = GameFont.Medium;
			GUI.color = Ink;
			Widgets.Label(new Rect(body.x, y, body.width, 34f), "RimToll.PassTitle".Translate());
			y += 34f;
			Text.Font = GameFont.Small;
			GUI.color = InkFaint;
			Widgets.Label(new Rect(body.x, y, body.width, 24f), c.faction?.Name ?? "");
			y += 30f;
			Text.Anchor = TextAnchor.UpperLeft;

			GUI.color = ParchmentDeep;
			Widgets.DrawLineHorizontal(body.x, y, body.width);
			GUI.color = Color.white;
			y += 14f;

			float portrait = 150f;
			Rect fields = new Rect(body.x, y, body.width - portrait - 20f, 170f);
			Rect frame = new Rect(body.xMax - portrait, y, portrait, portrait);

			float fy = fields.y;
			DrawField(fields, ref fy, "RimToll.PassHolder".Translate(), c.pass.holderName);
			DrawField(fields, ref fy, "RimToll.PassCount".Translate(), c.pass.claimedCount.ToString());
			DrawField(fields, ref fy, "RimToll.PassReason".Translate(), ("RimToll.Reason" + c.pass.reason).Translate());

			GUI.color = Color.white;
			Matrix4x4 photoMatrix = GUI.matrix;
			Verse.UI.RotateAroundPivot(-3f, frame.center);
			if (RimTollTex.Photo != null)
			{
				float pex = frame.width * (10f / 320f);
				GUI.DrawTexture(new Rect(frame.x - pex, frame.y - pex, frame.width + pex * 2f, frame.height + pex * 2f),
					RimTollTex.Photo);
			}
			else
			{
				Widgets.DrawBoxSolid(frame, new Color(0.925f, 0.894f, 0.796f));
				Widgets.DrawBox(frame, 1, SolidColorMaterials.NewSolidColorTexture(ParchmentEdge));
			}
			if (c.pass.portrait != null && !c.pass.portrait.Destroyed)
			{
				RenderTexture tex = PortraitsCache.Get(c.pass.portrait,
					new Vector2(frame.width - 10f, frame.height - 10f), Rot4.South,
					new Vector3(0f, 0f, PortraitRise), PortraitZoom,
					supersample: true, compensateForUIScale: true, renderHeadgear: false);
				GUI.DrawTexture(frame.ContractedBy(5f), tex);
			}
			Text.Anchor = TextAnchor.UpperCenter;
			Text.Font = GameFont.Tiny;
			GUI.color = InkFaint;
			Widgets.Label(new Rect(frame.x - 10f, frame.yMax + 3f, frame.width + 20f, 20f),
				"RimToll.PassPortrait".Translate());
			GUI.matrix = photoMatrix;
			Text.Anchor = TextAnchor.UpperLeft;
			Text.Font = GameFont.Small;

			float sealSize = 92f;
			Rect seal = new Rect(body.xMax - sealSize + 16f, body.yMax - sealSize + 18f, sealSize, sealSize);
			GUI.color = Color.white;
			Texture2D sealTex = c.pass.forgery == TollPass.FakeSeal && RimTollTex.SealGreen != null
				? RimTollTex.SealGreen : RimTollTex.Seal;
			if (sealTex != null)
			{
				Matrix4x4 matrix = GUI.matrix;
				Verse.UI.RotateAroundPivot(10f, seal.center);
				GUI.DrawTexture(seal, sealTex);
				GUI.matrix = matrix;
			}
			else
			{
				Widgets.DrawBoxSolid(seal, Wax);
				if (c.faction?.def?.FactionIcon != null)
				{
					GUI.color = c.faction.Color;
					GUI.DrawTexture(seal.ContractedBy(14f), c.faction.def.FactionIcon);
				}
			}
			string issued = GenDate.DateFullStringAt(GenTicks.TicksAbs, Vector2.zero);
			if (c.pass.forgery == TollPass.FakeMonth)
			{
				issued = issued.Replace(GenDate.Quadrum(GenTicks.TicksAbs, 0f).Label(),
					("RimToll.FakeMonth" + c.pass.fakeMonth).Translate());
			}
			GUI.color = InkFaint;
			Text.Font = GameFont.Tiny;
			Widgets.Label(new Rect(body.x, body.yMax - 44f, body.width - sealSize - 14f, 44f),
				"RimToll.PassFooter".Translate(issued));
			Text.Font = GameFont.Small;
			GUI.color = Color.white;

			for (int i = 0; i < c.stamps.Count; i++)
			{
				TollStampMark mark = c.stamps[i];
				DrawMarkAt(sheet, mark.x, mark.y, mark.angle, mark.type, 0.62f);
			}
		}

		private void DrawButtons(Rect area, TollCase c, GameComponent_TollState comp, float rowH)
		{
			float y = area.y;
			if (c.verdict == 1)
			{
				float quarter = (area.width - 18f) / 4f;
				for (int level = 0; level < 3; level++)
				{
					string key = level == 0 ? "RimToll.PassPriceFair" : (level == 1 ? "RimToll.PassPriceFirm" : "RimToll.PassPriceGouge");
					Rect r = new Rect(area.x + level * (quarter + 6f), y, quarter, rowH);
					if (Widgets.ButtonText(r, key.Translate(TollUtility.AmountForLevel(c.baseAmount, level))))
					{
						int chosen = level;
						Close();
						comp.DemandToll(caseId, chosen);
						return;
					}
				}
				if (Widgets.ButtonText(new Rect(area.x + 3f * (quarter + 6f), y, quarter, rowH),
					"RimToll.OptionLetPass".Translate()))
				{
					Close();
					comp.LetPass(caseId);
					return;
				}
				y += rowH + 6f;

				if (c.recruit != null && !c.recruit.Dead && c.recruit.Spawned)
				{
					if (Widgets.ButtonText(new Rect(area.x, y, area.width, rowH),
						"RimToll.OptionRecruit".Translate(c.recruit.LabelShortCap, c.recruitDays)))
					{
						Close();
						comp.AcceptRecruit(caseId);
					}
				}
				return;
			}

			float half = (area.width - 6f) / 2f;
			if (Widgets.ButtonText(new Rect(area.x, y, half, rowH), "RimToll.PassAccuse".Translate()))
			{
				Close();
				comp.AccusePass(caseId);
				return;
			}
			if (Widgets.ButtonText(new Rect(area.x + half + 6f, y, half, rowH), "RimToll.OptionRefuse".Translate()))
			{
				Close();
				comp.RefusePassage(caseId);
			}
		}

		private void DrawField(Rect area, ref float y, string label, string value)
		{
			GUI.color = InkFaint;
			Text.Font = GameFont.Tiny;
			Widgets.Label(new Rect(area.x, y, area.width, 18f), label);
			y += 17f;
			GUI.color = Ink;
			Text.Font = GameFont.Small;
			Widgets.Label(new Rect(area.x, y, area.width, 26f), value);
			GUI.color = Color.white;
			y += 30f;
		}
	}
}
