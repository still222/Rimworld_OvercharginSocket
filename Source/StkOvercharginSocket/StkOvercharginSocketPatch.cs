using HarmonyLib;
using RimWorld;
using Verse;

namespace StkOvercharginSocket;

[HarmonyPatch(typeof(Building_MechCharger), nameof(Building_MechCharger.Tick))]
public static class Patch_MechCharger_Tick
{
	public static void Prefix(Building_MechCharger __instance)
	{
		if (__instance.IsHashIntervalTick(250))
		{
			CompPowerLevel powerLevel = __instance.GetComp<CompPowerLevel>();

			if (__instance.currentlyChargingMech != null && __instance.Power.PowerOn)
				__instance.Power.PowerOutput = (0f - __instance.Power.Props.PowerConsumption) * powerLevel.PowerUsageMod;

			else
				__instance.Power.PowerOutput = 0f;
		}
	}
}