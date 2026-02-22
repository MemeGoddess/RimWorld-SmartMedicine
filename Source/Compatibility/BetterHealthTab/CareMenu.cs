using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace SmartMedicine.Compatibility.BetterHealthTab
{
	[HarmonyPatch]
	public static class CareMenu
  {
	  private static readonly Type type = AccessTools.TypeByName("BetterHealthTab.HealthTab.Hediffs.Row");

	  private static readonly FieldInfo Hediffs = AccessTools.Field(type, "_hediffs");
	  private static readonly MethodInfo Focused = AccessTools.PropertyGetter(AccessTools.TypeByName("CLIK.Components.UIComponent"), "Focused");

		[HarmonyPrepare]
	  static bool Prepare()
	  {
		  return CompatibilityLoader.BetterHealthTab;
	  }

	  [HarmonyTargetMethod]
	  static MethodBase TargetMethod()
	  {
		  var test = AccessTools.Method(type, "InputNow");
		  return test;
	  }


	  [HarmonyPrefix]
	  static void InputNow(object __instance, object painter)
	  {
		  if (Event.current.type != EventType.MouseUp)
			  return;

		  var isFocused = Focused.Invoke(__instance, []);

		  if (isFocused is not true)
			  return;

		  if (Event.current.button != 1)
			  return;

		  if (Hediffs.GetValue(__instance) is not List<Hediff> hediffs)
			  return;

		  if (hediffs.All(h => !h.TendableNow(true)))
			  return;

		  Verse.Log.Warning(Event.current.ToString());
			Event.current.Use();
			var list = HediffRowPriorityCare.CreateCareMenuOptionsWithList(hediffs);
			Find.WindowStack.Add(new FloatMenu(list));
		}
	}
}
