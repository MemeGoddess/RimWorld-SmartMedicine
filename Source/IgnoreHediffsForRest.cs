using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SmartMedicine
{
	[HarmonyPatch]
	public static class IgnoreHediffsForRest
	{
		static readonly MethodInfo GetList = AccessTools.Method(typeof(PriorityCareSettingsComp), nameof(PriorityCareSettingsComp.GetIgnore));
		static readonly MethodInfo Contains = AccessTools.Method(typeof(HashSet<Hediff>), nameof(HashSet<Hediff>.Contains));

		private static readonly FieldInfo GetHediffList = AccessTools.Field(typeof(HediffSet), nameof(HediffSet.hediffs));
		private static readonly MethodInfo GetItem = AccessTools.PropertyGetter(typeof(List<Hediff>), "get_Item");
		private static readonly MethodInfo FullyImmune =
			AccessTools.Method(typeof(HediffUtility), nameof(HediffUtility.FullyImmune));

		private static readonly FieldInfo SkipStatusField =
			AccessTools.Field(typeof(IgnoreHediffsForRest), nameof(SkipStatus));

		private static PatchStatus SkipStatus = PatchStatus.None;

		[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.HasTendedAndHealingInjury))]
		[HarmonyPrefix]
		public static void EnableSkip() => SkipStatus = SkipStatus == PatchStatus.HealthTracker 
			? PatchStatus.HealthTracker 
			: PatchStatus.Job;

		[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.HasTendedAndHealingInjury))]
		[HarmonyPostfix]
		public static void DisableSkip() => SkipStatus = SkipStatus == PatchStatus.HealthTracker 
			? PatchStatus.HealthTracker 
			: PatchStatus.None;

		[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTickInterval))]
		[HarmonyPrefix]
		public static void PreventSkip() => SkipStatus = PatchStatus.HealthTracker;

		[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.HealthTickInterval))]
		[HarmonyPostfix]
		public static void AllowSkip() => SkipStatus = PatchStatus.None;


		[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.HasTendedAndHealingInjury))]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> DamageHediffs(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			var list = il.DeclareLocal(typeof(HashSet<Hediff>));

			var matcher = new CodeMatcher(instructions);

			matcher.MatchEndForward(
				new CodeMatch(OpCodes.Ldc_I4_0),
				new CodeMatch(x => x.IsStloc()),
				new CodeMatch(OpCodes.Br_S)
			);

			if (matcher.IsInvalid)
				throw new Exception($"Unable to find entrypoint for {nameof(DamageHediffs)}");

			matcher.Insert(
				new CodeInstruction(OpCodes.Call, GetList),
				new CodeInstruction(OpCodes.Stloc, list)
			);

			matcher.MatchEndForward(
				new CodeMatch(OpCodes.Callvirt),
				new CodeMatch(OpCodes.Isinst),
				new CodeMatch(x => x.IsStloc()),
				new CodeMatch(x => x.IsLdloc()),
				new CodeMatch(OpCodes.Brfalse_S),
				new CodeMatch(x => x.IsLdloc())
			);

			if (matcher.IsInvalid)
				throw new Exception("Unable to find Hediff cast and load");

			var hediffLoad = matcher.Instruction.Clone();
			hediffLoad.labels.Clear();

			var skip = (Label)matcher.InstructionAt(-1).Clone().operand;
			var disable = il.DefineLabel();
			matcher.Instruction.labels.Add(disable);

			matcher.Insert(
				new CodeInstruction(OpCodes.Ldsfld, SkipStatusField),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Ceq),
				new CodeInstruction(OpCodes.Brfalse_S, disable),
				new CodeInstruction(OpCodes.Ldloc, list),
				hediffLoad,
				new CodeInstruction(OpCodes.Callvirt, Contains),
				new CodeInstruction(OpCodes.Brtrue_S, skip)
				);


			return matcher.Instructions();
		}

		[HarmonyPatch(typeof(HediffSet), nameof(HediffSet.HasImmunizableNotImmuneHediff))]
		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> InfectionHediffs(IEnumerable<CodeInstruction> instructions,
			ILGenerator il)
		{
			var list = il.DeclareLocal(typeof(HashSet<Hediff>));

			var matcher = new CodeMatcher(instructions);

			matcher.MatchEndForward(
				new CodeMatch(OpCodes.Ldc_I4_0),
				new CodeMatch(x => x.IsStloc()),
				new CodeMatch(OpCodes.Br_S)
			);

			if (matcher.IsInvalid)
				throw new Exception($"Unable to find entrypoint for {nameof(InfectionHediffs)}");

			matcher.Insert(
				new CodeInstruction(OpCodes.Call, GetList),
				new CodeInstruction(OpCodes.Stloc, list)
			);

			matcher.MatchStartForward(
				new CodeMatch(OpCodes.Ldarg_0),
				new CodeMatch(OpCodes.Ldfld, GetHediffList),
				new CodeMatch(OpCodes.Ldloc_0),
				new CodeMatch(OpCodes.Callvirt, GetItem)
			);

			if (matcher.IsInvalid)
				throw new Exception("Unable to find instructions to load Hediff list");

			var loadHediffList = matcher.InstructionsInRange(matcher.Pos, matcher.Pos + 3);
			loadHediffList.ForEach(x => x.labels.Clear());

			OpCode[] validCodes = [OpCodes.Brtrue_S, OpCodes.Brfalse, OpCodes.Brfalse_S, OpCodes.Brtrue];
			matcher.MatchStartForward(new CodeMatch(x => validCodes.Contains(x.opcode)));
			if (matcher.IsInvalid)
				throw new Exception("Unable to find a skip label to use for check");

			var skip = (Label)matcher.Instruction.Clone().operand;

			var checkFullyImmune = new List<CodeMatch>(loadHediffList.Select(x => new CodeMatch(x.opcode, x.operand)))
				{ new(OpCodes.Call, FullyImmune) };

			matcher.MatchStartForward(checkFullyImmune.ToArray());

			if (matcher.IsInvalid)
				throw new Exception("Unable to find FullyImmune call");

			matcher.InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc, list));
			matcher.InsertAndAdvance(loadHediffList);
			matcher.Insert(new CodeInstruction(OpCodes.Callvirt, Contains),
				new CodeInstruction(OpCodes.Brtrue_S, skip));

			return matcher.Instructions();
		}
	}

	public enum PatchStatus
	{
		None = 0,
		Job = 1,
		HealthTracker = 2
	}
}
