using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace SmartMedicine.Compatibility
{
	public static class CompatibilityLoader
  {
		public static bool NiceHealthTab = ModLister.AnyModActiveNoSuffix(["andromeda.nicehealthtab"]);


		public static int CompatCount = new[] {NiceHealthTab}.Count(x => x);
  }
}
