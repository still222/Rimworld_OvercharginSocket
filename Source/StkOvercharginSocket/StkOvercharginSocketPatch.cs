using HarmonyLib;
using RimWorld;
using Verse;

namespace StkOvercharginSocket;

[HarmonyPatch(typeof(Building_MechCharger), nameof(Building_MechCharger.Tick))]
public static class Patch_MechCharger_Tick
{
	private const float DefaultChargePerTick = 0.00083333335f;
	public static void Prefix(Building_MechCharger __instance)
	{
		CompPowerLevel powerLevel = __instance.GetComp<CompPowerLevel>();

		if (__instance.IsHashIntervalTick(250))
		{
			if (__instance.currentlyChargingMech != null && __instance.Power.PowerOn)
				__instance.Power.PowerOutput = (0f - __instance.Power.Props.PowerConsumption) * powerLevel.PowerUsageMod;

			else
				__instance.Power.PowerOutput = 0f;
		}

		float chargePerTickMod = powerLevel.PowerLevel - 1f;
		if (chargePerTickMod != 0f && __instance.currentlyChargingMech != null && __instance.Power.PowerOn)
		{
			__instance.currentlyChargingMech.needs.energy.CurLevel += chargePerTickMod * DefaultChargePerTick;
			__instance.wasteProduced += __instance.WasteProducedPerTick * DefaultChargePerTick;
		}
	}
}