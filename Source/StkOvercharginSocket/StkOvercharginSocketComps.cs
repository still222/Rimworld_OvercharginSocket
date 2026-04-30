using System;
using System.Collections.Generic;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace StkOvercharginSocket;

public class CompPowerLevel : ThingComp
{
	public CompProperties_PowerLevel Props => (CompProperties_PowerLevel)props;
	private Building_MechCharger Charger => parent as Building_MechCharger;
	private CompPowerTrader powerComp;
	private const float DefaultChargePerTick = 0.00083333335f;	//Original class defines it as a const, but never uses defined value, instead retyping it again when needed.
	public int PowerLevel = 1;			//Updates from the interface
	private int realPowerLevel = 1;		//Updates when power actually get updated
	private static int TechLevel => MechTechUtility.GetLevel();
	public virtual float PowerUsageMod => PowerLevel * PowerScaling;
	public virtual float PowerScaling => Props.ScalingEnabled ? (float)Math.Pow(1.025, PowerLevel - 1) : 1f;

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		powerComp = parent.GetComp<CompPowerTrader>();
	}

	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref PowerLevel, "PowerLevel", 1, false);
	}

	public override string CompInspectStringExtra()
	{
		string text = base.CompInspectStringExtra();

		if (!text.NullOrEmpty())
			text += "\n";

		text += $"Power level: {PowerLevel}/{Props.PowerLevels * TechLevel}";

		return text;
	}

	public override void CompTick()
	{
		base.CompTick();

		if (parent.IsHashIntervalTick(250))
		{
			if (Charger.currentlyChargingMech != null && Charger.Power.PowerOn)
			{
				realPowerLevel = PowerLevel;
				Charger.Power.PowerOutput = (0f - Charger.Power.Props.PowerConsumption) * PowerUsageMod;
			}

			else
				Charger.Power.PowerOutput = 0f;
			
		}

		int chargeMod = realPowerLevel - 1;
		if (chargeMod > 0 && Charger.currentlyChargingMech != null && Charger.Power.PowerOn)
		{
			Charger.currentlyChargingMech.needs.energy.CurLevel += chargeMod * DefaultChargePerTick;
			Charger.wasteProduced += Charger.WasteProducedPerTick * chargeMod;
		}
		
	}

	[SyncMethod(SyncContext.None)]
	public void SetPowerLevel(int level)
	{
		level = Mathf.Clamp(level, 1, Props.PowerLevels * TechLevel);

		if (PowerLevel != level)
			PowerLevel = level;
	}
	
	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		if (parent.Faction != Faction.OfPlayer)
			yield break;

		if (powerComp != null)
		{
			if (Find.Selector.SelectedObjects.Count == 1)
			{
				yield return new Gizmo_PowerLevel(this);
			}

			else
			{
				yield return new Command_SetPowerLevel
				{
					comp = this,
					defaultLabel = "stkSetPowerLevel".Translate(),
					defaultDesc = "stkSetPowerLevelDesc".Translate(),
					icon = ContentFinder<Texture2D>.Get("UI/Commands/DesirePower")
				};
			}
		}
	}

}

public class CompProperties_PowerLevel : CompProperties
{
	public int PowerLevels = 5;
	public bool ScalingEnabled = false;

	public CompProperties_PowerLevel()
	{
		compClass = typeof(CompPowerLevel);
	}

}