using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartMedicine.Compatibility.NiceHealthTab;

[HarmonyPatch]
public static class InjuryIcons
{
	private static readonly MethodInfo IsOver =
		AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver));

	private static readonly MethodInfo CurrentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));
	private static readonly MethodInfo Button = AccessTools.PropertyGetter(typeof(Event), nameof(Event.button));

	public static readonly MethodInfo
		HediffTendableNow = AccessTools.Method(typeof(Hediff), nameof(Hediff.TendableNow));

	private static readonly MethodInfo CreateMenu = AccessTools.Method(typeof(HediffRowPriorityCare),
		nameof(HediffRowPriorityCare.CreateCareMenu));

	private static readonly MethodInfo xMax = AccessTools.PropertyGetter(typeof(Rect),
		nameof(Rect.xMax));
	private static readonly MethodInfo height = AccessTools.PropertyGetter(typeof(Rect),
		nameof(Rect.height));

	private static readonly MethodInfo Draw = AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawTextureFitted),
		new[] { typeof(Rect), typeof(Texture), typeof(float), typeof(float) });
	[HarmonyPrepare]
	static bool Prepare()
	{
		return CompatibilityLoader.NiceHealthTab;
	}

	[HarmonyTargetMethod]
	static MethodBase TargetMethod()
	{
		var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
		return AccessTools.Method(type, "DrawAffectHediffGroupedRow");
	}

	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
		ILGenerator il)
	{
		var matcher = new CodeMatcher(instructions);

		var rect = il.DeclareLocal(typeof(Rect));

		var nullableFloatCtor = AccessTools.Constructor(
			typeof(Rect?),
			new[] { typeof(Rect) }
		);

		var miGetValueOrDefault = AccessTools.Method(
			typeof(Rect?),
			nameof(Nullable<Rect>.GetValueOrDefault),
			Type.EmptyTypes
		);
		matcher.MatchStartForward(
			new CodeMatch(OpCodes.Call, Draw)
		).Repeat(m =>
		{
			m.MatchStartBackwards(new CodeMatch(x => x.opcode == OpCodes.Newobj && x.operand is ConstructorInfo ci && ci.DeclaringType == typeof(Rect)));
			m.InsertAfterAndAdvance(
				new CodeInstruction(OpCodes.Newobj, nullableFloatCtor),
				new CodeInstruction(OpCodes.Stloc_S, rect),
				new CodeInstruction(OpCodes.Ldloca_S, rect),
				new CodeInstruction(OpCodes.Call, miGetValueOrDefault)
			);
			m.MatchStartForward(
				new CodeMatch(OpCodes.Call, Draw));
			m.Advance();
		});

		matcher.End();
		matcher.MatchStartBackwards(new CodeMatch(OpCodes.Call, IsOver))
			.ThrowIfInvalid("Unable to find MouseOver call to inject extra icons");


		var hasValue = AccessTools.PropertyGetter(typeof(Rect?), nameof(Nullable<Rect>.HasValue));
		var skipCtor = il.DefineLabel();
		var skipAll = il.DefineLabel();

		matcher.Instruction.labels.Add(skipAll);

		matcher.Insert(
			new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
			new CodeInstruction(OpCodes.Ldc_I4_1),
			new CodeInstruction(OpCodes.Callvirt, HediffTendableNow),
			new CodeInstruction(OpCodes.Brfalse_S, skipAll),

			new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),

			new CodeInstruction(OpCodes.Ldloca_S, rect),
			new CodeInstruction(OpCodes.Call, hasValue),
			new CodeInstruction(OpCodes.Brtrue, skipCtor),

			new CodeInstruction(OpCodes.Ldarg_3),
			new CodeInstruction(OpCodes.Ldarg_2),
			new CodeInstruction(OpCodes.Ldind_R4),
			new CodeInstruction(OpCodes.Ldarg_3),
			new CodeInstruction(OpCodes.Ldc_R4, 16f),
			new CodeInstruction(OpCodes.Sub),
			new CodeInstruction(OpCodes.Ldarg_1),
			new CodeInstruction(OpCodes.Ldarg_3),
			new CodeInstruction(OpCodes.Ldc_R4, 16f),
			new CodeInstruction(OpCodes.Sub),
			new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Text), nameof(Text.CalcHeight))),
			new CodeInstruction(OpCodes.Newobj,
				AccessTools.Constructor(typeof(Rect), new[] { typeof(float), typeof(float), typeof(float), typeof(float) })),
			new CodeInstruction(OpCodes.Newobj, nullableFloatCtor),
			new CodeInstruction(OpCodes.Stloc_S, rect),

			new CodeInstruction(OpCodes.Ldloca_S, rect) { labels = [skipCtor] },
			new CodeInstruction(OpCodes.Call, miGetValueOrDefault),
			new CodeInstruction(OpCodes.Call,
				AccessTools.Method(typeof(InjuryIcons),
					nameof(DrawExtraIcons)))
		);


		return matcher.Instructions();
	}
	public static void DrawExtraIcons(Hediff hediff, Rect rect)
	{
		if (hediff == null)
			return;
		var save = GUI.color;
		GUI.color = Color.white;
		var comp = PriorityCareSettingsComp.GetComp();
		if (comp.hediffCare.TryGetValue(hediff, out MedicalCareCategory heCare))
		{
			rect.x -= 20;
			HediffRowPriorityCare.loadedCareTextures ??= HediffRowPriorityCare.careTextures();
			Texture2D tex = HediffRowPriorityCare.loadedCareTextures[(int)heCare];
			GUI.DrawTexture(new Rect(rect.x, rect.y, 20f, 20f), tex);
		}

		GUI.color = save;

		if (comp.ignoredHediffs.Contains(hediff))
		{
			rect.x -= 20;
			save = GUI.color;
			GUI.color = new Color(1, 1, 1, 0.5f);
			GUI.DrawTexture(new Rect(rect.x, rect.y, 20f, 20f), Widgets.CheckboxOffTex);
			GUI.color = save;

		}
	}
}