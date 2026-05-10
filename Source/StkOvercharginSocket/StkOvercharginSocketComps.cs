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
	private Building_MechCharger charger;
	private Building_MechCharger Charger => charger ??= parent as Building_MechCharger;
	private List<MechWeightClassDef> mechClassesList;
	private List<MechWeightClassDef> MechClassesList => mechClassesList ??= Charger.def.building.requiredMechWeightClasses;
	private List<MechWeightClassDef> lightMechClasses;
	private List<MechWeightClassDef> LightMechClasses => lightMechClasses ??=
		[.. Props.countsAsLightWeightClasses
			.Append(MechWeightClassDefOf.Light)
			.Distinct()];
	private bool IsLightCompatible => MechClassesList.Any(LightMechClasses.Contains);
	private bool IsHeavyCompatible => MechClassesList.Any(def => !LightMechClasses.Contains(def));
	private static int TechLevel => MechTechUtility.GetLevel();	// From 1 to 4, depends on currently researched mech's technology
	private const float defaultChargePerTick = 0.00083333335f;	// From original charger class. It uses it as a plain number, could change with game version

	[SyncField]
	public int powerLevel = 1;				// Updates from the interface
	private float mechEnergyBonus;			// To simlify tick calculations we cache the value every 250 ticks
	private int realPowerLevel = 1;			// Updates on the tick which actually updates power
	public bool expectsHeavyMech = false;	// Gizmo shows power consumption depending on this bool. For chargers that charge non-Light or was charging them last time
	private bool OverchargeOnInt = false;	// For handling flick-like logic for Overcharging
	public bool wantsOvercharge = false;	// Controlled by gizmos, similar to flicking
	public bool critOverchargeSet = false;	// Controls incidents and send info for UI
	private Mote moteOvercharging;			// For the red glow of overcharging mechs
	public int MaxPowerLevel => Overcharged ? Props.powerLevels * TechLevel : Props.powerLevels;
	public float PowerScaling => Props.scalingMod > 1f ? (float)Math.Pow(Props.scalingMod, powerLevel - 1) : 1f;
	public float LightPowerUsage => realPowerLevel * Props.lightMechCost * PowerScaling;
	public float HeavyPowerUsage => realPowerLevel * Props.heavyMechCost * PowerScaling;
	public bool Overchargable => Props.overchargable && TechLevel > 1;
	public bool Overclockable => Props.overclockable;
	public bool Overcharged
	{
		get => OverchargeOnInt;
		set
		{
			if (OverchargeOnInt != value)
				OverchargeOnInt = value;
		}

	}

	public override void PostSpawnSetup(bool respawningAfterLoad)
	{
		base.PostSpawnSetup(respawningAfterLoad);

		if (Overchargable && !Overclockable)
			Log.Warning($"[StkChargingStations] {Charger.def.LabelCap} is overchargable but not overclockable, check its XML.");
		
		if (!IsLightCompatible)
			expectsHeavyMech = true;
	}

	public override void PostExposeData()
	{
		base.PostExposeData();

		Scribe_Values.Look(ref powerLevel, "PowerLevel", 1);
		Scribe_Values.Look(ref critOverchargeSet, "critOvercharge", false);
		Scribe_Values.Look(ref OverchargeOnInt, "OverchargeOnInt", false);
		Scribe_Values.Look(ref wantsOvercharge, "wantsOvercharge", false);
		Scribe_Values.Look(ref expectsHeavyMech, "ExpectsHeavyMech", false);

		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			if (!Overclockable)
				powerLevel = 1;

			if (!Overchargable)
			{
				critOverchargeSet = false;
				OverchargeOnInt = false;
				wantsOvercharge = false;
			}

		}

	}

	public override string CompInspectStringExtra()
	{
		string text = base.CompInspectStringExtra();

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

		var mech = Charger.currentlyChargingMech;
		bool powerOn = Charger.Power?.PowerOn ?? false;

		// Power update
		if (parent.IsHashIntervalTick(250))
		{
			critOverchargeSet = Overcharged && powerLevel > Props.powerLevels;

			if (mech == null || !powerOn)
				Charger.Power.PowerOutput = 0f;

			else
			{
				if (Overclockable)
				{
					realPowerLevel = powerLevel;	// Used for actual calculations
					mechEnergyBonus = defaultChargePerTick * (realPowerLevel - 1);
				}

				if (IsLightCompatible && IsHeavyCompatible)
				{
					var mechClass = mech.kindDef.race.race.mechWeightClass;
					expectsHeavyMech = mechClass != null && !LightMechClasses.Contains(mechClass);
				}

				Charger.Power.PowerOutput = expectsHeavyMech
					? -HeavyPowerUsage
					: -LightPowerUsage;
			}

		}

		// Overclock logic
		if (!Overclockable || mech == null || !powerOn || realPowerLevel <= 1)
			return;

		mech.needs.energy.CurLevel += mechEnergyBonus;
		MechTechUtility.ProduceWaste(Charger, realPowerLevel - 1);

		if (!critOverchargeSet) return;
		
		if (moteOvercharging == null || moteOvercharging.Destroyed)
			moteOvercharging = MoteMaker.MakeAttachedOverlay(mech, StkDefOf.StkMote_Overcharging, Vector3.zero);

		moteOvercharging?.Maintain();
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
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
		if (!Overclockable)
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
		if (!Overchargable)
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

			critOverchargeSet = false;
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
	public float scalingMod = 1.02157f;	// To better tune the power scaling through XML. 1.02157 should add additional x1.5 scaling on level 20.

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