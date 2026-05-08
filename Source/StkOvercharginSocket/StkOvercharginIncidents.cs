using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace StkOvercharginSocket;

public static class OvercharginIncidentUtility
{
	public static IEnumerable<Building> GetShortCircuitableChargers(Map map, bool onlyWithMech = true)
	// Original: RimWorld.ShortCircuitUtility.GetShortCircuitablePowerConduits
	// Hopefuly we can reuse some vanilla methods for chargers. For now this list is completely included into the short circut event
	{
		foreach (Building_MechCharger charger in map.listerBuildings.AllBuildingsColonistOfClass<Building_MechCharger>())
		{
			CompPowerLevel powerLevelComp = charger.GetComp<CompPowerLevel>();

			if (charger.Power == null || powerLevelComp == null || !powerLevelComp.Overchargable || !charger.Power.PowerOn)
				continue;

			if (onlyWithMech && charger.currentlyChargingMech == null)
				continue;

			if (powerLevelComp.critOverchargeSet && charger.Faction == Faction.OfPlayer)
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

public class Alert_HaveOverchargers : Alert
{
	private readonly List<Thing> targets = [];
	public Alert_HaveOverchargers()
	{
		defaultLabel = "stkHaveOverchargedRechargers".Translate();
		defaultExplanation = "stkHaveOverchargedRechargersDesc".Translate();
		defaultPriority = AlertPriority.Medium;
	}

	public override AlertReport GetReport()
	{
		targets.Clear();
		foreach (Map map in Find.Maps)
			foreach (var c in OvercharginIncidentUtility.GetShortCircuitableChargers(map, false))
				targets.Add(c);

		return AlertReport.CulpritsAre(targets);
	}

}

