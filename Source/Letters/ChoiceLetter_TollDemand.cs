using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimToll.Letters
{
	public class ChoiceLetter_TollDemand : ChoiceLetter
	{
		public int caseId;

		public override bool CanDismissWithRightClick => false;

		public override IEnumerable<DiaOption> Choices
		{
			get
			{
				GameComponent_TollState comp = GameComponent_TollState.Instance;
				TollCase c = comp?.GetCase(caseId);
				if (c != null && c.CanDemand && c.isBeggars)
				{
					int beggarId = caseId;
					if (c.recruit != null && !c.recruit.Dead && c.recruit.Spawned)
					{
						yield return new DiaOption("RimToll.OptionRecruit".Translate(c.recruit.LabelShortCap, c.recruitDays))
						{
							action = () => comp.AcceptRecruit(beggarId),
							resolveTree = true,
						};
					}
					yield return new DiaOption("RimToll.OptionDismissBeggars".Translate())
					{
						action = () => comp.LetPass(beggarId),
						resolveTree = true,
					};
					if (lookTargets != null && lookTargets.IsValid)
					{
						yield return Option_JumpToLocationAndPostpone;
					}
					yield return Option_Postpone;
					yield break;
				}
				if (c != null && c.CanDemand)
				{
					if (c.pass != null)
					{
						bool sendPawn = RimTollMod.Settings != null && RimTollMod.Settings.requireOfficer;
						int id = caseId;
						yield return new DiaOption((sendPawn ? "RimToll.OptionSendOfficer" : "RimToll.OptionExamine").Translate())
						{
							action = delegate
							{
								if (sendPawn) TollUtility.PickOfficer(id);
								else UI.Dialog_TollPass.OpenFor(id);
							},
							resolveTree = true,
						};
					}
					else
					{
						for (int level = 0; level < 3; level++)
						{
							int chosen = level;
							string key = level == 0 ? "RimToll.OptionFair"
								: (level == 1 ? "RimToll.OptionFirm" : "RimToll.OptionGouge");
							yield return new DiaOption(key.Translate(TollUtility.AmountForLevel(c.baseAmount, level)))
							{
								action = () => comp.DemandToll(caseId, chosen),
								resolveTree = true,
							};
						}
						yield return new DiaOption("RimToll.OptionRefuse".Translate())
						{
							action = () => comp.RefusePassage(caseId),
							resolveTree = true,
						};
					}
					yield return new DiaOption("RimToll.OptionLetPass".Translate())
					{
						action = () => comp.LetPass(caseId),
						resolveTree = true,
					};
					if (lookTargets != null && lookTargets.IsValid)
					{
						yield return Option_JumpToLocationAndPostpone;
					}
					yield return Option_Postpone;
					yield break;
				}
				yield return Option_Close;
			}
		}

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref caseId, "caseId");
		}
	}
}
