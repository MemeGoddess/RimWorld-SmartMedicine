
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SmartMedicine.Tests;

public class GenStep_Floor : GenStep_Terrain
{
	public override int SeedPart => 529830;

	public override void Generate(Map map, GenStepParams parms)
	{
		var terrainGrid = map.terrainGrid;
		var nearCells = new List<IntVec3>();
		using (map.pathing.DisableIncrementalScope())
		{
			foreach (var allCell in map.AllCells)
			{
				var edifice = allCell.GetEdifice(map);
				var floor = TerrainDefOf.Concrete;
				if (!floor.supportsRock && edifice != null)
				{
					nearCells.Add(edifice.Position);
					edifice.Destroy();
				}
				terrainGrid.SetTerrain(allCell, floor);
			}
			RoofCollapseCellsFinder.RemoveBulkCollapsingRoofs(nearCells, map);
			foreach (var terrainPatchMaker in map.Biome.terrainPatchMakers)
				terrainPatchMaker.Cleanup();
		}
	}
}
