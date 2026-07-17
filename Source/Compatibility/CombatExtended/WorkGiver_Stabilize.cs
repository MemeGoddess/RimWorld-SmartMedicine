using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace SmartMedicine.Compatibility.CombatExtended;

public class WorkGiver_Stabilize : WorkGiver_Scanner
{
	public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

	public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

	public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

	public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
	{
		return pawn.Map.mapPawns.SpawnedPawnsWithAnyHediff;
	}
	
	public override bool ShouldSkip(Pawn pawn, bool forced = false) => 
		forced || !CompatibilityLoader.CombatExtended || !Mod.settings.fieldTendingIfDying;

	public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
	{
		if (forced)
			return false;
		
		if (t is not Pawn { Downed: true } patient) 
			return false;

		if (!IsValidTendTarget(pawn, patient) ||
		    !patient.health.hediffSet.GetHediffsTendable().Any(h => h.CanBeStabilized()))
			return false;
		
		if (!HealthAIUtility.ShouldBeTendedNowByPlayer(patient)) 
			return false;
		var ticksUntilDead = FieldTendingUtility.TicksUntilDead(patient);
		var bed = RestUtility.FindPatientBedFor(patient);
			
		return (ticksUntilDead < GenDate.TicksPerHour * 2 || (bed != null && ticksUntilDead < FieldTendingUtility.DistanceTo(pawn, patient, bed, pawn))) && pawn.inventory?.innerContainer?.InnerListForReading?.Any(x => x.def.IsMedicine) is true && pawn.CanReserve((LocalTargetInfo) (Thing) patient, ignoreOtherReservations: forced) && (!patient.IsMutant || patient.mutant.Def.entitledToMedicalCare) && (!patient.InAggroMentalState || patient.health.hediffSet.HasHediff(HediffDefOf.Scaria));
	}
	
	public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
	{
		if(t is not Pawn pawn1)
			return null;
		
		var meds = GetBestMeds(pawn);
		meds ??=  GetBestMeds(pawn1);
		if(meds == null)
			return null;
		
		Job job = JobMaker.MakeJob(CE_JobDefOf.Stabilize, (LocalTargetInfo) t, (LocalTargetInfo) meds);
		job.count = 1;
		PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_Stabilizing, KnowledgeAmount.Total);
		return job;
	}

	private static Medicine GetBestMeds(Pawn pawn)
	{
		if (pawn?.inventory?.innerContainer == null)
			return null;

		var meds = new List<Medicine>();
		foreach (var thing in pawn.inventory.innerContainer.InnerListForReading)
		{
			if(!thing.def.IsMedicine || thing is not Medicine med)
				continue;
			meds.Add(med);
		}
		
		if(meds.Count == 0)
			return null;

		return meds.MaxBy(x => x.GetStatValue(StatDefOf.MedicalPotency));
	}
	
	private bool IsValidTendTarget(Pawn doctor, Pawn patient)
	{
		return (patient.Downed || !patient.HostileTo(doctor.Faction) && (patient.IsColonist || patient.IsQuestLodger() || patient.IsPrisonerOfColony || patient.IsSlaveOfColony || patient.Faction == Faction.OfPlayer && patient.IsAnimal || patient.IsColonySubhuman && patient.mutant.Def.entitledToMedicalCare));
	}
}