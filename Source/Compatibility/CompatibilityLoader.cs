using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartMedicine.Compatibility.CombatExtended;
using Verse;

namespace SmartMedicine.Compatibility
{
	public static class CompatibilityLoader
  {
		public static bool NiceHealthTab = ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);
		public static bool CombatExtended = ModLister.AnyModActiveNoSuffix(["CETeam.CombatExtended"]);

		public static int CompatCount = new[] {NiceHealthTab}.Count(x => x);
		
		public static void Setup()
		{
			if (CombatExtended)
			{
				CE_StabilizeUtility.Setup();
				Mod.harmony.PatchCategory("CombatExtended");
			}
		}
  }
}
