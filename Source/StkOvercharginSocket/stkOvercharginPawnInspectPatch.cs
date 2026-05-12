using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace StkOvercharginSocket;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
public static class Patch_Pawn_GetInspectString
// Default inspect string of a chraging mech just displays 50f/100f
{
	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var list = instructions.ToList();

		var getCharging = AccessTools.Method(
			typeof(MechTechUtility),
			nameof(MechTechUtility.GetChargingPercentPerHour)
		);

		bool replaced = false;
		bool replacedString = false;

		for (int i = 0; i < list.Count; i++)
		{
			// Match: ldc.r4 50
			// 50f is appeares only once
			if (!replaced &&
				i + 2 < list.Count &&
				list[i].opcode == OpCodes.Ldc_R4 &&
				(float)list[i].operand == 50f &&
				list[i + 2].opcode == OpCodes.Div)
			{
				// Skip: 50f, maxLevel, div
				i += 2;

				// Inject: pawn.GetChargingPercentPerHour()
				yield return new CodeInstruction(OpCodes.Ldarg_0);
				yield return new CodeInstruction(OpCodes.Call, getCharging);

				replaced = true;
				continue;
			}

			if (!replacedString &&
				list[i].opcode == OpCodes.Ldstr &&
				(string)list[i].operand == "PerDay")
			{
				yield return new CodeInstruction(OpCodes.Ldstr, "PerHour");
				replacedString = true;
				continue;
			}

			yield return list[i];

		}

	}

}
