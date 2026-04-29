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
	public int PowerLevel = 1;
	private CompPowerTrader powerComp;
	public virtual float PowerUsageMod => PowerLevel * PowerScaling;
	public virtual float PowerScaling => Props.ScalingEnabled ? (float)Math.Pow(1.025, PowerLevel - 1) : 1f;
	public int ComplexityBonus => Props.ComplexityPerLevel * PowerLevel;

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		powerComp = parent.GetComp<CompPowerTrader>();
		UpdatePower();
	}
	public override void PostExposeData()
	{
		base.PostExposeData();
		Scribe_Values.Look(ref PowerLevel, "PowerLevel", 1, false);
		UpdatePower();
	}

	private void UpdatePower()
	{
		
	}

	public override string CompInspectStringExtra()
	{
		string text = base.CompInspectStringExtra();

		if (!text.NullOrEmpty())
			text += "\n";

		text += $"Power level: {PowerLevel}/{Props.PowerLevels}";
		text += $"\nPower usage: {PowerUsageMod:F0} W";

		return text;
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

	[SyncMethod(SyncContext.None)]
	public void SetPowerLevel(int level)
	{
		level = Mathf.Clamp(level, 1, Props.PowerLevels);

		if (PowerLevel != level)
		{
			PowerLevel = level;
			UpdatePower();
		}
	}
}

public class CompProperties_PowerLevel : CompProperties
{
	public int PowerLevels = 25;
	public int ComplexityPerLevel = 2;
	public float firstLevelConsumption = 50f;
	public bool ScalingEnabled = false;

	public CompProperties_PowerLevel()
	{
		compClass = typeof(CompPowerLevel);
	}
}