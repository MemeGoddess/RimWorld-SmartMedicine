using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace SmartMedicine.Compatibility.NiceHealthTab.Optimizations
{
	[HarmonyPatch]
	public static class DollDraw_GetPartConditionLabel
	{
		private static readonly MethodInfo Translate = AccessTools.Method(typeof(Translator), nameof(Translator.Translate),
			[typeof(string)]);
		private static readonly MethodInfo OpImplicit = AccessTools.Method(
			typeof(TaggedString),
			"op_Implicit",
			[typeof(TaggedString)]);

		private static readonly Type CacheType = typeof(Dictionary<string, string>);

		private static readonly FieldInfo Cache =
			AccessTools.Field(typeof(DollDraw_GetPartConditionLabel), nameof(CachedLabels));
		private static readonly MethodInfo TryGet = AccessTools.Method(CacheType,
			nameof(Dictionary<string, string>.TryGetValue));
		private static readonly MethodInfo Add = AccessTools.Method(CacheType, nameof(Dictionary<string, string>.Add));

		private static readonly FieldInfo FieldSettings = AccessTools.Field(typeof(Mod), nameof(Mod.settings));

		private static readonly FieldInfo OptimizeSetting =
			AccessTools.Field(typeof(Settings), nameof(Settings.niceHealthOptimize));

		private static Dictionary<string, string> CachedLabels = new();
	  [HarmonyPrepare]
	  static bool Prepare()
	  {
		  return CompatibilityLoader.NiceHealthTab;
	  }

	  [HarmonyTargetMethod]
	  static MethodBase TargetMethod()
	  {
		  var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
		  return AccessTools.Method(type, "_GetPartConditionLabel");
	  }

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
			ILGenerator il)
		{
			try
			{
				var matcher = new CodeMatcher(instructions);

				var shouldOptimize = il.DeclareLocal(typeof(bool));
				matcher.Start();
				matcher.Insert(
					new CodeInstruction(OpCodes.Ldsfld, FieldSettings),
					new CodeInstruction(OpCodes.Ldfld, OptimizeSetting),
					new CodeInstruction(OpCodes.Stloc, shouldOptimize)
				);

				matcher.MatchStartForward(
					new CodeMatch(x => x.opcode == OpCodes.Ldstr && x.operand is string),
					new CodeMatch(OpCodes.Call, Translate),
					new CodeMatch(OpCodes.Call, OpImplicit)
				).Repeat(m =>
				{
					var str = m.Instruction.operand as string;

					var cacheHitSkip = il.DefineLabel();

					var disableCache = il.DefineLabel();
					var enableCache = il.DefineLabel();

					var localCache = il.DeclareLocal(typeof(string));

					// Check if cache is disabled, and jump to end
					m.InsertAndAdvance(
						new CodeInstruction(OpCodes.Ldloc, shouldOptimize),
						new CodeInstruction(OpCodes.Brtrue_S, enableCache),
						new CodeInstruction(OpCodes.Ldstr, str),
						new CodeInstruction(OpCodes.Call, Translate),
						new CodeInstruction(OpCodes.Call, OpImplicit),
						new CodeInstruction(OpCodes.Br_S, disableCache)
					);

					// Check if value is cached
					m.InsertAndAdvance(
						new CodeInstruction(OpCodes.Ldsfld, Cache) { labels = [enableCache] },
						new CodeInstruction(OpCodes.Ldstr, str),
						new CodeInstruction(OpCodes.Ldloca, localCache),
						new CodeInstruction(OpCodes.Callvirt, TryGet),
						new CodeInstruction(OpCodes.Brtrue_S, cacheHitSkip)
					);

					// Cache value if not
					m.Advance(3);
					m.InsertAndAdvance(
						new CodeInstruction(OpCodes.Stloc, localCache),
						new CodeInstruction(OpCodes.Ldsfld, Cache),
						new CodeInstruction(OpCodes.Ldstr, str),
						new CodeInstruction(OpCodes.Ldloc, localCache),
						new CodeInstruction(OpCodes.Call, Add),
						new CodeInstruction(OpCodes.Ldloc, localCache) { labels = [cacheHitSkip] }
					);

					// End
					m.Labels.Add(disableCache);
					m.Advance();

				});

				return matcher.Instructions();
			}
			catch (Exception ex)
			{
				Verse.Log.Warning($"[Smart Medicine] Unable to perform low level optimization on Nice Health Tab's `GetPartConditionLabel`\n{ex.Message}\n{ex.StackTrace}");
			}

			return instructions;
		}
	}
}
