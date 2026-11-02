using System;
using System.Collections;
using DevTools.Testing;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartMedicine.Tests;

public static class TickWaiter
{
	public static int BlockingWaitUntil(Func<bool>? action = null, int timeout = GenDate.TicksPerDay)
	{
		action ??= () => true;
        
		var ticks = 0;
		var tickManager = Find.TickManager;
		while (!action() && ticks < timeout)
		{
			tickManager.DoSingleTick();
			ticks++;
		}

		if (!action()) 
			Test.Fail($"Timeout after {ticks} ticks");
		return ticks;
	}
	
	public static IEnumerable YieldWaitUntil(Func<bool>? action = null, int timeout = GenDate.TicksPerDay)
	{
		action ??= () => true;
        
		var tickManager = Find.TickManager; var ticks = tickManager.TicksGame + timeout;
		var speed = tickManager.CurTimeSpeed;
		
		tickManager.CurTimeSpeed = TimeSpeed.Superfast;
		yield return new WaitUntil(() =>
		{
			tickManager.CurTimeSpeed = TimeSpeed.Ultrafast; 
			return action() || tickManager.TicksGame > ticks;
		});
		tickManager.CurTimeSpeed = speed;

		if (!action()) 
			Test.Fail($"Timeout after {ticks} ticks");
	}

	public static void WaitJobBlocking(this Pawn pawn, JobDef expectedJob,  int timeout = GenDate.TicksPerDay)
	{
		var tickManager = Find.TickManager;
		if (pawn.CurJob == null ||  pawn.CurJobDef != expectedJob)
		{
			var endTick = Find.TickManager.TicksGame + 10000;
			while (Find.TickManager.TicksGame < endTick)
				tickManager.DoSingleTick();
			if(Find.TickManager.TicksGame >= endTick)
				Test.Fail($"No job started after 100 ticks");
		}
		var jobID = pawn.CurJob?.loadID ?? -1;

		while (pawn.CurJob?.loadID == jobID && tickManager.TicksGame < timeout) 
			tickManager.DoSingleTick();
		
		if(tickManager.TicksGame >= timeout)
			Test.Fail($"Timeout after {timeout} ticks");
	}
}
