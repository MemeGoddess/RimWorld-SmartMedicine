using System.Linq;
using DevTools;
using DevTools.Testing;
using RimWorld;
using SmartMedicine.Compatibility;
using SmartMedicine.Compatibility.CombatExtended;
using TD.Utilities;
using Verse;

namespace SmartMedicine.Tests;

[TestFixture(TestType.Playing)]
public class StabilizationTest
{
	[Test]
	public void NoMedicinePrisonerIsNotEligibleForStabilization()
	{
		// Stabilization is only supplied when Combat Extended is active.
		if (!CompatibilityLoader.CombatExtended)
			return;

		Pawn doctor = null;
		Pawn prisoner = null;
		using (new ScopedRollback<bool>(ref Mod.settings.fieldTendingIfDying))
		{
			try
			{
				Mod.settings.fieldTendingIfDying = true;
				doctor = CreateDoctor();
				prisoner = CreateNoMedicinePrisoner();

				var stabilizableInjury = CreateStabilizableEmergency(prisoner);
				Expect.IsTrue(prisoner.IsPrisonerOfColony, "Patient must be a colony prisoner.");
				Expect.IsTrue(prisoner.Downed, "Patient must be downed.");
				Expect.IsTrue(prisoner.GetCare() == MedicalCareCategory.NoMeds,
					"Patient must have the no medicine care setting.");
				Expect.IsTrue(doctor.inventory.innerContainer.InnerListForReading.Any(thing => thing.def.IsMedicine),
					"Doctor must carry medicine.");
				Expect.IsTrue(stabilizableInjury != null,
					"Patient must have a Combat Extended stabilizable injury.");
				Expect.LessThan(FieldTendingUtility.TicksUntilDead(prisoner), GenDate.TicksPerHour * 2,
					"Patient must need emergency stabilization.");

				var workGiver = new WorkGiver_Stabilize();
				Expect.IsFalse(workGiver.HasJobOnThing(doctor, prisoner),
					"Doctors must not stabilize prisoners set to no medicine.");
			}
			finally
			{
				doctor?.Destroy();
				prisoner?.Destroy();
			}
		}
	}

	private Pawn CreateDoctor()
	{
		var doctor = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
		while (doctor.WorkTypeIsDisabled(WorkTypeDefOf.Doctor))
			doctor = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);

		doctor.health.Reset();
		doctor.skills.Learn(SkillDefOf.Medicine, 100000000);
		doctor.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial));
		GenSpawn.Spawn(doctor, Find.CurrentMap.Center, Find.CurrentMap);
		return doctor;
	}

	private Pawn CreateNoMedicinePrisoner()
	{
		var prisoner = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,
			Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate));
		prisoner.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
		prisoner.playerSettings.medCare = MedicalCareCategory.NoMeds;
		GenSpawn.Spawn(prisoner, Find.CurrentMap.Center + IntVec3.East * 2, Find.CurrentMap);
		return prisoner;
	}

	private static Hediff CreateStabilizableEmergency(Pawn patient)
	{
		patient.health.AddHediff(HediffDefOf.Anesthetic);
		for (var damageCount = 0; damageCount < 20; damageCount++)
		{
			patient.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8f));
			var injury = patient.health.hediffSet.GetHediffsTendable().FirstOrDefault(hediff => hediff.CanBeStabilized());
			if (injury != null && FieldTendingUtility.TicksUntilDead(patient) < GenDate.TicksPerHour * 2)
				return injury;
		}

		return null;
	}
}
