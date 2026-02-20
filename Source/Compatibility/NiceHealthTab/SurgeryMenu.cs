using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;
using Verse;

namespace SmartMedicine.Compatibility.NiceHealthTab
{
  [HarmonyPatch]
  public static class SurgeryMenu
  {
	private static readonly ConstructorInfo FloatMenuListCtor = AccessTools.Constructor(typeof(List<FloatMenuOption>));

	private static readonly MethodInfo AddRange =
		AccessTools.Method(typeof(List<FloatMenuOption>), nameof(List<FloatMenuOption>.AddRange));

	private static readonly MethodInfo CreateMenuOptions = AccessTools.Method(typeof(HediffRowPriorityCare),
		nameof(HediffRowPriorityCare.CreateCareMenuOptions));

	[HarmonyPrepare]
	static bool Prepare()
	{
	  return CompatibilityLoader.NiceHealthTab;
	}

	[HarmonyTargetMethod]
	static MethodBase TargetMethod()
	{
	  var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
	  return AccessTools.Method(type, "MakeMedOperationsFloatMenuForPart");
	}

	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
		ILGenerator il)
	{
	  var matcher = new CodeMatcher(instructions);

	  matcher.MatchStartForward(new CodeMatch(OpCodes.Newobj, FloatMenuListCtor))
		  .ThrowIfInvalid("Unable to find FloatMenuOption list constructor in Nice Health Tab");
	  matcher.Advance();
	  var listStore = matcher.Instruction.Clone();
	  var listLoad = new CodeInstruction(OpCodes.Ldloc, listStore.LocalIndex());

	  var nextOp = matcher.InstructionAt(1);
	  var skip = il.DefineLabel();
	  nextOp.labels.Add(skip);

	  matcher.InsertAfter(
		  // Null
		  new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
		  new CodeInstruction(OpCodes.Brfalse_S, skip),
		  // Tendable
		  new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
		  new CodeInstruction(OpCodes.Ldc_I4_1),
		  new CodeInstruction(OpCodes.Callvirt, HediffRowPriorityCare.HediffTendableNow),
		  new CodeInstruction(OpCodes.Brfalse_S, skip),
		  // Add Options
		  listLoad,
		  new CodeInstruction(OpCodes.Ldsfld, InjectInjuryHediff.CurrentHediffField),
		  new CodeInstruction(OpCodes.Call, CreateMenuOptions),
		  new CodeInstruction(OpCodes.Callvirt, AddRange)
	  );

	  return matcher.Instructions();
	}
  }
}