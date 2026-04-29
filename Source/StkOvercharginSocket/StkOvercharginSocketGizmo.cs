using UnityEngine;
using Verse;

namespace StkOvercharginSocket;
public class Gizmo_PowerLevel : Gizmo_Slider
{
	private CompPowerLevel comp;
	private static bool draggingBar;

	public Gizmo_PowerLevel(CompPowerLevel comp)
	{
		this.comp = comp;
	}

	protected override float Target
	{
		get => (float)comp.PowerLevel / comp.Props.PowerLevels;
		set => comp.SetPowerLevel(
			Mathf.Clamp(
				Mathf.RoundToInt(value * comp.Props.PowerLevels),
				1,
				comp.Props.PowerLevels
			)
		);
	}

	protected override float ValuePercent =>
		(float)comp.PowerLevel / comp.Props.PowerLevels;

	protected override string Title => "Power Level";

	protected override bool IsDraggable => true;

	protected override string BarLabel =>
		$"{comp.PowerLevel} / {comp.Props.PowerLevels} ({comp.PowerUsageMod:F0} W)";

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