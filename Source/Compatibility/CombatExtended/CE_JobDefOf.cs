using RimWorld;
using Verse;

namespace SmartMedicine.Compatibility.CombatExtended;

[DefOf]
public static class CE_JobDefOf
{
	[MayRequire("CETeam.CombatExtended")]
	public static JobDef Stabilize;
}