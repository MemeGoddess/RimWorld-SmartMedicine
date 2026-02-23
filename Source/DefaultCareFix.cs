using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using SmartMedicine.Utilities;
using UnityEngine;
using UnityStandardAssets.ImageEffects;
using Verse;
using Verse.AI;

namespace SmartMedicine
{
	[HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
	public class DefaultCareFix : FetchOnceGameComponent<DefaultCareFix>
	{
		private Dictionary<Pawn, MedicalCareCategory> OriginalCare = new();
		private Dictionary<Pawn, (int, MedicalCareCategory)> OriginalCareExpiry = new();

		private List<Pawn> intKey = new List<Pawn>();
		private List<MedicalCareCategory> intValue = new List<MedicalCareCategory>();
		public DefaultCareFix(Game game)
		{

		}

		public override void GameComponentTick()
		{
			if (Find.TickManager.TicksGame % 2500 != 0)
				return;

			var expired = OriginalCareExpiry
				.Where(x => x.Value.Item1 < Find.TickManager.TicksGame)
				.ToList();

			foreach (var (pawn, expiry) in expired)
			{
				if (!OriginalCare.TryGetValue(pawn, out var care))
					continue;
				Verse.Log.Warning(
					$"[SmartMedicine] DefaultCareFix Rollback has expired after 24 hours for {pawn.Label}, this should not happen.");

				OriginalCare.Remove(pawn);
				OriginalCareExpiry.Remove(pawn);
			}
		}

		public void AddWithExpiry(Pawn pawn, MedicalCareCategory care)
		{
			OriginalCare[pawn] = care;
			OriginalCareExpiry[pawn] = (Find.TickManager.TicksGame + 60000, pawn.playerSettings.medCare);
		}

		static void Postfix(JobDriver __instance, JobCondition condition)
		{
			if (__instance is not JobDriver_TendPatient)
				return;

			var pawn = __instance.job.targetA.Pawn;

			RollbackCare(pawn);
		}

		static void RollbackCare(Pawn patient)
		{
			if (patient == null)
				return;

			var gameComponent = GetComp();

			if (!gameComponent.OriginalCare.TryGetValue(patient, out var care))
				return;

			patient.playerSettings.medCare = care;

			gameComponent.OriginalCare.Remove(patient);
		}

		public MedicalCareCategory? GetOriginalCare(Pawn patient)
		{
			if (patient == null)
				return null;

			var gameComponent = GetComp();

			if (!gameComponent.OriginalCare.ContainsKey(patient))
				return null;

			gameComponent.OriginalCare.TryGetValue(patient, out var care);
			
			return care;
		}

		public override void ExposeData()
		{
			Scribe_Collections.Look(ref OriginalCare, nameof(OriginalCare), LookMode.Reference, LookMode.Value, ref intKey, ref intValue);
		}
	}
}

