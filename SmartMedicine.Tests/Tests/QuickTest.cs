using DevTools.Testing;
using RimWorld;
using SmartMedicine.Tests;
using Verse;

namespace GiddyUp.Tests;

public class QuickTest
{
	protected Faction? faction;
	protected Map Map;
	protected PawnKindDef colonist;

	protected Pawn rider;
	protected Pawn mount;
    
	[OneTimeSetUp]
	public void OneTimeSetup()
	{
		Map = Find.CurrentMap;
		faction = Faction.OfPlayer;
		colonist = PawnKindDefOf.Colonist;
	}

	[SetUp]
	public void SetUp()
	{
		rider = PawnGenerator.GeneratePawn(colonist, faction);
		mount = PawnGenerator.GeneratePawn(PawnKindDefOf.Muffalo, faction);
		GenSpawn.Spawn(mount, Map.Center, Map);
		GenSpawn.Spawn(rider, Map.Center, Map);
	}

	[TearDown]
	public void TearDown()
	{
		CleanupPawn(rider);
		CleanupPawn(mount);
	}
    
	protected static void CleanupPawn(Pawn pawn)
	{
		if (pawn == null || pawn.Destroyed)
			return;

		if (pawn.Spawned)
			pawn.DeSpawn();

		pawn.Destroy();
	}
}
