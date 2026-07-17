using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace SmartMedicine.Compatibility.CombatExtended;

[HarmonyPatch]
[HarmonyPatchCategory("CombatExtended")]
public class CE_DirtyBleedCache
{
	[HarmonyPrepare]
	static bool Prepare()
	{
		return CompatibilityLoader.CombatExtended;
	}

	[HarmonyTargetMethod]
	static MethodBase TargetMethod()
	{
		var type = AccessTools.TypeByName("CombatExtended.HediffComp_Stabilize");
		if (type == null)
			return null;

		var method = AccessTools.Method(type, "Stabilize");
		return method;
	}

	[HarmonyPostfix]
	static void Stabilize(HediffComp __instance)
	{
		__instance.Pawn.health.hediffSet.DirtyCache();
	}
}