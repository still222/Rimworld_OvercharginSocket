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
	private const float DefaultChargePerTick = 0.00083333335f;	// From original charger class. It uses it as a plain number, could change with game version
	private static int TechLevel => MechTechUtility.GetLevel();	// From 1 to 4, depends on currently researched mech's technology
	private Mote moteOvercharging;			// For the red glow of overcharging mechs
	private bool OverchargeOnInt = false;	// For handling flick-like logic for Overcharging
	private int realPowerLevel = 1;			// Updates on the tick which actually updates power
	public int PowerLevel = 1;				// Updates from the interface
	public bool ExpectsHeavyMech = false;	// Gizmo shows power consumption depending on this bool. For chargers that charge non-Light or was charging them last time
	public bool wantsOvercharge = false;	// Controlled by gizmos, similar to flicking
	public int MaxOvercharge => Props.powerLevels * TechLevel;	// This is sent to the gizmo tooltip
	public virtual int MaxPowerLevel => Overcharged ? MaxOvercharge : Props.powerLevels;
	public virtual float PowerScaling => Props.scalingMod > 1f ? (float)Math.Pow(Props.scalingMod, PowerLevel - 1) : 1f;
	public virtual float LightPowerUsage => realPowerLevel * Props.lightMechCost * PowerScaling;
	public virtual float HeavyPowerUsage => realPowerLevel * Props.heavyMechCost * PowerScaling;
	public virtual bool Overchargable => Props.overchargable && TechLevel > 1;
	public virtual bool Overclockable => Props.overclockable;
	public virtual bool Overcharged
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
		powerComp = parent.GetComp<CompPowerTrader>();

		if (Overchargable && !Overclockable)
			Log.Warning($"[StkChargingStations] {Charger.def.LabelCap} is overchargable but not overclockable, check its XML.");
		
		if (!IsLightCompatible)
			ExpectsHeavyMech = true;
	}

	public override void PostExposeData()
	{
		base.PostExposeData();

		Scribe_Values.Look(ref PowerLevel, "PowerLevel", 1);
		Scribe_Values.Look(ref OverchargeOnInt, "OverchargeOnInt", false);
		Scribe_Values.Look(ref wantsOvercharge, "wantsOvercharge", false);
		Scribe_Values.Look(ref ExpectsHeavyMech, "ExpectsHeavyMech", false);

		if (Scribe.mode == LoadSaveMode.PostLoadInit)
		{
			if (!Overclockable)
				PowerLevel = 1;

			if (!Overchargable)
			{
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

			text += $"Power level: {PowerLevel}/{MaxPowerLevel}";
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
				{
					if (PowerLevel > MaxPowerLevel)
						PowerLevel = MaxPowerLevel;

					realPowerLevel = PowerLevel;
				}

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
		
		if (Overcharged && (moteOvercharging == null || moteOvercharging.Destroyed))
			moteOvercharging = MoteMaker.MakeAttachedOverlay(mech, StkDefOf.StkMote_Overcharging, Vector3.zero);

		moteOvercharging?.Maintain();
	}

	public override IEnumerable<Gizmo> CompGetGizmosExtra()
	{
		if (parent.Faction != Faction.OfPlayer)
			yield break;

		if (powerComp != null && Overclockable)
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
					Order = 20f,
					hotKey = KeyBindingDefOf.Command_ColonistDraft
				};

			}

		}

	}

	[SyncMethod(SyncContext.None)]
	public void SetPowerLevel(float inputLevel)
	{
		if (!Overclockable)
			return;

		int level = Mathf.Clamp(Mathf.RoundToInt(inputLevel), 1, MaxPowerLevel);

		if (PowerLevel != level)
			PowerLevel = level;
	}

	// Flickable Overcharge
	[SyncMethod(SyncContext.None)]
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