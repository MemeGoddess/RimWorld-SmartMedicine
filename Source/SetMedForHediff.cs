using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using RimWorld;
using Verse;
using HarmonyLib;
using UnityEngine;
using TD.Utilities;

namespace SmartMedicine
{
	public class PriorityCareComp : GameComponent
	{
		// This is here to suppress an error
		public PriorityCareComp(Game game)
		{
			
		}

		// Reroute existing calls to use the new comp
		public static Dictionary<Hediff, MedicalCareCategory> Get()
		{
			return Current.Game.GetComponent<PriorityCareSettingsComp>().hediffCare;
		}
	}

	public class PriorityCareSettingsComp : GameComponent
	{
		public Dictionary<Hediff, MedicalCareCategory> hediffCare;
		private List<Hediff> hediffList;
		private List<MedicalCareCategory> medCareList;

		public HashSet<Hediff> ignoredHediffs;
		public PriorityCareSettingsComp(Game game)
		{
			hediffCare = new();
			hediffList = [];
			medCareList = [];

			ignoredHediffs = new();
		}

		
		public override void ExposeData()
		{
			base.ExposeData();
			
			Scribe_Collections.Look(ref hediffCare, "hediffCare", LookMode.Reference, LookMode.Value, ref hediffList,
					ref medCareList);

			Scribe_Collections.Look(ref ignoredHediffs, "ignoredHediffs", LookMode.Reference);

			hediffCare ??= new();
			ignoredHediffs ??= new();
		}


		public static Dictionary<Hediff, MedicalCareCategory> Get()
		{
			return Current.Game.GetComponent<PriorityCareSettingsComp>().hediffCare;
		}

		public static HashSet<Hediff> GetIgnore()
		{
			var test = Current.Game.GetComponent<PriorityCareSettingsComp>().ignoredHediffs;
			return test;
		}

		public static bool MaxPriorityCare(Pawn patient, out MedicalCareCategory care) => MaxPriorityCare(patient.health.hediffSet.hediffs, out care);
		public static bool MaxPriorityCare(List<Hediff> hediffs, out MedicalCareCategory care)
		{
			care = MedicalCareCategory.NoCare;
			bool found = false;
			var hediffCare = Get();
			foreach (Hediff h in hediffs)
			{
				if (h.TendableNow() && hediffCare.TryGetValue(h, out MedicalCareCategory heCare))
				{
					care = heCare > care ? heCare : care;
					found = true;
				}
			}
			return found;
		}
		
		public static bool AllPriorityCare(Pawn patient) => AllPriorityCare(patient.health.hediffSet.hediffs);
		public static bool AllPriorityCare(List<Hediff> hediffs)
		{
			var hediffCare = Get();
			foreach(Hediff h in hediffs)
			{
				if (!hediffCare.ContainsKey(h))
					return false;
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Hediff), "PostRemoved")]
	public static class RemoveHediffHook
	{
		//public virtual void PostRemoved()
		public static void Prefix(Hediff __instance)
		{
			Log.Message($"removing {__instance} from priorityCare");
			PriorityCareSettingsComp.Get().Remove(__instance);
		}
	}
	
	[HarmonyPatch(typeof(HealthCardUtility), "EntryClicked")]
	public static class SuppressRightClickHediff
	{
		//private static void EntryClicked(IEnumerable<Hediff> diffs, Pawn pawn)
		public static bool Prefix()
		{
			//suppress right click for popup 
			return Event.current.button != 1;
		}
	}

	[StaticConstructorOnStartup]
	[HarmonyPatch(typeof(HealthCardUtility), "DrawHediffRow")]
	public static class HediffRowPriorityCare
	{

		private static readonly MethodInfo DrawHighlightIfMouseover =
			AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawHighlightIfMouseover));

		private static readonly MethodInfo WidgetLabel =
			AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), [typeof(Rect), typeof(string)]);

		private static readonly MethodInfo CurrentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));

		private static readonly MethodInfo DrawElementStack = AccessTools
			.Method(typeof(GenUI), nameof(GenUI.DrawElementStack))
			.MakeGenericMethod([typeof(GenUI.AnonymousStackElement)]);

		private static readonly MethodInfo ElementsAdd = AccessTools.Method(typeof(List<GenUI.AnonymousStackElement>),
			nameof(List<GenUI.AnonymousStackElement>.Add));

		private static readonly MethodInfo ButtonInvis = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonInvisible));
		private static readonly MethodInfo HediffTendableNow = AccessTools.Method(typeof(Hediff), nameof(Hediff.TendableNow));
		private static readonly MethodInfo Button = AccessTools.PropertyGetter(typeof(Event), nameof(Event.button));
		private static readonly MethodInfo CreateMenu = AccessTools.Method(typeof(HediffRowPriorityCare), nameof(LabelButton));

		private static readonly MethodInfo CreateElement =
			AccessTools.Method(typeof(HediffRowPriorityCare), nameof(AddElements));

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase mb)
		{
			// This might still be the best way to get it.
			var hediffIndex =
				mb.GetMethodBody()?.LocalVariables.First(lv => lv.LocalType == typeof(Hediff)).LocalIndex;
			var hediffLoad = new CodeInstruction(OpCodes.Ldloc_S, hediffIndex);

			var matcher = new CodeMatcher(instructions);

			matcher.MatchStartForward(new CodeMatch(OpCodes.Call, DrawHighlightIfMouseover))
				.ThrowIfInvalid($"Unable to find entrypoint for {nameof(HediffRowPriorityCare)}");

			matcher.MatchStartForward(new CodeMatch(OpCodes.Call, WidgetLabel))
				.ThrowIfInvalid("Unable to find Hediff label on Hediff row");

			var loadRect = matcher.InstructionAt(-2).Clone();

			var skip = il.DefineLabel();
			var exitPoint = matcher.InstructionAt(1);

			exitPoint.labels.Add(skip);

			#region Care Menu
			// Check if Right Click
			matcher.InsertAfterAndAdvance(
				new CodeInstruction(OpCodes.Call, CurrentEvent),
				new CodeInstruction(OpCodes.Call, Button),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Ceq),
				new CodeInstruction(OpCodes.Brfalse_S, skip)
			).ThrowIfInvalid("Instructions invalid after checking Right Click on Hediff row");

			// Check if clicked in this area
			matcher.InsertAfterAndAdvance(
				loadRect,
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Call, ButtonInvis),
				new CodeInstruction(OpCodes.Brfalse_S, skip)
			).ThrowIfInvalid("Instructions invalid after checking Click Area on Hediff row");

			// Check if tendable
			matcher.InsertAfterAndAdvance(
				new CodeInstruction(OpCodes.Ldloc_S, hediffIndex),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Callvirt, HediffTendableNow),
				new CodeInstruction(OpCodes.Brfalse_S, skip)
			).ThrowIfInvalid("Instructions invalid after checking Tendable on Hediff row");

			matcher.InsertAfter(
				new CodeInstruction(OpCodes.Ldloc_S, hediffIndex),
				new CodeInstruction(OpCodes.Call, CreateMenu)
			).ThrowIfInvalid("Instructions invalid after setting up Specific Care Menu on Hediff row");
			#endregion

			#region Care Icon
			matcher.End();
			matcher.MatchStartBackwards(new CodeMatch(OpCodes.Call, DrawElementStack))
				.ThrowIfInvalid("Unable to find DrawElementStack call on Hediff row");

			matcher.MatchStartBackwards(new CodeMatch(x =>
					x.IsLdloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(List<GenUI.AnonymousStackElement>)))
				.ThrowIfInvalid("Unable to find Elements list on Hediff row");

			var elementsLoad = matcher.Instruction.Clone();
			var elementsSave = new CodeInstruction(OpCodes.Stloc, elementsLoad.operand);

			matcher.MatchStartBackwards(
					new CodeMatch(x =>
						x.opcode == OpCodes.Ldfld && x.operand is FieldInfo fieldInfo && fieldInfo.FieldType == typeof(Rect)),
					new CodeMatch(x =>
						x.IsLdloc()))
				.ThrowIfInvalid("Unable to find Rect for drawing icons on Hediff row");

			var rectLoad = matcher.InstructionsInRange(matcher.Pos - 1, matcher.Pos);
			rectLoad.ForEach(x => x.labels.Clear());

			matcher.MatchStartBackwards(new CodeMatch(OpCodes.Callvirt, ElementsAdd))
				.ThrowIfInvalid("Unable to find hook for drawing icons on Hediff row");

			matcher.InsertAfter(
				new List<CodeInstruction>(rectLoad)
				{
					elementsLoad,
					hediffLoad,
					new(OpCodes.Call, CreateElement),
					elementsSave,
				}
			).ThrowIfInvalid("Instructions invalid after inserting call for drawing icons on Hediff row");
			#endregion



			var debug = string.Join("\n", matcher.Instructions().Select(x => x.ToString()));
			return matcher.Instructions();
		}


		public static List<GenUI.AnonymousStackElement> AddElements(Rect rect, List<GenUI.AnonymousStackElement> elements, Hediff hediff)
		{
			if (PriorityCareSettingsComp.Get().TryGetValue(hediff, out MedicalCareCategory heCare))
			{
				elements.Add(new GenUI.AnonymousStackElement
				{
					drawer = delegate (Rect r)
					{
						loadedCareTextures ??= careTextures();
						Texture2D tex = loadedCareTextures[(int)heCare];
						r = new Rect(2 * rect.x + rect.width - r.x - 20f, r.y, 20f, 20f);
						GUI.DrawTexture(r, tex);
					},
					width = 20f
				});
			}

			if (PriorityCareSettingsComp.GetIgnore().Contains(hediff))
			{
				elements.Add(new GenUI.AnonymousStackElement
				{
					drawer = delegate (Rect r)
					{
						r = new Rect(2 * rect.x + rect.width - r.x - 20f, r.y, 20f, 20f);
						var save = GUI.color;
						GUI.color = new Color(1, 1, 1, 0.5f);
						GUI.DrawTexture(r, Widgets.CheckboxOffTex);
						GUI.color = save;
					},
					width = 20f
				});
			}

			return elements;
		}


		private static AccessTools.FieldRef<Texture2D[]> careTextures =
			AccessTools.StaticFieldRefAccess< Texture2D[]>(AccessTools.Field( typeof(MedicalCareUtility), "careTextures"));

		private static Texture2D[] loadedCareTextures;

		public static void LabelButton(Hediff hediff)
		{
			loadedCareTextures ??= careTextures();
			var set = PriorityCareSettingsComp.GetIgnore();

			var list = new List<FloatMenuOption>
			{
				new(PatientBedRestDefOf.PatientBedRest.labelShort.CapitalizeFirst(), delegate
				{
					if (!set.Add(hediff))
						set.Remove(hediff);
				}, set.Contains(hediff) ? Widgets.CheckboxOffTex : Widgets.CheckboxOnTex, new Color(1, 1, 1, 0.5f)),
				new("TD.DefaultCare".Translate(), delegate
				{
					PriorityCareSettingsComp.Get().Remove(hediff);
				}, iconTex: loadedCareTextures[(int)hediff.pawn.playerSettings.medCare], new Color(1, 1, 1, 0.5f))
			};

			for (var i = 0; i < 5; i++)
			{
				var mc = (MedicalCareCategory)i;
				list.Add(new FloatMenuOption(mc.GetLabel().CapitalizeFirst(), delegate
				{
					PriorityCareSettingsComp.Get()[hediff] = mc;
				}, loadedCareTextures[(int)mc], Color.white));
			}

			Find.WindowStack.Add(new FloatMenu(list));
		}
	}

	[HarmonyPatch(typeof(Hediff), "TendPriority", MethodType.Getter)]
	public static class PriorityHediff
	{
		public static bool Prefix(Hediff __instance, ref float __result)
		{
			if(PriorityCareSettingsComp.Get().TryGetValue(__instance, out MedicalCareCategory hediffCare))
			{
				MedicalCareCategory defaultCare = __instance.pawn.GetCare();

				int diff = ((int)hediffCare) - ((int)defaultCare);
				__result += diff*5;//Raise priority for higher meds, lower for lower meds.
				return false;
			}
			return true;
		}
	}

	[StaticConstructorOnStartup]
	public static class PriorityCareJobFail
	{
		static PriorityCareJobFail()
		{
			HarmonyMethod transpiler = new HarmonyMethod(typeof(PriorityCareJobFail), nameof(Transpiler));
			Harmony harmony = new Harmony("uuugggg.rimworld.SmartMedicine.main");

			Predicate<MethodInfo> check = m => m.Name.Contains("MakeNewToils");

			harmony.PatchGeneratedMethod(typeof(JobDriver_TendPatient), check, transpiler: transpiler);
		}

		public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			FieldInfo medCareInfo = AccessTools.Field(typeof(Pawn_PlayerSettings), "medCare");
			MethodInfo AllowsMedicineInfo = AccessTools.Method(typeof(MedicalCareUtility), "AllowsMedicine");

			MethodInfo AllowsMedicineForHediffInfo = AccessTools.Method(typeof(PriorityCareJobFail), "AllowsMedicineForHediff");

			//
			//IL_007d: ldfld        class RimWorld.JobDriver_TendPatient/'<MakeNewToils>c__Iterator0' RimWorld.JobDriver_TendPatient/'<MakeNewToils>c__Iterator0'/'<MakeNewToils>c__AnonStorey1'::'<>f__ref$0'
			//IL_0082: ldfld class RimWorld.JobDriver_TendPatient RimWorld.JobDriver_TendPatient/'<MakeNewToils>c__Iterator0'::$this
			//IL_0087: call instance class Verse.Pawn RimWorld.JobDriver_TendPatient::get_Deliveree()
			//After Deliveree Pawn

			//IL_008c: ldfld class RimWorld.Pawn_PlayerSettings Verse.Pawn::playerSettings
			//IL_0091: ldfld valuetype RimWorld.MedicalCareCategory RimWorld.Pawn_PlayerSettings::medCare
			//Skip medCare

			//IL_0096: ldarg.0      // this
			//IL_0097: ldfld        class RimWorld.JobDriver_TendPatient/'<MakeNewToils>c__Iterator0' RimWorld.JobDriver_TendPatient/'<MakeNewToils>c__Iterator0'/'<MakeNewToils>c__AnonStorey1'::'<>f__ref$0'
			//IL_009c: ldfld class RimWorld.JobDriver_TendPatient RimWorld.JobDriver_TendPatient/'<MakeNewToils>c__Iterator0'::$this
			//IL_00a1: call instance class Verse.Thing RimWorld.JobDriver_TendPatient::get_MedicineUsed()
			//IL_00a6: ldfld class Verse.ThingDef Verse.Thing::def

			//IL_00ab: call         bool RimWorld.MedicalCareUtility::AllowsMedicine(valuetype RimWorld.MedicalCareCategory, class Verse.ThingDef)
			//Call my method instead that checks both

			//IL_00b0: brtrue IL_00b7

			List <CodeInstruction> instList = instructions.ToList();
			for (int i = 0; i < instList.Count; i++)
			{
				//pawn.AllowsMedicineForHediff, not pawn.playerSettings.medCare.AllowsMedicine
				if (instList[i].Calls(AllowsMedicineInfo))
				{
					yield return new CodeInstruction(OpCodes.Call, AllowsMedicineForHediffInfo);
				}
				else
					yield return instList[i];

				//Remove .playerSettings.medCare, just using pawn
				if (i+2 < instList.Count && 
					instList[i + 2].LoadsField(medCareInfo))
					i += 2;
			}
		}

		public static bool AllowsMedicineForHediff(Pawn deliveree, ThingDef med)
		{
			if (PriorityCareSettingsComp.MaxPriorityCare(deliveree, out MedicalCareCategory heCare))
			{
				//This is uses to allow higher medicine above normal limit below.
				//this is NOT used to stop the job is PriorityCare is lowered
				if (heCare.AllowsMedicine(med)) return true;
			}

			//Not required but hey why dont I patch this in for Pharmacist
			MedicalCareCategory care = deliveree.GetCare();

			return care.AllowsMedicine(med);
		}
	}

	[HarmonyPatch(typeof(Hediff), "TendableNow")]
	public static class PriorityCareTendableNow
	{
		//public virtual bool TendableNow(bool ignoreTimer = false);
		public static bool Prefix(ref bool __result, Hediff __instance, bool ignoreTimer)
		{
			if (ignoreTimer) return true;

			if (PriorityCareSettingsComp.Get().TryGetValue(__instance, out MedicalCareCategory heCare) && heCare == MedicalCareCategory.NoCare)
			{
				__result = false;
				return false;
			}
			return true;
		}
	}

	[DefOf]
	public static class PatientBedRestDefOf
	{
		public static WorkTypeDef PatientBedRest;
	}
}
