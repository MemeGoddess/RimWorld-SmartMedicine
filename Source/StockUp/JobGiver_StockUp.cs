using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using HarmonyLib;
using System.Reflection.Emit;
using System.Reflection;

namespace SmartMedicine
{
	[DefOf]
	public static class SmartMedicineJobDefOf
	{
		public static JobDef StockUp;
		public static JobDef StockDown;
	}

	public class JobGiver_StockUp : ThinkNode_JobGiver
	{
		public static bool Skip(Pawn pawn)
		{
			if (pawn.inventory.UnloadEverything)
				return true;

			Log.Message($"Skip need tend?");
			if (pawn.Map.mapPawns.AllPawnsSpawned.Any(p => HealthAIUtility.ShouldBeTendedNowByPlayer(p) && pawn.CanReserveAndReach(p, PathEndMode.ClosestTouch, Danger.Deadly, ignoreOtherReservations: true)))
				return true;

			if (pawn.Map.mapPawns.AllPawnsSpawned.Any(p => p is IBillGiver billGiver && billGiver.BillStack.AnyShouldDoNow && pawn.CanReserveAndReach(p, PathEndMode.ClosestTouch, Danger.Deadly, ignoreOtherReservations: true)))
				return true;

			return false;
		}
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (pawn.StockUpIsFull()) return null;
			Log.Message($"{pawn} needs stocking up");

			if (Skip(pawn))
				return null;

			var nutrientPasteStockUp = pawn.StockUpNeeds(ThingDefOf.MealNutrientPaste);
			if (nutrientPasteStockUp > 0 &&
			    StockUpUtility.EnoughAvailable(ThingDefOf.MealNutrientPaste, pawn.Map))
			{
				var dispensers = pawn.Map.listerThings.GetThingsOfType<Building_NutrientPasteDispenser>()
					.Where(x => x.CanDispenseNow &&
					            pawn.CanReach(new LocalTargetInfo(x.InteractionCell), PathEndMode.OnCell, Danger.None))
					.OrderBy(x => pawn.Position.DistanceTo(x.InteractionCell))
					.ToList();

				if (dispensers.Any())
				{
					var selected = dispensers.FirstOrDefault();

					var dispensed = 0;
					Thing stack = null;
					for (int i = 0; i < Math.Min(nutrientPasteStockUp, ThingDefOf.MealNutrientPaste.stackLimit); i++)
					{
						var meal = selected.TryDispenseFood();

						if (meal == null)
							break;

						GenPlace.TryPlaceThing(meal, selected.InteractionCell, selected.Map, ThingPlaceMode.Near);

						stack ??= meal;
						dispensed++;
					}

					if (stack != null)
						return new Job(SmartMedicineJobDefOf.StockUp, stack) { count = dispensed };
				}
			}

			Log.Message($"any things?");
			Predicate<Thing> validator = (Thing t) => pawn.StockingUpOn(t) && pawn.StockUpNeeds(t) > 0 && !MassUtility.WillBeOverEncumberedAfterPickingUp(pawn, t, 1) && pawn.CanReserve(t, FindBestMedicine.maxPawns, 1) && !t.IsForbidden(pawn);
			Thing thing = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map, ThingRequest.ForGroup(ThingRequestGroup.HaulableEver), PathEndMode.ClosestTouch, TraverseParms.For(pawn), 9999, validator);
			if (thing != null)
			{
				int pickupCount = Math.Min(pawn.StockUpNeeds(thing), MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing));
				Log.Message($"{pawn} stock thing is {thing}, count {pickupCount}");
				if (pickupCount > 0)
					return new Job(SmartMedicineJobDefOf.StockUp, thing) { count = pickupCount};
			}

			Log.Message($"{pawn} looking to return");
			Thing toReturn = pawn.StockUpThingToReturn();
			if (toReturn == null) return null;
			Log.Message($"returning {toReturn}");

			int dropCount = -pawn.StockUpNeeds(toReturn);
			Log.Message($"dropping {dropCount}");
			if (StoreUtility.TryFindBestBetterStoreCellFor(toReturn, pawn, pawn.Map, StoragePriority.Unstored, pawn.Faction, out IntVec3 dropLoc, true))
				return new Job(SmartMedicineJobDefOf.StockDown, toReturn, dropLoc) { count = dropCount };
			Log.Message($"nowhere to store");
			return null;
		}
	}

	//private void CleanupCurrentJob(JobCondition condition, bool releaseReservations, bool cancelBusyStancesSoft = true)
	[HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
	public static class CleanupCurrentJob_Patch
	{
		public static void Prefix(Pawn_JobTracker __instance, Pawn ___pawn)
		{
			if (__instance.curJob?.def == JobDefOf.TendPatient)
			{
				Pawn pawn = ___pawn;
				if (!pawn.Destroyed && pawn.carryTracker != null && pawn.carryTracker.CarriedThing != null)
				{
					if (StockUpUtility.StockingUpOn(pawn, pawn.carryTracker.CarriedThing))
						pawn.inventory.innerContainer.TryAddOrTransfer(pawn.carryTracker.CarriedThing);
				}
			}
		}
	}

	[HarmonyPatch]
	public static class DontUnloadAfterArriving_Patch
	{
		static MethodBase TargetMethod()
		{
			var prop = AccessTools.Property(typeof(Pawn_InventoryTracker), "FirstUnloadableThing");
			return prop?.GetGetMethod(nonPublic: true) ?? prop?.GetGetMethod();
		}

		static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
		{
			var list = instructions.ToList();

			var tmpField = AccessTools.Field(typeof(Pawn_InventoryTracker), "tmpItemsToKeep");

			var clearMi = AccessTools.Method(typeof(List<ThingDefCount>), nameof(List<ThingDefCount>.Clear));

			var hookMi = AccessTools.Method(typeof(DontUnloadAfterArriving_Patch), nameof(InjectStockUpSettings));

			// Sanity checks
			if (tmpField == null || clearMi == null || hookMi == null)
			{
				Verse.Log.Error("Failed to find all fields/methods to patch Caravan Unloading");
				foreach (var ci in list) yield return ci;
				yield break;
			}

			bool injected = false;

			for (int i = 0; i < list.Count; i++)
			{
				var ci = list[i];
				yield return ci;

				// If clearing
				if (!injected
				    && ci.opcode == OpCodes.Callvirt
				    && ci.operand is MethodInfo mi
				    && mi == clearMi
				    && i > 0
				    && list[i - 1].opcode == OpCodes.Ldsfld
				    && Equals(list[i - 1].operand, tmpField))
				{
					// Do 
					yield return new CodeInstruction(OpCodes.Ldarg_0);
					yield return new CodeInstruction(OpCodes.Call, hookMi);

					injected = true;
				}
			}

			if (!injected)
			{
				Verse.Log.Warning("DontUnloadAfterArriving_Patch did not find tmpItemsToKeep.Clear() to inject after");
			}
		}

		private static List<ThingDefCount> tmpRef;
		public static void InjectStockUpSettings(Pawn_InventoryTracker __instance)
		{
			var pawn = __instance?.pawn;
			if (pawn == null) return;

			tmpRef ??= AccessTools.StaticFieldRefAccess<List<ThingDefCount>>(typeof(Pawn_InventoryTracker), "tmpItemsToKeep");

			tmpRef.AddRange(pawn.StockUpSettingsAsCounts());
		}
	}
}