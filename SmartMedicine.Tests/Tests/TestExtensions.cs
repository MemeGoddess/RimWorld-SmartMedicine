using RimWorld;
using Verse;

namespace SmartMedicine.Tests;

public static class TestExtensions
{
	public static Thing Spawn(this Map map, ThingDef thingDef, int count = 1, IntVec3? loc = null)
	{
		var thing = GenSpawn.Spawn(thingDef, loc ?? map.Center, map);
		if(count > 1)
			thing.stackCount = count;
		return thing;
	}

	public static Thing SpawnWithStockpile(this Map map, ThingDef thingDef, int count = 1, IntVec3? loc = null)
	{
		var stockPile = new Zone_Stockpile(default, map.zoneManager);
		map.zoneManager.RegisterZone(stockPile);
		stockPile.AddCell(loc ?? map.Center);
		var thing = map.Spawn(thingDef, count, loc ?? map.Center);
		map.resourceCounter.CheckUpdateResource(thing);
		return thing;
	}
}
