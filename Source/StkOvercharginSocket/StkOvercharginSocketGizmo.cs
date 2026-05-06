using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.Steam;

namespace StkOvercharginSocket;

[StaticConstructorOnStartup]
public class Gizmo_PowerLevel(CompPowerLevel comp) : Gizmo_Slider
{
	private readonly CompPowerLevel comp = comp;
	private static bool draggingBar;
	private static readonly Texture2D OverchargeIcon = ContentFinder<Texture2D>.Get("UI/Commands/SetTargetFuelLevel");
	private static int TechLevel => MechTechUtility.GetLevel();
	private int MaxPowerLevel => comp.Props.powerLevels * TechLevel;

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

	public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
	{
		if (!comp.Props.overchargable)
		{
			return base.GizmoOnGUI(topLeft, maxWidth, parms);
		}
		if (SteamDeck.IsSteamDeckInNonKeyboardMode)
		{
			return base.GizmoOnGUI(topLeft, maxWidth, parms);
		}
		KeyCode keyCode = (KeyBindingDefOf.Command_ColonistDraft != null) ? KeyBindingDefOf.Command_ColonistDraft.MainKey : KeyCode.None;
		if (keyCode != KeyCode.None && !GizmoGridDrawer.drawnHotKeys.Contains(keyCode) && KeyBindingDefOf.Command_ColonistDraft.KeyDownEvent)
		{
			if (!comp.Overcharged)
				SoundDefOf.Tick_High.PlayOneShotOnCamera();
			else
				SoundDefOf.Tick_Low.PlayOneShotOnCamera();

			comp.ToggleOvercharge();
			Event.current.Use();
		}
		return base.GizmoOnGUI(topLeft, maxWidth, parms);
	}

	protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
	{
		if (comp.Props.overchargable)
		{
			headerRect.xMax -= 24f;
			Rect rect = new(headerRect.xMax, headerRect.y, 24f, 24f);
			GUI.DrawTexture(rect, OverchargeIcon);
			GUI.DrawTexture(new Rect(rect.center.x, rect.y, rect.width / 2f, rect.height / 2f), comp.Overcharged ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
			if (Widgets.ButtonInvisible(rect))
			{
				if (!comp.Overcharged)
					SoundDefOf.Tick_High.PlayOneShotOnCamera();
				else
					SoundDefOf.Tick_Low.PlayOneShotOnCamera();

				comp.ToggleOvercharge();
			}
			if (Mouse.IsOver(rect))
			{
				Widgets.DrawHighlight(rect);
				TooltipHandler.TipRegion(rect, OverchargeTip, 828267373);
				mouseOverElement = true;
			}
		}
		base.DrawHeader(headerRect, ref mouseOverElement);
	}

	protected override string Title => "Power Level";

	protected override bool IsDraggable => true;

	protected override string BarLabel =>
		$"{comp.PowerLevel} / {MaxPowerLevel} ({(comp.ExpectsHeavyMech ? comp.HeavyPowerUsage : comp.LightPowerUsage):F0} W)";

	protected override bool DraggingBar
	{
		get => draggingBar;
		set => draggingBar = value;
	}

	private string OverchargeTip()
	{
		string text = string.Format("{0}", "CommandToggleAllowAutoRefuel".Translate()) + "\n\n";
		string str = comp.Overcharged ? "On".Translate() : "Off".Translate();
		string text2 = comp.PowerLevel.ToString("F0").Colorize(ColoredText.TipSectionTitleColor);
		string text3 = string.Concat(text + "CommandToggleAllowAutoRefuelDesc".Translate(text2, str.UncapitalizeFirst().Named("ONOFF")).Resolve(), "\n\n");
		string text4 = KeyPrefs.KeyPrefsData.GetBoundKeyCode(KeyBindingDefOf.Command_ColonistDraft, KeyPrefs.BindingSlot.A).ToStringReadable();
		return text3 + ("HotKeyTip".Translate() + ": " + text4);
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
	private static int TechLevel => MechTechUtility.GetLevel();
	public override void ProcessInput(Event ev)
	{
		base.ProcessInput(ev);

		comps ??= [];

		if (!comps.Contains(comp))
			comps.Add(comp);

		int max = comps.Min(c => c.Props.powerLevels * TechLevel);

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