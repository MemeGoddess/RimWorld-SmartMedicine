using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using SmartMedicine.Utilities;
using Verse;

namespace SmartMedicine;
public class PriorityCareSettingsComp : FetchOnceGameComponent<PriorityCareSettingsComp>
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
		return GetComp().hediffCare;
	}

	public static HashSet<Hediff> GetIgnore()
	{
		return GetComp().ignoredHediffs;
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