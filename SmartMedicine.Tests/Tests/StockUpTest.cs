using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DevTools.Testing;
using GiddyUp.Tests;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using SmartMedicine.Compatibility;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SmartMedicine.Tests;

[TestFixture(TestType.Playing)]
public class StockUpTest
{
	private Pawn stocker;
	private FieldInfo UltraSpeedBoost = AccessTools.Field(typeof(TickManager), "UltraSpeedBoost");
	private FieldInfo CurTimeSpeed = AccessTools.Field(typeof(TickManager), "curTimeSpeed");
	private MethodInfo InspectPaneUtilityToggleTab = AccessTools.Method(typeof(InspectPaneUtility), "InterfaceToggleTab");
	private static Dictionary<(IntVec3, IntVec3), float> pathCosts = new();
	
	private static bool wasCalled = false;
	
	[OneTimeSetUp]
	public void TickFast()
	{
		UltraSpeedBoost.SetValue(null, true);
		CurTimeSpeed.SetValue(Find.TickManager, TimeSpeed.Ultrafast);
	}

	[OneTimeTearDown]
	public void DontTickFast() =>
		UltraSpeedBoost.SetValue(null, false);

	[SetUp]
	public void Init()
	{
		if (stocker != null) 
			return;
		
		stocker = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
		while(stocker.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
			stocker = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
		
		stocker.health.Reset();
		
		GenSpawn.Spawn(stocker, Find.CurrentMap.Center, Find.CurrentMap);
		stocker.Name = new NameSingle("Smart Doctor");
		
		stocker.skills.Learn(SkillDefOf.Medicine, 100000000);
		stocker.workSettings.SetPriority(WorkTypeDefOf.Doctor, 1);
	}
	
	// [Test]
	// public IEnumerator ButtonShowsInMenu()
	// {
	// 	wasCalled = false;
	// 	yield return new WaitForFixedUpdate();
	// 	Find.Selector.Select(stocker);
	// 	var tab = CompatibilityLoader.CombatExtended ? InspectTabManager.GetSharedInstance(AccessTools.TypeByName("CombatExtended.ITab_Inventory")) : InspectTabManager.GetSharedInstance(typeof(ITab_Pawn_Gear));
	// 	var window = MainButtonDefOf.Inspect.TabWindow;
	// 	Find.WindowStack.Add(window);
	// 	InspectPaneUtilityToggleTab.Invoke(null, [tab, window]);
	// 	yield return new WaitForFixedUpdate();
	// 	yield return new WaitForFixedUpdate();
	// 	yield return new WaitForFixedUpdate();
	// 	yield return TickWaiter.YieldWaitUntil(() => wasCalled, GenDate.TicksPerHour * 6).GetEnumerator();
	// 	Expect.IsTrue(wasCalled, "Button is not rendering in Gear Tab");
	// }
	
	[Test]
	public IEnumerator StockUpOnMeds()
	{
		var medDef = ThingDefOf.MedicineIndustrial;
		
		Find.CurrentMap.SpawnWithStockpile(medDef, 2, Find.CurrentMap.Center + new IntVec3(15, 0, 0));
		
		stocker.StockUpSettings().Add(medDef, 1);
		stocker.jobs.EndCurrentJob(JobCondition.InterruptForced);
		yield return stocker.WaitJob(SmartMedicineJobDefOf.StockUp);
		Expect.IsTrue(stocker.inventory.innerContainer.InnerListForReading.Any(x => x.def == medDef));
	}

	[Test]
	public IEnumerator DoctorWillTendOnGround()
	{
		foreach (var pawn in Find.CurrentMap.mapPawns.AllPawnsSpawned.Where(x => x != stocker).ToList())
		{
			if(pawn == stocker)
				continue;

			pawn.Destroy();
		}

		var patient = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
		GenSpawn.Spawn(patient, Find.CurrentMap.Center - new IntVec3(10, 0, 0), Find.CurrentMap);
		var part = patient.RaceProps.body.corePart;
		patient.Name = new NameSingle("Patient1");

		var originalFieldTendingForLackOfBed = Mod.settings.fieldTendingForLackOfBed;
		var originalFieldTendingAlways = Mod.settings.fieldTendingAlways;
		var originalFieldTendingIfDying = Mod.settings.fieldTendingIfDying;
		
		// No bed - Should lay down
		try
		{
			Mod.settings.fieldTendingForLackOfBed = true;
			Mod.settings.fieldTendingAlways = false;

			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 10, 1f, hitPart: part));
			Expect.LessThan(HealthUtility.TicksUntilDeathDueToBloodLoss(patient), int.MaxValue,
				patient.health.hediffSet.hediffs.Select(x => x.Label + ": " + x.BleedRate).ToLineList());
			patient.jobs.EndCurrentJob(JobCondition.InterruptForced);

			yield return patient.WaitJob(JobDefOf.LayDown, waitJobType: WaitJobType.StartsJob);

			yield return stocker.WaitJob(JobDefOf.TendPatient, waitJobType: WaitJobType.StartsJob);
			yield return new WaitUntil(() => !patient.health.HasHediffsNeedingTendByPlayer());
		}
		finally
		{
			Mod.settings.fieldTendingAlways = originalFieldTendingAlways;
			Mod.settings.fieldTendingAlways = originalFieldTendingForLackOfBed;
		}
		
		patient.health.Reset();
		patient.jobs.EndCurrentJob(JobCondition.InterruptForced);
		patient.Name = new NameSingle("Patient2");
		
		
		// No Bed - Should suffer
		try
		{
			Mod.settings.fieldTendingForLackOfBed = false;
			Mod.settings.fieldTendingAlways = false;

			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 10, 1f, hitPart: part));
			Expect.LessThan(HealthUtility.TicksUntilDeathDueToBloodLoss(patient), int.MaxValue,
				patient.health.hediffSet.hediffs.Select(x => x.Label + ": " + x.BleedRate).ToLineList());
			patient.jobs.EndCurrentJob(JobCondition.InterruptForced);
			yield return patient.WaitJob(JobDefOf.LayDown, (int)(GenDate.TicksPerHour * 0.25),
				waitJobType: WaitJobType.DoesntStartJob);
		}
		finally
		{
			Mod.settings.fieldTendingAlways = originalFieldTendingAlways;
			Mod.settings.fieldTendingAlways = originalFieldTendingForLackOfBed;
		}
		
		patient.health.Reset();
		patient.jobs.EndCurrentJob(JobCondition.InterruptForced);
		patient.Name = new NameSingle("Patient3");
		
		// Bed far away - Should tend enough
		var harmony = new Harmony("SmartMedicine.Tests");
		
		var originalGetBed = AccessTools.Method(typeof(RestUtility), nameof(RestUtility.FindPatientBedFor));
		var prefixGetBed = new HarmonyMethod(GetBed);
		harmony.Patch(originalGetBed, prefix: prefixGetBed);

		var originalGetPathCost = AccessTools.Method(typeof(Settings), nameof(Settings.GetPathCost));
		var prefixGetPathCost = new HarmonyMethod(GetPathCost);
		harmony.Patch(originalGetPathCost, prefixGetPathCost);
		
		try
		{
			Mod.settings.fieldTendingForLackOfBed = false;
			Mod.settings.fieldTendingAlways = false;
			Mod.settings.fieldTendingIfDying = true;


			var farBedSpot = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Bed, GenStuff.DefaultStuffFor(ThingDefOf.Bed)),
				Find.CurrentMap.Center - new IntVec3(5, 0, 0), Find.CurrentMap) as Building_Bed;
			farBedSpot.Medical = true;
			farBedSpot.SetFactionDirect(Faction.OfPlayer);
			const float carryMoveSpeedFactor = 0.6f;
			var carryTimeFactor = 1f / carryMoveSpeedFactor;
			
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 10, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 5, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 1, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 1, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 1, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 5, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 1, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 1, 1f, hitPart: part));
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 1, 1f, hitPart: part));
			patient.health.AddHediff(HediffDefOf.CatatonicBreakdown);
			Expect.LessThan(HealthUtility.TicksUntilDeathDueToBloodLoss(patient), int.MaxValue,
				patient.health.hediffSet.hediffs.Select(x => x.Label + ": " + x.BleedRate).ToLineList());
			yield return new WaitForFixedUpdate();
			var ticksUntilDead = FieldTendingUtility.TicksUntilDead(patient);
			
			// Die before hospital
			pathCosts[(patient.Position, IntVec3.Zero)] = ticksUntilDead - GenDate.TicksPerHour;
			
			yield return stocker.WaitJob(JobDefOf.TendPatient, waitJobType: WaitJobType.StartsJob);
			yield return new WaitUntilTimeout(() => FieldTendingUtility.TicksUntilDead(patient) > ticksUntilDead);
			stocker.jobs.EndCurrentJob(JobCondition.InterruptForced);
			
			var tendedTicks = FieldTendingUtility.TicksUntilDead(patient);
			Expect.IsTrue(tendedTicks > ticksUntilDead, "Did not heal");
			Expect.IsTrue(tendedTicks != int.MaxValue, "Should not have fully tended");
			stocker.WaitJob(JobDefOf.Rescue);
		}
		finally
		{
			Mod.settings.fieldTendingAlways = originalFieldTendingAlways;
			Mod.settings.fieldTendingForLackOfBed = originalFieldTendingForLackOfBed;
			Mod.settings.fieldTendingIfDying = originalFieldTendingIfDying;

			harmony.Unpatch(originalGetBed, prefixGetBed.method);
			harmony.Unpatch(originalGetPathCost, prefixGetPathCost.method);
		}
	}
	
	internal static void WasCalled(Pawn pawn) => wasCalled = true;

	internal static bool GetBed(ref Building_Bed __result)
	{
		__result = new Building_Bed()
		{
			Position = IntVec3.Zero
		};
		return false;
	}

	internal static bool GetPathCost(Pawn pawn, IntVec3 start, IntVec3 end, ref float __result)
	{
		if(!pathCosts.TryGetValue((start, end), out var cost))
			return true;
		__result = cost;
		return false;
	}
}
