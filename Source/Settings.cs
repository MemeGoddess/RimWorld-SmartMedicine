using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using SmartMedicine.Compatibility;
using TD.Utilities;

namespace SmartMedicine
{
	public class Settings : ModSettings
	{
		//TODO: save per map
		public bool useDoctorMedicine = true;
		public bool usePatientMedicine = true;
		public bool useCloseMedicine = true;
		public int distanceToUseEqualOnGround = 6;

		public bool useColonistMedicine = true;
		public bool useAnimalMedicine = true;
		public bool useOtherEvenIfFar = false;
		public int distanceToUseFromOther = 12;

		public bool minimalMedicineForNonUrgent = false;
		public bool noMedicineForNonUrgent = false;

		public bool stockUp = true;
		public float stockUpEnough = 1.5f;
		public bool stockUpReturn = false;

		public bool fieldTendingForLackOfBed = false;
		public bool fieldTendingAlways = false;

		public bool defaultUnlimitedSurgery = false;

		public bool stockAnythingWithoutDev = false;

		public bool niceHealthOptimize = true;

		private string settingDoctorInv;

		private string settingPatientInv;

		private string settingNearby;
		private string settingNearbyDesc;

		private string settingNearbyDist;
		private string settingNearbyDistDesc;

		private string spacesFormat;

		private string settingOtherInv;

		private string settingAnimalInv;

		private string settingOtherAnyDist;
		private string settingOtherAnyDistDesc;

		private string settingOtherDist;

		private string settingMinimal;
		private string settingMinimalDesc;

		private string settingNoMed;
		private string settingNoMedDesc;

		private string settingStockUp;
		private string settingStockUpDesc;

		private string settingStockUpEnough;
		private string settingStockUpEnoughDesc;

		private string settingStockUpReturn;

		private string settingFieldTendingNoBeds;
		private string settingFieldTendingNoBedsDesc;

		private string settingFieldTendingAlways;
		private string settingFieldTendingAlwaysDesc;

		private string settingGlobalSurgeryUnlimited;
		private string settingGlobalSurgeryUnlimitedDesc;

		private string stockAnythingWithoutDevLabel;

		
		private string niceHealthTab;

		private string niceHealthOptimizeLabel;

		private bool DonePre = false;
		private Vector2 scroll = Vector2.zero;
		public Rect scrollView = new();
		public void PreOpen()
		{
			if (DonePre)
				return;

			DonePre = true;
			settingDoctorInv = "TD.SettingDoctorInv".Translate();
			settingPatientInv = "TD.SettingPatientInv".Translate();
			settingNearby = "TD.SettingNearby".Translate();
			settingNearbyDesc = "TD.SettingNearbyDesc".Translate();
			settingNearbyDist = "TD.SettingNearbyDist".Translate();
			settingNearbyDistDesc = "TD.SettingNearbyDistDesc".Translate();
			spacesFormat = "TD.SpacesFormat".Translate();
			settingOtherInv = "TD.SettingOtherInv".Translate();
			settingAnimalInv = "TD.SettingAnimalInv".Translate();
			settingOtherAnyDist = "TD.SettingOtherAnyDist".Translate();
			settingOtherAnyDistDesc = "TD.SettingOtherAnyDistDesc".Translate();
			settingOtherDist = "TD.SettingOtherDist".Translate();
			settingMinimal = "TD.SettingMinimal".Translate();
			settingMinimalDesc = "TD.SettingMinimalDesc".Translate();
			settingNoMed = "TD.SettingNoMed".Translate();
			settingNoMedDesc = "TD.SettingNoMedDesc".Translate();
			settingStockUp = "TD.SettingStockUp".Translate();
			settingStockUpDesc = "TD.SettingStockUpDesc".Translate();
			settingStockUpEnough = "TD.SettingStockUpEnough".Translate();
			settingStockUpEnoughDesc = "TD.SettingStockUpEnoughDesc".Translate();
			settingStockUpReturn = "TD.SettingStockUpReturn".Translate();
			settingFieldTendingNoBeds = "TD.SettingFieldTendingNoBeds".Translate();
			settingFieldTendingNoBedsDesc = "TD.SettingFieldTendingNoBedsDesc".Translate();
			settingFieldTendingAlways = "TD.SettingFieldTendingAlways".Translate();
			settingFieldTendingAlwaysDesc = "TD.SettingFieldTendingAlwaysDesc".Translate();
			settingGlobalSurgeryUnlimited = "TD.SettingGlobalSurgeryUnlimited".Translate();
			settingGlobalSurgeryUnlimitedDesc = "TD.SettingGlobalSurgeryUnlimitedDesc".Translate();
			stockAnythingWithoutDevLabel = "TD.StockAnythingWithoutDev".Translate();
			niceHealthTab = "NiceHealthTabSettingsCategory".TryTranslate(out var niceHealthTabActual) ? niceHealthTabActual : "Nice Health Tab";
			niceHealthOptimizeLabel = "TD.NiceHealthTab.Optimize".Translate();
		}

		private static List<TabRecord> CompatTabs = null;

		private static Action<Listing_Standard> RenderCurrentTab = null;

		public bool FieldTendingActive(Pawn patient)
		{
			return patient.IsFreeColonist && 
				(fieldTendingAlways || 
				(fieldTendingForLackOfBed && RestUtility.FindPatientBedFor(patient) == null));
		}

		public void DoWindowContents(Rect wrect)
		{
			PreOpen();
			var font = Text.Font;
			var options = new Listing_Standard();

			options.BeginScrollViewEx(wrect, ref scroll, scrollView);
			options.CheckboxLabeled(settingDoctorInv, ref useDoctorMedicine);
			options.CheckboxLabeled(settingPatientInv, ref usePatientMedicine);
			if (useDoctorMedicine || usePatientMedicine)
			{
				options.CheckboxLabeled(settingNearby, ref useCloseMedicine, settingNearbyDesc);
				if (useCloseMedicine)
				{
					options.SliderLabeled(settingNearbyDist, ref distanceToUseEqualOnGround, spacesFormat, 0, 99, settingNearbyDistDesc);
				}
			}
			options.Gap();


			options.CheckboxLabeled(settingOtherInv, ref useColonistMedicine);
			options.CheckboxLabeled(settingAnimalInv, ref useAnimalMedicine);
			if (useColonistMedicine || useAnimalMedicine)
			{
				options.CheckboxLabeled(settingOtherAnyDist, ref useOtherEvenIfFar, settingOtherAnyDistDesc);
				if (!useOtherEvenIfFar)
					options.SliderLabeled(settingOtherDist, ref distanceToUseFromOther, spacesFormat, 0, 99);
			}
			options.Gap();


			options.CheckboxLabeled(settingMinimal, ref minimalMedicineForNonUrgent,
				settingMinimalDesc);
			if (minimalMedicineForNonUrgent) noMedicineForNonUrgent = false;

			options.CheckboxLabeled(settingNoMed, ref noMedicineForNonUrgent,
				settingNoMedDesc);
			if (noMedicineForNonUrgent) minimalMedicineForNonUrgent = false;
			options.Gap();

			options.CheckboxLabeled(settingStockUp, ref stockUp);
			options.Label(settingStockUpDesc);
			options.SliderLabeled(settingStockUpEnough, ref stockUpEnough, "{0:P0}", 0, 5, settingStockUpEnoughDesc);
			options.CheckboxLabeled(settingStockUpReturn, ref stockUpReturn);
			options.Gap();


			options.CheckboxLabeled(settingFieldTendingNoBeds, ref fieldTendingForLackOfBed, settingFieldTendingNoBedsDesc);
			if (fieldTendingForLackOfBed)
				fieldTendingAlways = false; 

			options.CheckboxLabeled(settingFieldTendingAlways, ref fieldTendingAlways, settingFieldTendingAlwaysDesc);
			if (fieldTendingAlways)
				fieldTendingForLackOfBed = false;
			options.Gap();

			options.CheckboxLabeled(settingGlobalSurgeryUnlimited, ref defaultUnlimitedSurgery, settingGlobalSurgeryUnlimitedDesc);

			options.CheckboxLabeled(stockAnythingWithoutDevLabel, ref stockAnythingWithoutDev, settingGlobalSurgeryUnlimitedDesc);

			if (CompatibilityLoader.CompatCount > 0 && CompatTabs == null)
			{
				CompatTabs = new();
				if(CompatibilityLoader.NiceHealthTab)
					CompatTabs.Add(new TabRecord(niceHealthTab, () => RenderCurrentTab = NiceHealthTabSettings, RenderCurrentTab == NiceHealthTabSettings));

				CompatTabs.First().clickedAction.Invoke();
			}

			switch (CompatibilityLoader.CompatCount)
			{
				case 1:
					var tab = CompatTabs[0];
					Text.Font = GameFont.Medium;
					options.Label(tab.label);
					Text.Font = font;
					break;
				case > 1:
					TabDrawer.DrawTabs(options.GetRect(32), CompatTabs);
					break;
			}

			RenderCurrentTab?.Invoke(options);

			options.EndScrollView(ref scrollView);
		}

		private void NiceHealthTabSettings(Listing_Standard options)
		{
			// TODO Add tooltip
			options.CheckboxLabeled(niceHealthOptimizeLabel, ref niceHealthOptimize);
		}


		public override void ExposeData()
		{
			Scribe_Values.Look(ref useDoctorMedicine, "useDoctorMedicine", true);
			Scribe_Values.Look(ref usePatientMedicine, "usePatientMedicine", true);
			Scribe_Values.Look(ref useCloseMedicine, "useCloseMedicine", true);
			Scribe_Values.Look(ref distanceToUseEqualOnGround, "distanceToUseEqualOnGround", 6);

			Scribe_Values.Look(ref useColonistMedicine, "useColonistMedicine", true);
			Scribe_Values.Look(ref useAnimalMedicine, "useAnimalMedicine", true);
			Scribe_Values.Look(ref useOtherEvenIfFar, "useOtherEvenIfFar", false);
			Scribe_Values.Look(ref distanceToUseFromOther, "distanceToUseFromOther", 12);

			Scribe_Values.Look(ref minimalMedicineForNonUrgent, "minimalMedicineForNonUrgent", false);
			Scribe_Values.Look(ref noMedicineForNonUrgent, "noMedicineForNonUrgent", false);
			
			Scribe_Values.Look(ref stockUp, "stockUp", true);
			Scribe_Values.Look(ref stockUpEnough, "stockUpEnough", 1.5f);
			Scribe_Values.Look(ref stockUpReturn, "stockUpReturn", false);

			Scribe_Values.Look(ref fieldTendingForLackOfBed, "fieldTendingForLackOfBed", false);
			Scribe_Values.Look(ref fieldTendingAlways, "fieldTendingAlways", false);
			Scribe_Values.Look(ref defaultUnlimitedSurgery, "defaultUnlimitedSurgery", false);
			Scribe_Values.Look(ref stockAnythingWithoutDev, "defaultUnlimitedSurgery", false);
		}
	}


}