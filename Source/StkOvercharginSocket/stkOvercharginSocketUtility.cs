using RimWorld;
using UnityEngine;
using Verse;

namespace StkOvercharginSocket;

[DefOf]
public static class StkDefOf
{
	//public static ResearchProjectDef BasicMechtech;
	public static ResearchProjectDef StandardMechtech;
	public static ResearchProjectDef HighMechtech;
	public static ResearchProjectDef UltraMechtech;
	public static DesignationDef StkDesignationFlick;
	public static IncidentDef ShortCircuit;
	public static ThingDef StkMote_Overcharging;
	public static JobDef StkOverchargeFlick;
}

public static class MechTechUtility
{
	public static int GetLevel()
	{
		if (StkDefOf.UltraMechtech.IsFinished) return 4;
		if (StkDefOf.HighMechtech.IsFinished) return 3;
		if (StkDefOf.StandardMechtech.IsFinished) return 2;
		else return 1;
	}

	public static float GetChargingPercentPerHour(this Pawn p)
	// For replacing default inspect string on a mech
	{
		var energy = p.needs?.energy;
		if (energy == null)
			return 0f;

		return 2.0833333f / energy.MaxLevel * p.OverclockValue();	// 50 (default value of energy) / 24 = 2.0833333~
	}

	public static int OverclockValue(this Pawn p)
	{
		var charger = p.needs?.energy?.currentCharger;
		if (charger == null)
			return 1;

		var comp = charger.GetComp<CompPowerLevel>();
		if (comp == null)
			return 1;

		return comp.powerLevel;
	}

	public static void ProduceWaste(this Building_MechCharger c, int chargeMod)
	{
		c.wasteProduced += c.WasteProducedPerTick * chargeMod;
		c.wasteProduced = Mathf.Clamp(c.wasteProduced, 0f, c.WasteProducedPerChargingCycle);
		if (c.wasteProduced >= c.WasteProducedPerChargingCycle && !c.Container.innerContainer.Any)
		{
			c.wasteProduced = 0f;
			c.GenerateWastePack();
		}

	}

	public static void UpdateOverchargeFlickDesignation(Thing t)
	{
		bool wantsFlick = t is ThingWithComps twc &&
			twc.AllComps.Any(c => c is CompPowerLevel f && f.WantsFlick());

		Designation designation = t.Map.designationManager.DesignationOn(t, StkDefOf.StkDesignationFlick);

		if (wantsFlick && designation == null)
			t.Map.designationManager.AddDesignation(new Designation(t, StkDefOf.StkDesignationFlick));

		else if (!wantsFlick)
			designation?.Delete();

	}

}

