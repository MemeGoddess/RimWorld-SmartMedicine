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
		internal static ModContentPack contentPack;
		public Mod(ModContentPack content) : base(content)
		{
			// initialize settings
			settings = GetSettings<Settings>();

			harmony = new Harmony("uuugggg.rimworld.SmartMedicine.main");

			harmony.PatchAllUncategorized(Assembly.GetExecutingAssembly());
			
			LongEventHandler.ExecuteWhenFinished(CompatibilityLoader.Setup);
			contentPack = content;
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