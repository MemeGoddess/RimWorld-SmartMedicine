using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace SmartMedicine.Compatibility.NiceHealthTab;

[HarmonyPatch]
public static class DiseaseMenu
{
	private static readonly MethodInfo IsOver =
		AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver));

	private static readonly MethodInfo CurrentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));
	private static readonly MethodInfo EventType = AccessTools.PropertyGetter(typeof(Event), nameof(Event.type));
	private static readonly MethodInfo Button = AccessTools.PropertyGetter(typeof(Event), nameof(Event.button));

	public static readonly MethodInfo
		HediffTendableNow = AccessTools.Method(typeof(Hediff), nameof(Hediff.TendableNow));

	private static readonly MethodInfo CreateMenu = AccessTools.Method(typeof(HediffRowPriorityCare),
		nameof(HediffRowPriorityCare.CreateCareMenu));

	[HarmonyPrepare]
	static bool Prepare()
	{
		return CompatibilityLoader.NiceHealthTab;
	}

	[HarmonyTargetMethods]
	static MethodBase[] TargetMethods()
	{
		var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
		return [AccessTools.Method(type, "DrawAffectWholeBodyRowIconDisease")];
	}

	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
		ILGenerator il)
	{
		var matcher = new CodeMatcher(instructions);

		matcher.MatchStartForward(
			new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawBoxSolid)))
		).ThrowIfInvalid("Unable to find Draw call");

		var drawCall = matcher.Pos;
		matcher.MatchStartBackwards(new CodeMatch(x =>
				x.IsLdloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Rect)))
			.ThrowIfInvalid("Unable to find rect");
		var rectLoad =
			new CodeInstruction(OpCodes.Ldloc, (matcher.Instruction.Clone().operand as LocalBuilder)!.LocalIndex);

		matcher.Advance(drawCall - matcher.Pos);

		var end = matcher.InstructionAt(1);
		var skip = il.DefineLabel();
		end.labels.Add(skip);

		matcher.InsertAfterAndAdvance(
			rectLoad,
			new CodeInstruction(OpCodes.Call, IsOver),
			new CodeInstruction(OpCodes.Brfalse_S, skip),
			new CodeInstruction(OpCodes.Call, CurrentEvent),
			new CodeInstruction(OpCodes.Call, Button),
			new CodeInstruction(OpCodes.Ldc_I4_1),
			new CodeInstruction(OpCodes.Ceq),
			new CodeInstruction(OpCodes.Brfalse_S, skip),
			new CodeInstruction(OpCodes.Call, CurrentEvent),
			new CodeInstruction(OpCodes.Callvirt, EventType),
			new CodeInstruction(OpCodes.Ldc_I4_0),
			new CodeInstruction(OpCodes.Ceq),
			new CodeInstruction(OpCodes.Brfalse_S, skip)
		).ThrowIfInvalid("Instructions invalid after checking Right Click on Nice Hediff row");

		matcher.InsertAfterAndAdvance(
			new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
			new CodeInstruction(OpCodes.Brfalse_S, skip),
			new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
			new CodeInstruction(OpCodes.Ldc_I4_1),
			new CodeInstruction(OpCodes.Callvirt, HediffTendableNow),
			new CodeInstruction(OpCodes.Brfalse_S, skip)
		).ThrowIfInvalid("Instructions invalid after checking Tendable on Nice Hediff row");

		matcher.InsertAfter(
			new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
			new CodeInstruction(OpCodes.Call, CreateMenu)
		).ThrowIfInvalid("Instructions invalid after setting up Specific Care Menu on Hediff row");

		var debug2 = string.Join("\n", instructions.Select(x => x.ToString()));
		return matcher.Instructions();
	}
}