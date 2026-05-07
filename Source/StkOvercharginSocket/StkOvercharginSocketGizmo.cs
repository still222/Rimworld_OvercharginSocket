using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace StkOvercharginSocket;

[StaticConstructorOnStartup]
public class Gizmo_PowerLevel(CompPowerLevel comp) : Gizmo_Slider
{
	private static bool draggingBar;
	private static readonly Texture2D PreciseLevel = ContentFinder<Texture2D>.Get("UI/Commands/StkSetTargetOverclock");
	private int MaxPowerLevel => comp.MaxPowerLevel;
	private float FloatStepLevel => 1f / MaxPowerLevel;
	private float LightMechPowerUsage => comp.PowerLevel * comp.Props.lightMechCost * comp.PowerScaling;
	private float HeavyMechPowerUsage => comp.PowerLevel * comp.Props.heavyMechCost * comp.PowerScaling;

	protected override float ValuePercent => (float)comp.PowerLevel / MaxPowerLevel;
	protected override int Increments { get => MaxPowerLevel; }
	protected override string Title => comp.Overcharged ? "stkChargingOverchargeLevel".TranslateSimple() : "stkChargingOverclockLevel".TranslateSimple();
	protected override bool IsDraggable => comp.Overclockable;
	protected override FloatRange DragRange { get => new(FloatStepLevel, 1f); }
	protected override string BarLabel => $"{comp.PowerLevel} / {MaxPowerLevel} ({(comp.ExpectsHeavyMech ? HeavyMechPowerUsage : LightMechPowerUsage):F0} W)";
	protected override Color BarColor 
	{
		get => comp.Overcharged 
			? new Color(0.569f, 0.125f, 0f)
			: base.BarColor;
	}
	protected override Color BarHighlightColor 
	{
		get => comp.Overcharged 
			? new Color(0.749f, 0.165f, 0f)
			: base.BarHighlightColor;
	}

	protected override bool DraggingBar
	{
		get => draggingBar;
		set => draggingBar = value;
	}
	protected override float Target
	{
		get => (float)comp.PowerLevel / MaxPowerLevel;
		set => comp.SetPowerLevel(value * MaxPowerLevel);
	}

	protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
	{
		headerRect.xMax -= 24f;
		Rect rect = new(headerRect.xMax, headerRect.y, 24f, 24f);
		GUI.DrawTexture(rect, PreciseLevel);

		if (Widgets.ButtonInvisible(rect))
		{
			Command_SetPowerLevel command = new()
			{
				comp = comp,
				defaultLabel = "stkSetPowerLevel".TranslateSimple(),
				defaultDesc = "stkSetPowerLevelDesc".TranslateSimple(),
				icon = PreciseLevel
			};

			command.ProcessInput(Event.current);

			SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
		}

		if (Mouse.IsOver(rect))
		{
			Widgets.DrawHighlight(rect);
			TooltipHandler.TipRegion(rect, OverchargeTip, 828267373);
			mouseOverElement = true;
		}

		base.DrawHeader(headerRect, ref mouseOverElement);
	}

	private string OverchargeTip()
	{
		return "stkSetPowerLevelDesc".TranslateSimple();
	}

	protected override string GetTooltip()
	{
		return "";
	}
	
}

[StaticConstructorOnStartup]
public class Command_SetPowerLevel : Command
// Mimics how vanilla handles multiple selected refuelables
{
	public CompPowerLevel comp;
	private List<CompPowerLevel> comps;
	public override void ProcessInput(Event ev)
	{
		base.ProcessInput(ev);

		comps ??= [];

		if (!comps.Contains(comp))
			comps.Add(comp);

		int max = comps.Min(c => c.MaxPowerLevel);

		int start = comps[0].PowerLevel;

		static string PowerLevelTextGetter(int x)
		{
			return "stkSetPowerLevelGizmo".Translate(x);
		}

		Find.WindowStack.Add(new Dialog_Slider(PowerLevelTextGetter, 1, max, delegate(int value)
		{
			foreach (var c in comps)
				c.SetPowerLevel(value);
		}, start));

	}

	public override bool InheritInteractionsFrom(Gizmo other)
	{
		comps ??= [];

		comps.Add(((Command_SetPowerLevel)other).comp);
		return false;
	}

}
