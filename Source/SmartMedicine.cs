using System;
using System.Reflection;
using System.Linq;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using SmartMedicine.Compatibility;
using SmartMedicine.Compatibility.CombatExtended;

namespace SmartMedicine
{
	public class Mod : Verse.Mod
	{
		public static Settings settings;
		internal static Harmony harmony;
		public Mod(ModContentPack content) : base(content)
		{
			// initialize settings
			settings = GetSettings<Settings>();
#if DEBUG
			Harmony.DEBUG = true;
#endif

			harmony = new Harmony("uuugggg.rimworld.SmartMedicine.main");

			harmony.PatchAllUncategorized();

			
			LongEventHandler.ExecuteWhenFinished(CompatibilityLoader.Setup);
			
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
			settings.DoWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return "TD.SmartMedicine".Translate();
		}
	}
}