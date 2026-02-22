using System;
using System.Collections.Generic;
using System.Diagnostics;
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
	public static class CareIcons
	{
		private static readonly Type type = AccessTools.TypeByName("BetterHealthTab.HealthTab.Hediffs.Row");

		private static readonly Type iconStackType = AccessTools.TypeByName("BetterHealthTab.HealthTab.Hediffs.IconStack");
		private static readonly ConstructorInfo iconStackCtor = AccessTools.Constructor(iconStackType);

		private static readonly MethodInfo setFlow = AccessTools.PropertySetter(iconStackType, "Flow");
		private static readonly MethodInfo setSpacing = AccessTools.PropertySetter(iconStackType, "Spacing");
		private static readonly MethodInfo fill = AccessTools.Method(iconStackType, "Fill");

		private static readonly FieldInfo _icons = AccessTools.Field(type, "_icons");

		private static readonly Type uiComponentType = AccessTools.TypeByName("CLIK.Components.UIComponent");
		private static readonly Type listType = typeof(List<>).MakeGenericType(uiComponentType);
		private static readonly ConstructorInfo listCtor = AccessTools.Constructor(listType);
		private static readonly MethodInfo listAdd = AccessTools.Method(listType, "Add");
		private static readonly Type iconType = AccessTools.TypeByName("CLIK.Components.Icon");
		private static readonly ConstructorInfo iconCtor = AccessTools.Constructor(iconType);
		private static readonly MethodInfo setColor = AccessTools.PropertySetter(iconType, "Color");
		private static readonly MethodInfo setTexture = AccessTools.PropertySetter(iconType, "Texture");
		private static readonly FieldInfo Hediffs = AccessTools.Field(type, "_hediffs");
		[HarmonyPrepare]
		static bool Prepare()
		{
			return CompatibilityLoader.BetterHealthTab;
		}

		[HarmonyTargetMethod]
		static MethodBase TargetMethod()
		{
			return AccessTools.Method(type, "RecacheIcons");
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> DoCareIcons(IEnumerable<CodeInstruction> instructions,
			ILGenerator il)
		{
			var matcher = new CodeMatcher(instructions.ToList());

			matcher.Start();
			matcher.MatchEndForward(
				new CodeMatch(OpCodes.Newobj, listCtor),
				new CodeMatch(i => i.IsStloc())
			).ThrowIfInvalid("Unable to find data list being made");

			var listLocalInstruction = matcher.Instruction;
			var listLoadInstruction = new CodeInstruction(
				listLocalInstruction.opcode == OpCodes.Stloc_0 ? OpCodes.Ldloc_0 :
				listLocalInstruction.opcode == OpCodes.Stloc_1 ? OpCodes.Ldloc_1 :
				listLocalInstruction.opcode == OpCodes.Stloc_2 ? OpCodes.Ldloc_2 :
				listLocalInstruction.opcode == OpCodes.Stloc_3 ? OpCodes.Ldloc_3 :
				OpCodes.Ldloc_S, listLocalInstruction.operand
			);

			matcher.End();
			matcher.MatchStartBackwards(
				new CodeMatch(OpCodes.Ldarg_0),
				new CodeMatch(OpCodes.Ldfld, _icons)
			).ThrowIfInvalid("Unable to find entrypoint for icons");

			matcher.InsertAndAdvance(new List<CodeInstruction>
			{
				listLoadInstruction,
				new(OpCodes.Ldarg_0),
				new(OpCodes.Ldfld, Hediffs),
				new(OpCodes.Call, AccessTools.Method(typeof(CareIcons), nameof(GetIcons))),

				new(OpCodes.Callvirt, listAdd),
			}).ThrowIfInvalid("Failed to inject new icon list");

			return matcher.Instructions();
		}

		private static object GetIcons(List<Hediff> hediffs)
		{
			if(hediffs.Any(x => x.TryGetComp<HediffComp_Immunizable> () == null))
				Debugger.Break();


			HediffRowPriorityCare.loadedCareTextures ??= HediffRowPriorityCare.careTextures();
			var iconStack = iconStackCtor.Invoke(null);
			var flowsType = AccessTools.Inner(iconStackType, "Flows");
			setFlow.Invoke(iconStack, [Enum.ToObject(flowsType, 3)]);
			setSpacing.Invoke(iconStack, [4.0]);

			var comp = PriorityCareSettingsComp.GetComp();

			var icons = listCtor.Invoke(null);
			var hediffCares = hediffs.Where(comp.hediffCare.ContainsKey).ToList();
			if (hediffCares.Any())
			{
				var maxCare = hediffCares.Max(h => comp.hediffCare[h]);
				var icon = iconCtor.Invoke(null);
				setTexture.Invoke(icon, [HediffRowPriorityCare.loadedCareTextures[(int)maxCare]]);
				listAdd.Invoke(icons, [icon]);
			}

			if (hediffs.Any(comp.ignoredHediffs.Contains))
			{
				var icon = iconCtor.Invoke(null);
				setTexture.Invoke(icon, [Widgets.CheckboxOffTex]);
				setColor.Invoke(icon, [new Color(1, 1, 1, 0.5f)]);
				listAdd.Invoke(icons, [icon]);
			}

			fill.Invoke(iconStack, [icons]);

			return iconStack;
		}
	}
}
