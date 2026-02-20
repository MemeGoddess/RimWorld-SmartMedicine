using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace SmartMedicine.Compatibility.NiceHealthTab;

[HarmonyPatch]
public static class InjectDiseaseHediff
{
	private static readonly MethodInfo IsOver =
		AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver));

	private static readonly MethodInfo CurrentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));
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

	[HarmonyTargetMethod]
	static MethodBase TargetMethod()
	{
		var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
		return AccessTools.Method(type, "DrawDiseases");
	}

	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
		ILGenerator il)
	{
		var matcher = new CodeMatcher(instructions);

		matcher.MatchStartForward(new CodeMatch(x =>
				x.opcode == OpCodes.Callvirt && x.operand is MethodInfo { Name: "<DrawDiseases>g__DrawRow|0" }))
			.Repeat(m =>
			{
				m.MatchStartBackwards(new CodeMatch(x =>
					x.IsLdloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Hediff)));
				var hediffLoad = m.Instruction.Clone();

				m.InsertAndAdvance(
					hediffLoad,
					new CodeInstruction(OpCodes.Stsfld, InjectInjuryHediff.CurrentHediffField)
				).ThrowIfInvalid("Instructions invalid after inserting Hediff");

				m.MatchStartForward(new CodeMatch(x =>
					x.opcode == OpCodes.Callvirt && x.operand is MethodInfo { Name: "<DrawDiseases>g__DrawRow|0" }));

				m.InsertAfterAndAdvance(
					new CodeInstruction(OpCodes.Ldnull),
					new CodeInstruction(OpCodes.Stsfld, InjectInjuryHediff.CurrentHediffField)
				);
			});

		return matcher.Instructions();
	}
}