using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StkOvercharginSocket;

public static class OvercharginIncidentUtility
{
	public static IEnumerable<Building> GetShortCircuitableChargers(Map map, bool onlyWithMech = true)
	// Original: RimWorld.ShortCircuitUtility.GetShortCircuitablePowerConduits
	{
		foreach (Building_MechCharger charger in map.listerBuildings.AllBuildingsColonistOfClass<Building_MechCharger>())
		{
			CompPowerLevel powerLevelComp = charger.GetComp<CompPowerLevel>();

			if (charger.Power == null || powerLevelComp == null || !powerLevelComp.Overchargable || !charger.Power.PowerOn)
				continue;

			if (onlyWithMech && charger.currentlyChargingMech == null)
				continue;

			if (powerLevelComp.Overcharged && powerLevelComp.powerLevel > 1 && charger.Faction == Faction.OfPlayer)
				yield return charger;

		}

	}

	// Incidents themselves check it already, but I need it for alerts
	public static bool IncidentDisabledByScenario(IncidentDef def)
	{
		foreach (var part in Find.Scenario.parts)
			if (part is ScenPart_DisableIncident disable && disable.Incident == def)
				return true;

		return false;
	}

}

[HarmonyPatch(typeof(ShortCircuitUtility), nameof(ShortCircuitUtility.GetShortCircuitablePowerConduits))]
public static class Patch_GetShortCircuitablePowerConduits
{
	[HarmonyPostfix]
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
	private bool? incidentDisabled;
	private bool IncidentDisabled => incidentDisabled ??= OvercharginIncidentUtility.IncidentDisabledByScenario(StkDefOf.ShortCircuit);

	public Alert_HaveOverchargers()
	{
		defaultLabel = "stkHaveOverchargedRechargers".Translate();
		defaultExplanation = "stkHaveOverchargedRechargersDesc".Translate();
		defaultPriority = AlertPriority.Medium;
	}

	public override AlertReport GetReport()
	{
		targets.Clear();

		if (IncidentDisabled)
			return AlertReport.Inactive;

		foreach (Map map in Find.Maps)
			foreach (var c in OvercharginIncidentUtility.GetShortCircuitableChargers(map, false))
				targets.Add(c);

		return AlertReport.CulpritsAre(targets);
	}

}

