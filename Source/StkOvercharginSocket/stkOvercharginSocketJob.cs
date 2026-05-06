using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace StkOvercharginSocket;

public class JobDriver_OverchargeFlick : JobDriver_Flick
{
	protected override IEnumerable<Toil> MakeNewToils()
	{
		this.FailOnDespawnedOrNull(TargetIndex.A);
		this.FailOn(() => base.Map.designationManager.DesignationOn(base.TargetThingA, StkDefOf.StkDesignationFlick) == null);
		yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
		yield return Toils_General.Wait(15).FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
		Toil finalize = ToilMaker.MakeToil("MakeNewToils");
		finalize.initAction = delegate
		{
			Pawn actor = finalize.actor;
			ThingWithComps thingWithComps = (ThingWithComps)actor.CurJob.targetA.Thing;
			foreach (var comp in thingWithComps.AllComps)
			{
				if (comp is CompPowerLevel powerComp && powerComp.WantsFlick())
					powerComp.DoFlick();

			}

			actor.records.Increment(RecordDefOf.SwitchesFlicked);
			base.Map.designationManager.DesignationOn(thingWithComps, StkDefOf.StkDesignationFlick)?.Delete();
		};

		finalize.defaultCompleteMode = ToilCompleteMode.Instant;
		yield return finalize;
	}
	
}

public class WorkGiver_OverchargeFlick : WorkGiver_Flick
{
	public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
	{
		foreach (Designation item in pawn.Map.designationManager.designationsByDef[StkDefOf.StkDesignationFlick])
			yield return item.target.Thing;
	}

	public override bool ShouldSkip(Pawn pawn, bool forced = false)
	{
		return !pawn.Map.designationManager.AnySpawnedDesignationOfDef(StkDefOf.StkDesignationFlick);
	}

	public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
	{
		if (pawn.Map.designationManager.DesignationOn(t, StkDefOf.StkDesignationFlick) == null)
			return false;

		if (!pawn.CanReserve(t, 1, -1, null, forced))
			return false;

		return true;
	}

	public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
	{
		return JobMaker.MakeJob(StkDefOf.StkOverchargeFlick, t);
	}

}

