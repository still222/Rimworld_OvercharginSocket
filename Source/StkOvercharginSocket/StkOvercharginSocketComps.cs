using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;

namespace StkOvercharginSocket;

public class CompPowerLevel : ThingComp
{
	private CompPowerTrader powerComp;
	public CompProperties_PowerLevel Props => (CompProperties_PowerLevel)props;
	private Building_MechCharger Charger => parent as Building_MechCharger;
	private List<MechWeightClassDef> MechClassesList => Charger.def.building.requiredMechWeightClasses;
	private List<MechWeightClassDef> LightMechClasses =>
		[.. Props.countsAsLightWeightClasses
			.Append(MechWeightClassDefOf.Light)
			.Distinct()];
	private bool IsLightCompatible => MechClassesList.Any(LightMechClasses.Contains);
	private bool IsHeavyCompatible => MechClassesList.Any(def => !LightMechClasses.Contains(def));
	private static int TechLevel => MechTechUtility.GetLevel();	// From 1 to 4, depends on currently researched mech's technology
	private const float DefaultChargePerTick = 0.00083333335f;	// From original charger class. It uses it as a plain number, could change with game version
	private int realPowerLevel = 1;			// Updates on the tick which actually updates power
	public int PowerLevel = 1;				// Updates from the interface
	public bool Overcharged = false;		// For overpowered charging with explosions
	public bool ExpectsHeavyMech = false;	// Gizmo shows power consumption depending on this bool. For chargers that charge non-Light or was charging them last time
	public virtual bool Overclockable => Props.overclockable;
	public virtual float PowerScaling => Props.scalingEnabled ? (float)Math.Pow(1.025, PowerLevel - 1) : 1f;
	public virtual float LightPowerUsage => realPowerLevel * Props.lightMechCost * PowerScaling;
	public virtual float HeavyPowerUsage => realPowerLevel * Props.heavyMechCost * PowerScaling;

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);
		powerComp = parent.GetComp<CompPowerTrader>();

		if (Props.overchargable && !Overclockable)
			Log.Warning($"[StkChargingStations] {Charger.def.LabelCap} is overchargable but not overclockable, check its XML.");
		
		if (!IsLightCompatible)
			ExpectsHeavyMech = true;
	}

	public override void PostExposeData()
	{
		base.PostExposeData();

		Scribe_Values.Look(ref PowerLevel, "PowerLevel", 1);
		Scribe_Values.Look(ref Overcharged, "Overcharged", false);
		Scribe_Values.Look(ref ExpectsHeavyMech, "ExpectsHeavyMech", false);

		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			if (!Overclockable)
				PowerLevel = 1;

			if (!Props.overchargable)
				Overcharged = false;
		}

	}

	public override string CompInspectStringExtra()
	{
		string text = base.CompInspectStringExtra();

		if (Overclockable)
		{
			if (!text.NullOrEmpty())
				text += "\n";

			text += $"Power level: {PowerLevel}/{Props.powerLevels * TechLevel}";
		}

		return text;
	}

	public override void CompTick()
	{
		base.CompTick();

		var mech = Charger.currentlyChargingMech;
		bool powerOn = Charger.Power.PowerOn;

		// Power update
		if (parent.IsHashIntervalTick(250))
		{
			if (mech == null || !powerOn)
				Charger.Power.PowerOutput = 0f;

			else
			{
				if (Overclockable)
					realPowerLevel = PowerLevel;

				if (IsLightCompatible && IsHeavyCompatible)
				{
					var mechClass = mech.kindDef.race.race.mechWeightClass;
					ExpectsHeavyMech = mechClass != null && !LightMechClasses.Contains(mechClass);
				}

				Charger.Power.PowerOutput = ExpectsHeavyMech
					? -HeavyPowerUsage
					: -LightPowerUsage;
			}

		}

		// Overclock logic
		if (!Overclockable || mech == null || !powerOn || realPowerLevel <= 1)
			return;

		int chargeMod = realPowerLevel - 1;
		mech.needs.energy.CurLevel += chargeMod * DefaultChargePerTick;
		MechTechUtility.ProduceWaste(Charger, chargeMod);
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		if (parent.Faction != Faction.OfPlayer)
			yield break;

		if (powerComp != null && Overclockable)
		{
			if (Find.Selector.SelectedObjects.Count == 1)
				yield return new Gizmo_PowerLevel(this);

			else
			{
				yield return new Command_SetPowerLevel
				{
					comp = this,
					defaultLabel = "stkSetPowerLevel".Translate(),
					defaultDesc = "stkSetPowerLevelDesc".Translate(),
					icon = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel")
				};

				if (Props.overchargable)
				{
					string str = Overcharged ? "On".Translate() : "Off".Translate();
					yield return new Command_Toggle
					{
						isActive = () => Overcharged,
						toggleAction = ToggleOvercharge,
						defaultLabel = "CommandToggleAllowAutoRefuel".Translate(),
						defaultDesc = "CommandToggleAllowAutoRefuelDescMult".Translate(str.UncapitalizeFirst().Named("ONOFF")),
						icon = Overcharged ? TexCommand.ForbidOn : TexCommand.ForbidOff,
						Order = 20f,
						hotKey = KeyBindingDefOf.Command_ColonistDraft
					};

				}

			}

		}

	}

	[SyncMethod(SyncContext.None)]
	public void SetPowerLevel(float inputLevel)
	{
		if (!Overclockable)
			return;

		int level = Mathf.Clamp(Mathf.RoundToInt(inputLevel), 1, Props.powerLevels * TechLevel);

		if (PowerLevel != level)
			PowerLevel = level;
	}

	[SyncMethod(SyncContext.None)]
	public void ToggleOvercharge()
	{
		if (!Props.overchargable)
			return;
		
		Overcharged = !Overcharged;
	}

}

public class CompProperties_PowerLevel : CompProperties
{
	public int powerLevels = 5;
	public bool scalingEnabled = true;
	public bool overclockable = true;
	public bool overchargable = false;

	// Default power cost for light and heavy chargers
	public float lightMechCost = 200f;
	public float heavyMechCost = 400f;

	// For potential mod compatability (ultra-light class)
	public List<MechWeightClassDef> countsAsLightWeightClasses = [];

	public CompProperties_PowerLevel()
	{
		compClass = typeof(CompPowerLevel);
	}

}