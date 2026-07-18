using HarmonyLib;
using Verse;

namespace SmartMedicine.Compatibility.CombatExtended;

public static class CE_StabilizeUtility
{
	private static bool initialized;

	private delegate bool canBeStabilizedDelegate(Hediff hediff);
	private static canBeStabilizedDelegate canBeStabilized = _ => false;
	public static bool CanBeStabilized(this Hediff hediff) => 
		canBeStabilized(hediff);

	public static void Setup()
	{
		if (initialized)
			return;
		initialized = true;

		if (!CompatibilityLoader.CombatExtended)
			return;

		var utilityType = AccessTools.TypeByName("CombatExtended.CE_Utility");
		if (utilityType == null)
			return;
		
		var canBeStabilizedMethod = utilityType.GetMethod("CanBeStabilized", AccessTools.all);
		if (canBeStabilizedMethod == null)
			return;
		
		canBeStabilized = AccessTools.MethodDelegate<canBeStabilizedDelegate>(canBeStabilizedMethod);
	}
}