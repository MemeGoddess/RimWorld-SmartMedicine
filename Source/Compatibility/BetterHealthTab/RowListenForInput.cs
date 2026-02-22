using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace SmartMedicine.Compatibility.BetterHealthTab
{
  [HarmonyPatch]
  public class RowListenForInput
  {

    private static MethodInfo LIS =
      AccessTools.PropertySetter(AccessTools.TypeByName("BetterHealthTab.HealthTab.Hediffs.Row"), "ListensForInput");

    [HarmonyPrepare]
    static bool Prepare() => 
	    CompatibilityLoader.BetterHealthTab;

    [HarmonyTargetMethod]
    public static MethodBase TargetMethod()
    {
      Type rowType = AccessTools.TypeByName("BetterHealthTab.HealthTab.Hediffs.Row");
      return AccessTools.Constructor(rowType, new Type[] { typeof(IEnumerable<Verse.Hediff>) });
    }

    [HarmonyPostfix]
    public static void Postfix(object __instance, List<Hediff> hediffs)
    {
      LIS.Invoke(__instance, new object[] { true });
    }
  }
}
