using System;
using System.Collections.Generic;
using System.Linq;
using Multiplayer.API;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace StkOvercharginSocket;

public class CompPowerLevel : ThingComp
{
	public CompProperties_PowerLevel Props => (CompProperties_PowerLevel)props;
	private Building_MechCharger Charger;
	private List<MechWeightClassDef> mechClassesList;
	private List<MechWeightClassDef> MechClassesList => mechClassesList ??= Charger.def.building.requiredMechWeightClasses;
	private List<MechWeightClassDef> lightMechClasses;
	private List<MechWeightClassDef> LightMechClasses => lightMechClasses ??=
		[.. Props.countsAsLightWeightClasses
			.Append(MechWeightClassDefOf.Light)
			.Distinct()
		];
	private bool IsLightCompatible => MechClassesList.Any(LightMechClasses.Contains);
	private bool IsHeavyCompatible => MechClassesList.Any(def => !LightMechClasses.Contains(def));
	private static int TechLevel => MechTechUtility.GetLevel();	// From 1 to 4, depends on currently researched mech's technology
	private int realPowerLevel = 1;			// Updates on the tick which actually updates power
	private float mechEnergyBonus;			// To simlify tick calculations we cache the value every 250 ticks
	private bool failState = false;			// To catch weird mod compatability issues
	private bool wantsOvercharge = false;	// Controlled by gizmos, similar to flicking
	private Mote moteOvercharging;			// For the red glow of overcharging mechs

	[SyncField]
	public int powerLevel = 1;				// Updates from the interface
	public bool expectsHeavyMech = false;	// Gizmo shows power consumption depending on this bool. For chargers that charge non-Light or was charging them last time
	public bool Overcharged = false;		// For handling flick-like logic for Overcharging
	public bool Overclockable => !failState && Props.overclockable;
	public bool Overchargable => !failState && Props.overchargable && TechLevel > 1;
	public int MaxPowerLevel => Overcharged ? Props.powerLevels * TechLevel : Props.powerLevels;
	public float PowerScaling => Props.maxScaling <= 1f ? 1f : (float)Math.Pow(Props.maxScaling, (powerLevel - 1) / (MaxPowerLevel - 1f));

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);

		if (parent is not Building_MechCharger)
		{
			failState = true;
			Log.WarningOnce($"[StkChargingStations] {parent.def.LabelCap} has unexpected class.", 95283752);
			return;
		}

		Charger = parent as Building_MechCharger;
		if (Overchargable && !Overclockable)
			Log.WarningOnce($"[StkChargingStations] {Charger.def.LabelCap} is overchargable but not overclockable, check its XML.", 436873897);
		
		if (!IsLightCompatible)
			expectsHeavyMech = true;
	}

	public override void PostExposeData()
	{
		base.PostExposeData();

		if (failState)
			return;

		Scribe_Values.Look(ref powerLevel, "PowerLevel", 1);
		Scribe_Values.Look(ref Overcharged, "Overcharged", false);
		Scribe_Values.Look(ref wantsOvercharge, "wantsOvercharge", false);
		Scribe_Values.Look(ref expectsHeavyMech, "ExpectsHeavyMech", false);

		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			if (!Overclockable)
				powerLevel = 1;

			if (!Overchargable)
			{
				Overcharged = false;
				wantsOvercharge = false;
			}

		}

	}

	public override string CompInspectStringExtra()
	{
		string text = base.CompInspectStringExtra();

		if (failState)
			return text;

		if (Overclockable)
		{
			if (!text.NullOrEmpty())
				text += "\n";

			text += $"Power level: {powerLevel}/{MaxPowerLevel}";
		}

		return text;
	}

	public override void CompTick()
	{
		base.CompTick();

		if (failState)
			return;

		var mech = Charger.currentlyChargingMech;
		bool powerOn = Charger.Power?.PowerOn ?? false;

		// Power update
		if (parent.IsHashIntervalTick(250))
		{
			if (mech == null || !powerOn)
				Charger.Power.PowerOutput = 0f;

			else
			{
				if (Overclockable)
				{
					realPowerLevel = powerLevel;	// Used for actual calculations
					mechEnergyBonus = Building_MechCharger.ChargePerTick * (realPowerLevel - 1);
				}

				if (IsLightCompatible && IsHeavyCompatible)
				{
					var mechClass = mech.kindDef.race.race.mechWeightClass;
					expectsHeavyMech = mechClass != null && !LightMechClasses.Contains(mechClass);
				}

				Charger.Power.PowerOutput = expectsHeavyMech
					? -realPowerLevel * Props.heavyMechCost * PowerScaling
					: -realPowerLevel * Props.lightMechCost * PowerScaling;
			}

		}

		// Overclock logic
		if (!Overclockable || mech == null || !powerOn || realPowerLevel <= 1)
			return;

		mech.needs.energy.CurLevel += mechEnergyBonus;
		MechTechUtility.ProduceWaste(Charger, realPowerLevel - 1);

		if (!Overcharged) return;
		
		if (moteOvercharging == null || moteOvercharging.Destroyed)
			moteOvercharging = MoteMaker.MakeAttachedOverlay(mech, StkDefOf.StkMote_Overcharging, Vector3.zero);

		moteOvercharging?.Maintain();
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		if (failState)
			yield break;

		if (parent.Faction != Faction.OfPlayer)
			yield break;

		if (Charger.Power != null && Overclockable)
		{
			if (Find.Selector.SelectedObjects.Count > 1)
				yield return new Command_SetPowerLevel
				{
					comp = this,
					defaultLabel = "stkSetPowerLevel".TranslateSimple(),
					defaultDesc = "stkSetPowerLevelDesc".TranslateSimple(),
					icon = ContentFinder<Texture2D>.Get("UI/Commands/StkSetTargetOverclock")
				};

			else yield return new Gizmo_PowerLevel(this);

			if (Overchargable)
			{
				string str = Overcharged ? "Disable".Translate() : "Enable".Translate();
				yield return new Command_Toggle
				{
					isActive = () => wantsOvercharge,
					toggleAction = ToggleOvercharge,
					defaultLabel = "stkCommandToggleOvercharge".TranslateSimple(),
					defaultDesc = "stkCommandToggleOverchargeDescMult".Translate(str.UncapitalizeFirst().Named("ONOFF")),
					icon = ContentFinder<Texture2D>.Get("UI/Commands/StkOverchargeCommand"),
					Order = -1f,	// Should be just before vanilla flick button
					hotKey = KeyBindingDefOf.Command_ColonistDraft
				};

			}

		}

	}

	public void SetPowerLevel(float inputLevel)
	{
		if (!Overclockable || failState)
			return;

		int level = Mathf.Clamp(Mathf.RoundToInt(inputLevel), 1, MaxPowerLevel);

		if (MP.enabled)
		{
			MP.WatchBegin();
			MP.Watch(this, nameof(powerLevel));
		}

		if (powerLevel != level)
			powerLevel = level;

		if (MP.enabled)
			MP.WatchEnd();
	}

	// Flickable Overcharge
	[SyncMethod]
	public void ToggleOvercharge()
	{
		if (!Overchargable || failState)
			return;
		
		wantsOvercharge = !wantsOvercharge;
		MechTechUtility.UpdateOverchargeFlickDesignation(parent);
	}

	public void DoFlick()
	{
		Overcharged = !Overcharged;
		SoundDefOf.FlickSwitch.PlayOneShot(new TargetInfo(parent.Position, parent.Map));

		if (!Overcharged)	// When overcharge is disabled remove dangerous effects
		{
			if (powerLevel > MaxPowerLevel)
				powerLevel = MaxPowerLevel;
		}

	}

	public bool WantsFlick()
	{
		return wantsOvercharge != Overcharged;
	}

}

public class CompProperties_PowerLevel : CompProperties
{
	public int powerLevels = 5;
	public bool overclockable = true;
	public bool overchargable = false;
	public float maxScaling = 1.5f;

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