using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace SmartMedicine.Compatibility.NiceHealthTab;

[HarmonyPatch]
public static class DiseaseIcons
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
		foreach (var nestedType in type.GetNestedTypes(BindingFlags.NonPublic))
		{
			var method = nestedType
				.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
				.FirstOrDefault(m =>
					m.Name.Contains("DrawDiseases") &&
					m.Name.Contains("b__") &&
					m.GetParameters().Length == 1 &&
					m.GetParameters()[0].ParameterType == typeof(Rect));

			if (method != null)
				return method;
		}

		Verse.Log.Error("Could not find DrawDiseases Rect lambda");
		return null;
	}

	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
		ILGenerator il)
	{
		var nullableFloatCtor = AccessTools.Constructor(
			typeof(Rect?),
			new[] { typeof(Rect) }
		);

		var miGetValueOrDefault = AccessTools.Method(
			typeof(Rect?),
			nameof(Nullable<Rect>.GetValueOrDefault),
			Type.EmptyTypes
		);
		var matcher = new CodeMatcher(instructions);

		var rectLocal = il.DeclareLocal(typeof(Rect));

		matcher.End();
		var exit = matcher.Instruction.labels.ToList();
		matcher.Instruction.labels.Clear();

		matcher.Start();
		matcher.MatchStartForward(new CodeMatch(x =>
				x.IsStloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Rect)))
			.ThrowIfInvalid("Unable to find Rect to draw icons on infections");

		var rectLoad = new CodeInstruction(OpCodes.Ldloc, (matcher.Instruction.operand as LocalBuilder)!.LocalIndex);

		matcher.End();
		matcher.MatchStartBackwards(new CodeMatch(x =>
				x.opcode == OpCodes.Newobj && x.operand is ConstructorInfo ci && ci.DeclaringType == typeof(Rect)))
			.ThrowIfInvalid("Unable to find new Rect being made");

		matcher.InsertAfter(
				new CodeInstruction(OpCodes.Newobj, nullableFloatCtor),
				new CodeInstruction(OpCodes.Stloc_S, rectLocal),
				new CodeInstruction(OpCodes.Ldloca_S, rectLocal),
				new CodeInstruction(OpCodes.Call, miGetValueOrDefault)
			)
			.ThrowIfInvalid("Instructions invalid after capturing rect in DrawRow");

		matcher.End();

		var skipAll = il.DefineLabel();
		matcher.Instruction.labels.Add(skipAll);

		matcher.Insert(
			new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField) { labels = exit },
			new CodeInstruction(OpCodes.Ldc_I4_1),
			new CodeInstruction(OpCodes.Callvirt, HediffTendableNow),
			new CodeInstruction(OpCodes.Brfalse_S, skipAll),

			new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
			new CodeInstruction(OpCodes.Ldarg_1),
			new CodeInstruction(OpCodes.Ldloc, rectLocal),
			new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DiseaseIcons), nameof(DrawExtraIcons)))
		);

		return matcher.Instructions();
	}

	private static void DrawExtraIcons(Hediff hediff, Rect first, Rect? second)
	{
		if (second != null)
		{
			InjuryIcons.DrawExtraIcons(hediff, second.Value);
			return;
		}

		first = first.ContractedBy(4f);
		var rect = new Rect(first.xMax, first.center.y - first.height / 2, first.height * 2f, first.height);
		InjuryIcons.DrawExtraIcons(hediff, rect);
	}
}