using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace StkOvercharginSocket;
public class Gizmo_PowerLevel(CompPowerLevel comp) : Gizmo_Slider
{
	private readonly CompPowerLevel comp = comp;
	private static int TechLevel => MechTechUtility.GetLevel();
	private int MaxPowerLevel = comp.Props.PowerLevels * TechLevel;
	private static bool draggingBar;

	protected override float Target
	{
		get => (float)comp.PowerLevel / MaxPowerLevel;
		set => comp.SetPowerLevel(
			Mathf.Clamp(
				Mathf.RoundToInt(value * MaxPowerLevel),
				1,
				MaxPowerLevel
			)
		);
	}

	protected override float ValuePercent =>
		(float)comp.PowerLevel / MaxPowerLevel;

	protected override string Title => "Power Level";

	protected override bool IsDraggable => true;

	protected override string BarLabel =>
		$"{comp.PowerLevel} / {MaxPowerLevel} ({comp.PowerUsageMod:F0} W)";

	protected override bool DraggingBar
	{
		get => draggingBar;
		set => draggingBar = value;
	}

	protected override string GetTooltip()
	{
		return "";
	}
}

public class Command_SetPowerLevel : Command
//Mimics how vanilla handles multiple selected refuelables
{
	public CompPowerLevel comp;
	private List<CompPowerLevel> comps;
	private static int TechLevel => MechTechUtility.GetLevel();
	public override void ProcessInput(Event ev)
	{
		base.ProcessInput(ev);

		comps ??= [];

		if (!comps.Contains(comp))
			comps.Add(comp);

		int max = comps.Min(c => c.Props.PowerLevels * TechLevel);

		int start = comps[0].PowerLevel;

		Find.WindowStack.Add(new Dialog_Slider(
			"Set Power Level".Translate(),
			1,
			max,
			value =>
			{
				foreach (var c in comps)
					c.SetPowerLevel(value);
			},
			start
		));
	}

	public override bool InheritInteractionsFrom(Gizmo other)
	{
		comps ??= [];

		comps.Add(((Command_SetPowerLevel)other).comp);
		return false;
	}
}