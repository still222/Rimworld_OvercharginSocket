using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StkOvercharginSocket;

public static class OvercharginIncidentUtility
{
	public static IEnumerable<Building> GetShortCircuitableChargers(Map map)
	// Original: RimWorld.ShortCircuitUtility.GetShortCircuitablePowerConduits
	// Hopefuly we can reuse some vanilla methods for chargers. For now this list is completely included into the short circut event
	{
		foreach (Building_MechCharger charger in map.listerBuildings.AllBuildingsColonistOfClass<Building_MechCharger>())
		{
			CompPowerLevel powerLevelComp = charger.GetComp<CompPowerLevel>();

			if (charger.Power == null || powerLevelComp == null || !powerLevelComp.Overchargable)
				continue;

			if (!charger.Power.PowerOn || !powerLevelComp.Overcharged || charger.currentlyChargingMech == null)
				continue;

			if (powerLevelComp.PowerLevel > powerLevelComp.Props.powerLevels)
				yield return charger;

		}

	}

}

[HarmonyPatch(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.GetShortCircuitablePowerConduits))]
public static class Patch_GetShortCircuitablePowerConduits
{
	public static IEnumerable<Building> Postfix(IEnumerable<Building> values, Map map)
	{
		foreach (Building building in values)
			yield return building;

		foreach (Building building in OvercharginIncidentUtility.GetShortCircuitableChargers(map))
			yield return building;

	}

}
