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
			
		return (ticksUntilDead < GenDate.TicksPerHour * 2 || (bed != null && ticksUntilDead < FieldTendingUtility.DistanceTo(pawn, patient, bed, pawn))) &&
		       FindBestMedicine.Find(pawn, patient, out _, true).Any() && 
		       pawn.CanReserve((LocalTargetInfo) (Thing) patient, ignoreOtherReservations: forced) && 
		       (!patient.IsMutant || patient.mutant.Def.entitledToMedicalCare) && 
		       (!patient.InAggroMentalState || patient.health.hediffSet.HasHediff(HediffDefOf.Scaria));
	}
	
	public override Job JobOnThing(Pawn healer, Thing t, bool forced = false)
	{
		if(t is not Pawn patient)
			return null;
		
		var meds = GetBestMeds(healer, patient);
		if(meds == null)
			return null;
		
		Job job = JobMaker.MakeJob(CE_JobDefOf.Stabilize, (LocalTargetInfo) t, (LocalTargetInfo) meds);
		job.count = 1;
		PlayerKnowledgeDatabase.KnowledgeDemonstrated(CE_ConceptDefOf.CE_Stabilizing, KnowledgeAmount.Total);
		return job;
	}

	private static Medicine GetBestMeds(Pawn healer, Pawn patient)
	{
		if (healer?.inventory?.innerContainer == null)
			return null;

		var meds = FindBestMedicine.Find(healer, patient, out int totalCount, true);
		if(meds.Count == 0)
			return null;

		return meds.FirstOrDefault().Thing as Medicine;
	}
	
	private bool IsValidTendTarget(Pawn doctor, Pawn patient)
	{
		return (patient.Downed || !patient.HostileTo(doctor.Faction)) && 
			(patient.IsColonist || patient.IsQuestLodger() || patient.IsPrisonerOfColony || patient.IsSlaveOfColony || patient.Faction == Faction.OfPlayer && patient.IsAnimal || patient.IsColonySubhuman && patient.mutant.Def.entitledToMedicalCare) &&
			patient.playerSettings.medCare >= MedicalCareCategory.HerbalOrWorse;
	}
}