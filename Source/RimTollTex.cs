using UnityEngine;
using Verse;

namespace RimToll
{
	[StaticConstructorOnStartup]
	public static class RimTollTex
	{
		public static readonly Texture2D Collect =
			ContentFinder<Texture2D>.Get("UI/Commands/RimToll_Collect", reportFailure: false);

		public static readonly Texture2D Seal =
			ContentFinder<Texture2D>.Get("UI/RimToll_Seal", reportFailure: false);

		public static readonly Texture2D SealGreen =
			ContentFinder<Texture2D>.Get("UI/RimToll_SealGreen", reportFailure: false);

		public static readonly Texture2D Sheet =
			ContentFinder<Texture2D>.Get("UI/RimToll_Sheet", reportFailure: false);

		public static readonly Texture2D Photo =
			ContentFinder<Texture2D>.Get("UI/RimToll_Photo", reportFailure: false);

		public static readonly Texture2D StampMark =
			ContentFinder<Texture2D>.Get("UI/RimToll_StampMark", reportFailure: false);

		public static readonly Texture2D StampGreen =
			ContentFinder<Texture2D>.Get("UI/RimToll_StampGreen", reportFailure: false);

		public static readonly Texture2D ShowMarker =
			ContentFinder<Texture2D>.Get("UI/Commands/RimToll_ShowMarker", reportFailure: false);

		public static readonly Texture2D StampRed =
			ContentFinder<Texture2D>.Get("UI/RimToll_StampRed", reportFailure: false);
	}
}
