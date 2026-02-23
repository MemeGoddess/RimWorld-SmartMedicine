using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace SmartMedicine.Compatibility.NiceHealthTab;

[HarmonyPatch]
public static class InjectInjuryHediff
{
	private static readonly MethodInfo MakeMedOperationsForFloatMenuPart =
		AccessTools.Method("NiceHealthTab.DollDrawer:MakeMedOperationsFloatMenuForPart");
	private static readonly MethodInfo DrawAffectHediffGroupedRow =
		AccessTools.Method("NiceHealthTab.DollDrawer:DrawAffectHediffGroupedRow");

	private static readonly MethodInfo DrawDiseases = AccessTools.Method("NiceHealthTab.DollDrawer:DrawDiseases");

	public static readonly FieldInfo CurrentHediffField =
		AccessTools.Field(typeof(InjectInjuryHediff), nameof(CurrentHediff));

	public static Hediff CurrentHediff;

	[HarmonyPrepare]
	static bool Prepare()
	{
		var modEnabled = ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);

		if (modEnabled)
			Verse.Log.Message("[Smart Medicine] Apply Nice Health Tab patch");

		return modEnabled;
	}

	[HarmonyTargetMethod]
	static MethodBase TargetMethod()
	{
		var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
		return AccessTools.Method(type, "ListAllHediffs");
	}

	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> ListAllHediffs(IEnumerable<CodeInstruction> instructions)
	{
		var matcher = new CodeMatcher(instructions);

		#region Surgery
		matcher.End();

		matcher.MatchStartBackwards(new CodeMatch(OpCodes.Call, MakeMedOperationsForFloatMenuPart))
			.ThrowIfInvalid("Unable to find Float Menu creation in Nice Health Tab");
		var createMenu = matcher.Pos;

		matcher.MatchStartBackwards(new CodeMatch(
			x => x.IsLdloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Hediff)));

		var hediffLoad = matcher.Instruction.Clone();

		matcher.Advance(createMenu - matcher.Pos);

		matcher.InsertAndAdvance(
			hediffLoad,
			new CodeInstruction(OpCodes.Stsfld, CurrentHediffField)
		).ThrowIfInvalid("Instructions invalid after inserting Hediff");

		matcher.InsertAfter(
			new CodeInstruction(OpCodes.Ldnull),
			new CodeInstruction(OpCodes.Stsfld, CurrentHediffField)
		);

		#endregion

		#region Draw Hediffs

		matcher.End();

		matcher.MatchStartBackwards(new CodeMatch(OpCodes.Call, DrawAffectHediffGroupedRow))
			.ThrowIfInvalid("Unable to find Float Menu creation in Nice Health Tab");
		createMenu = matcher.Pos;

		matcher.MatchStartBackwards(new CodeMatch(
			x => x.IsLdloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Hediff)));

		hediffLoad = matcher.Instruction.Clone();

		matcher.Advance(createMenu - matcher.Pos);

		matcher.InsertAndAdvance(
			hediffLoad,
			new CodeInstruction(OpCodes.Stsfld, CurrentHediffField)
		).ThrowIfInvalid("Instructions invalid after inserting Hediff");

		matcher.InsertAfter(
			new CodeInstruction(OpCodes.Ldnull),
			new CodeInstruction(OpCodes.Stsfld, CurrentHediffField)
		);

		#endregion

		#region DrawDiseases
		matcher.Start();
		matcher.MatchStartForward(new CodeMatch(OpCodes.Call, DrawDiseases))
			.ThrowIfInvalid("Unable to find DrawDiseases to setup whole body hediffs");

		matcher.MatchStartForward(
				new CodeMatch(x => x.IsLdloc()),
				new CodeMatch(x => x.opcode == OpCodes.Ldfld && x.operand is FieldInfo fi && fi.FieldType == typeof(Hediff)),
				new CodeMatch(OpCodes.Callvirt),
				new CodeMatch(OpCodes.Ldloc_S),
				new CodeMatch(OpCodes.Ldftn),
				new CodeMatch(OpCodes.Newobj),
				new CodeMatch(OpCodes.Ldnull),
				new CodeMatch(OpCodes.Call,
					DrawDiseases.DeclaringType != null
						? AccessTools.Method(DrawDiseases.DeclaringType, "DrawAffectWholeBodyRowIcon")
						: null))
			.ThrowIfInvalid("Unable to find DrawAffectWholeBodyRowIcon call for whole body hediffs")
			.Repeat(m =>
			{
				var display = m.Instruction.Clone();
				m.Advance();
				var hediff = m.Instruction.Clone();
				m.InsertAfterAndAdvance(
					new CodeInstruction(OpCodes.Stsfld, CurrentHediffField),
					display,
					hediff
				);
				m.Advance(6);
				m.InsertAfter(
					new CodeInstruction(OpCodes.Ldnull),
					new CodeInstruction(OpCodes.Stsfld, CurrentHediffField)
				);
			});

		#endregion

		return matcher.Instructions();
	}
}