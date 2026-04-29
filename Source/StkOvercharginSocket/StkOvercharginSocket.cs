using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Multiplayer.API;
using UnityEngine;
using Verse;

namespace StkOvercharginSocket;

[StaticConstructorOnStartup]
public static class StkOvercharginSocket_MP
{
	static StkOvercharginSocket_MP()
	{
		if (MP.enabled)
		{
			MP.RegisterSyncMethod(typeof(CompPowerLevel), nameof(CompPowerLevel.SetPowerLevel));
		}
	}
}

[StaticConstructorOnStartup]
public static class Startup
{
	static Startup()
	{
		var harmony = new Harmony("stk.sfw.patcher");
		harmony.PatchAll();
	}
}

[StaticConstructorOnStartup]
public class Command_SetPowerLevel : Command
{
	public CompPowerLevel comp;
	private List<CompPowerLevel> comps;

	public override void ProcessInput(Event ev)
	{
		base.ProcessInput(ev);

		comps ??= [];

		if (!comps.Contains(comp))
			comps.Add(comp);

		int max = comps.Min(c => c.Props.PowerLevels);

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