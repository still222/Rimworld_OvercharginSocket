using HarmonyLib;
using RimWorld;
using Verse;

namespace StkOvercharginSocket;

[HarmonyPatch(typeof(FlickUtility), nameof(FlickUtility.WantsToBeOn))]
public static class Patch_FlickUtility_WantsToBeOn
{
	static bool Prefix(Thing t, ref bool __result)
	{
		CompPowerLevel compPowerLevel = t.TryGetComp<CompPowerLevel>();
		if (compPowerLevel != null && !compPowerLevel.Overcharged)
		{
			__result = false;
			return false;
		}

		return true;
	}

}


