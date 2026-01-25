using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using UnityEngine;
using Verse;
using RimWorld;

namespace SmartMedicine.Compatibility
{
	// Omg this mod is a pain to work with
	[HarmonyPatch]
	public static class NiceHealthTab_SurgeryMenu
	{
		private static readonly ConstructorInfo FloatMenuListCtor = AccessTools.Constructor(typeof(List<FloatMenuOption>));

		private static readonly MethodInfo AddRange =
			AccessTools.Method(typeof(List<FloatMenuOption>), nameof(List<FloatMenuOption>.AddRange));

		private static readonly MethodInfo CreateMenuOptions = AccessTools.Method(typeof(HediffRowPriorityCare),
			nameof(HediffRowPriorityCare.CreateCareMenuOptions));

		[HarmonyPrepare]
		static bool Prepare()
		{
			return ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);
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
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField),
				new CodeInstruction(OpCodes.Brfalse_S, skip),
				// Tendable
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Callvirt, HediffRowPriorityCare.HediffTendableNow),
				new CodeInstruction(OpCodes.Brfalse_S, skip),
				// Add Options
				listLoad,
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField),
				new CodeInstruction(OpCodes.Call, CreateMenuOptions),
				new CodeInstruction(OpCodes.Callvirt, AddRange)
			);

						return matcher.Instructions();
		}
	}

	[HarmonyPatch]
	public static class NiceHealthTab_DiseaseMenu
	{
		private static readonly MethodInfo IsOver =
			AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver));

		private static readonly MethodInfo CurrentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));
		private static readonly MethodInfo EventType = AccessTools.PropertyGetter(typeof(Event), nameof(Event.type));
		private static readonly MethodInfo Button = AccessTools.PropertyGetter(typeof(Event), nameof(Event.button));

		public static readonly MethodInfo
			HediffTendableNow = AccessTools.Method(typeof(Hediff), nameof(Hediff.TendableNow));

		private static readonly MethodInfo CreateMenu = AccessTools.Method(typeof(HediffRowPriorityCare),
			nameof(HediffRowPriorityCare.CreateCareMenu));

		[HarmonyPrepare]
		static bool Prepare()
		{
			return ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);
		}

		[HarmonyTargetMethods]
		static MethodBase[] TargetMethods()
		{
			var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
			return [AccessTools.Method(type, "DrawAffectWholeBodyRowIconDisease")];
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
			ILGenerator il)
		{
			var matcher = new CodeMatcher(instructions);

			matcher.MatchStartForward(
				new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawBoxSolid)))
			).ThrowIfInvalid("Unable to find Draw call");

			var drawCall = matcher.Pos;
			matcher.MatchStartBackwards(new CodeMatch(x =>
					x.IsLdloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Rect)))
				.ThrowIfInvalid("Unable to find rect");
			var rectLoad =
				new CodeInstruction(OpCodes.Ldloc, (matcher.Instruction.Clone().operand as LocalBuilder)!.LocalIndex);

			matcher.Advance(drawCall - matcher.Pos);

			var end = matcher.InstructionAt(1);
			var skip = il.DefineLabel();
			end.labels.Add(skip);

			matcher.InsertAfterAndAdvance(
				rectLoad,
				new CodeInstruction(OpCodes.Call, IsOver),
				new CodeInstruction(OpCodes.Brfalse_S, skip),
				new CodeInstruction(OpCodes.Call, CurrentEvent),
				new CodeInstruction(OpCodes.Call, Button),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Ceq),
				new CodeInstruction(OpCodes.Brfalse_S, skip),
				new CodeInstruction(OpCodes.Call, CurrentEvent),
				new CodeInstruction(OpCodes.Callvirt, EventType),
				new CodeInstruction(OpCodes.Ldc_I4_0),
				new CodeInstruction(OpCodes.Ceq),
				new CodeInstruction(OpCodes.Brfalse_S, skip)
			).ThrowIfInvalid("Instructions invalid after checking Right Click on Nice Hediff row");

			matcher.InsertAfterAndAdvance(
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField),
				new CodeInstruction(OpCodes.Brfalse_S, skip),
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField),
				new CodeInstruction(OpCodes.Ldc_I4_1),
				new CodeInstruction(OpCodes.Callvirt, HediffTendableNow),
				new CodeInstruction(OpCodes.Brfalse_S, skip)
			).ThrowIfInvalid("Instructions invalid after checking Tendable on Nice Hediff row");

			matcher.InsertAfter(
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField),
				new CodeInstruction(OpCodes.Call, CreateMenu)
			).ThrowIfInvalid("Instructions invalid after setting up Specific Care Menu on Hediff row");

			var debug2 = string.Join("\n", instructions.Select(x => x.ToString()));
						return matcher.Instructions();
		}
	}

	[HarmonyPatch]
	public static class NiceHealthTab_InjectDiseaseHediff
	{
		private static readonly MethodInfo IsOver =
			AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver));

		private static readonly MethodInfo CurrentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));
		private static readonly MethodInfo Button = AccessTools.PropertyGetter(typeof(Event), nameof(Event.button));

		public static readonly MethodInfo
			HediffTendableNow = AccessTools.Method(typeof(Hediff), nameof(Hediff.TendableNow));

		private static readonly MethodInfo CreateMenu = AccessTools.Method(typeof(HediffRowPriorityCare),
			nameof(HediffRowPriorityCare.CreateCareMenu));

		[HarmonyPrepare]
		static bool Prepare()
		{
			return ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);
		}

		[HarmonyTargetMethod]
		static MethodBase TargetMethod()
		{
			var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
			return AccessTools.Method(type, "DrawDiseases");
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
			ILGenerator il)
		{
			var matcher = new CodeMatcher(instructions);

			matcher.MatchStartForward(new CodeMatch(x =>
					x.opcode == OpCodes.Callvirt && x.operand is MethodInfo { Name: "<DrawDiseases>g__DrawRow|0" }))
				.Repeat(m =>
				{
					m.MatchStartBackwards(new CodeMatch(x =>
						x.IsLdloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Hediff)));
					var hediffLoad = m.Instruction.Clone();

					m.InsertAndAdvance(
						hediffLoad,
						new CodeInstruction(OpCodes.Stsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField)
					).ThrowIfInvalid("Instructions invalid after inserting Hediff");

					m.MatchStartForward(new CodeMatch(x =>
						x.opcode == OpCodes.Callvirt && x.operand is MethodInfo { Name: "<DrawDiseases>g__DrawRow|0" }));

					m.InsertAfterAndAdvance(
						new CodeInstruction(OpCodes.Ldnull),
						new CodeInstruction(OpCodes.Stsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField)
					);
				});

						return matcher.Instructions();
		}
	}

	[HarmonyPatch]
	public static class NiceHealthTab_InjuryIcons
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
			return ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);
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
			

			var test = AccessTools.PropertyGetter(typeof(Nullable<Rect>), nameof(Nullable<Rect>.HasValue));
			var skip = il.DefineLabel();
			matcher.Insert(
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField),

				new CodeInstruction(OpCodes.Ldloca_S, rect),
				new CodeInstruction(OpCodes.Call, test),
				new CodeInstruction(OpCodes.Brtrue, skip),

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

				new CodeInstruction(OpCodes.Ldloca_S, rect) { labels = [skip] },
				new CodeInstruction(OpCodes.Call, miGetValueOrDefault),
				new CodeInstruction(OpCodes.Call,
					AccessTools.Method(typeof(NiceHealthTab_InjuryIcons),
						nameof(DrawExtraIcons)))
			);


						return matcher.Instructions();
		}

		private static AccessTools.FieldRef<Texture2D[]> careTextures =
			AccessTools.StaticFieldRefAccess<Texture2D[]>(AccessTools.Field(typeof(MedicalCareUtility), "careTextures"));

		private static Texture2D[] loadedCareTextures;
		public static void DrawExtraIcons(Hediff hediff, Rect rect)
		{
			if (hediff == null)
				return;
			var save = GUI.color;
			GUI.color = Color.white;
			if (PriorityCareSettingsComp.Get().TryGetValue(hediff, out MedicalCareCategory heCare))
			{
				rect.x -= 20;
				loadedCareTextures ??= careTextures();
				Texture2D tex = loadedCareTextures[(int)heCare];
				GUI.DrawTexture(new Rect(rect.x, rect.y, 20f, 20f), tex);
			}

			GUI.color = save;

			if (PriorityCareSettingsComp.GetIgnore().Contains(hediff))
			{
				rect.x -= 20;
				save = GUI.color;
				GUI.color = new Color(1, 1, 1, 0.5f);
				GUI.DrawTexture(new Rect(rect.x, rect.y, 20f, 20f), Widgets.CheckboxOffTex);
				GUI.color = save;

			}
		}
	}

	[HarmonyPatch]
	public static class NiceHealthTab_DiseaseIcons
	{
		private static readonly MethodInfo IsOver =
			AccessTools.Method(typeof(Mouse), nameof(Mouse.IsOver));

		private static readonly MethodInfo CurrentEvent = AccessTools.PropertyGetter(typeof(Event), nameof(Event.current));
		private static readonly MethodInfo Button = AccessTools.PropertyGetter(typeof(Event), nameof(Event.button));

		public static readonly MethodInfo
			HediffTendableNow = AccessTools.Method(typeof(Hediff), nameof(Hediff.TendableNow));

		private static readonly MethodInfo CreateMenu = AccessTools.Method(typeof(HediffRowPriorityCare),
			nameof(HediffRowPriorityCare.CreateCareMenu));

		[HarmonyPrepare]
		static bool Prepare()
		{
			return ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);
		}

		[HarmonyTargetMethod]
		static MethodBase TargetMethod()
		{
			var type = AccessTools.TypeByName("NiceHealthTab.DollDrawer");
			foreach (var nestedType in type.GetNestedTypes(BindingFlags.NonPublic))
			{
				var method = nestedType
					.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
					.FirstOrDefault(m =>
						m.Name.Contains("DrawDiseases") &&
						m.Name.Contains("b__") &&
						m.GetParameters().Length == 1 &&
						m.GetParameters()[0].ParameterType == typeof(Rect));

				if (method != null)
					return method;
			}

			Verse.Log.Error("Could not find DrawDiseases Rect lambda");
			return null;
		}

		[HarmonyTranspiler]
		public static IEnumerable<CodeInstruction> MakeMenu(IEnumerable<CodeInstruction> instructions,
			ILGenerator il)
		{
			var nullableFloatCtor = AccessTools.Constructor(
				typeof(Rect?),
				new[] { typeof(Rect) }
			);

			var miGetValueOrDefault = AccessTools.Method(
				typeof(Rect?),
				nameof(Nullable<Rect>.GetValueOrDefault),
				Type.EmptyTypes
			);
			var matcher = new CodeMatcher(instructions);

			var rectLocal = il.DeclareLocal(typeof(Rect));

			matcher.End();
			var exit = matcher.Instruction.labels.ToList();
			matcher.Instruction.labels.Clear();

			matcher.Start();
			matcher.MatchStartForward(new CodeMatch(x =>
				x.IsStloc() && x.operand is LocalBuilder lb && lb.LocalType == typeof(Rect)))
				.ThrowIfInvalid("Unable to find Rect to draw icons on infections");

			var rectLoad = new CodeInstruction(OpCodes.Ldloc, (matcher.Instruction.operand as LocalBuilder)!.LocalIndex);

			matcher.End();
			matcher.MatchStartBackwards(new CodeMatch(x =>
				x.opcode == OpCodes.Newobj && x.operand is ConstructorInfo ci && ci.DeclaringType == typeof(Rect)))
				.ThrowIfInvalid("Unable to find new Rect being made");

			matcher.InsertAfter(
				new CodeInstruction(OpCodes.Newobj, nullableFloatCtor),
				new CodeInstruction(OpCodes.Stloc_S, rectLocal),
				new CodeInstruction(OpCodes.Ldloca_S, rectLocal),
				new CodeInstruction(OpCodes.Call, miGetValueOrDefault)
			)
			.ThrowIfInvalid("Instructions invalid after capturing rect in DrawRow");

			matcher.End();
			matcher.Insert(
				new CodeInstruction(OpCodes.Ldsfld, NiceHealthTab_InjectInjuryHediff.CurrentHediffField) { labels = exit},
				new CodeInstruction(OpCodes.Ldarg_1),
				new CodeInstruction(OpCodes.Ldloc, rectLocal),
				new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(NiceHealthTab_DiseaseIcons), nameof(DrawExtraIcons)))
			);

			return matcher.Instructions();
		}

		private static void DrawExtraIcons(Hediff hediff, Rect first, Rect? second)
		{
			if (second != null)
			{
				NiceHealthTab_InjuryIcons.DrawExtraIcons(hediff, second.Value);
				return;
			}

			first = first.ContractedBy(4f);
			var rect = new Rect(first.xMax, first.center.y - first.height / 2, first.height * 2f, first.height);
			NiceHealthTab_InjuryIcons.DrawExtraIcons(hediff, rect);
		}
	}
  [HarmonyPatch]
  public static class NiceHealthTab_InjectInjuryHediff
  {
	private static readonly MethodInfo MakeMedOperationsForFloatMenuPart =
	AccessTools.Method("NiceHealthTab.DollDrawer:MakeMedOperationsFloatMenuForPart");
	private static readonly MethodInfo DrawAffectHediffGroupedRow =
			AccessTools.Method("NiceHealthTab.DollDrawer:DrawAffectHediffGroupedRow");

		private static readonly MethodInfo DrawDiseases = AccessTools.Method("NiceHealthTab.DollDrawer:DrawDiseases");

		public static readonly FieldInfo CurrentHediffField =
			AccessTools.Field(typeof(NiceHealthTab_InjectInjuryHediff), nameof(CurrentHediff));

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
}