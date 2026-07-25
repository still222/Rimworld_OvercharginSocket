using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StkOvercharginSocket;

// Default inspect string of a chraging mech just displays 50f/100f
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
public static class Pawn_GetInspectString
{
	static readonly MethodInfo getCharging =
		AccessTools.Method(
			typeof(MechTechUtility),
			nameof(MechTechUtility.GetChargingPercentPerHour)
		);
	static readonly MethodInfo markerMethod =
		AccessTools.Method(
			typeof(RestUtility),
			nameof(RestUtility.IsCharging)
		);
	static readonly MethodInfo missMethod =
		AccessTools.Method(
			typeof(RestUtility),
			nameof(RestUtility.IsSelfShutdown)
		);

	[HarmonyTranspiler]
	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var code = new List<CodeInstruction>(instructions);

		//Log.Message(string.Join("\n", code.Select((x, i) => $"{i}: {x}")));

		//if (this.IsCharging())
		//	  taggedString += " (+" + "PerDay".Translate((50f / maxLevel).ToStringPercent()) + ")";		<===
		//else if (this.IsSelfShutdown())

		bool marker = false;
		for (int i = 0; i < code.Count; i++)
		{
			// Target block
			if (!marker && i + 4 < code.Count)
			{
				if (code[i].Calls(markerMethod))
						marker = true;

				continue;
			}

			// Transplier's body
			if (code[i].opcode == OpCodes.Ldstr &&
				(string)code[i].operand == "PerDay")
			{
				if (i + 3 >= code.Count ||
					code[i + 1].opcode != OpCodes.Ldc_R4 ||
					code[i + 2].opcode != OpCodes.Ldloc_S ||
					code[i + 3].opcode != OpCodes.Div)
				{
					Log.Warning("[StkOverchargin] Pawn_GetInspectString Transplier failed to find a correct sequence, most likely from the game version change. Aborting Patch.");
					break;
				}

				code[i].operand = "PerHour";									//398: ldstr "PerDay"
				code[i + 1] = new CodeInstruction(OpCodes.Ldarg_0);				//399: ldc.r4 50
				code[i + 2] = new CodeInstruction(OpCodes.Call, getCharging);	//400: ldloc.s 13 (System.Single)
				code[i + 3] = new CodeInstruction(OpCodes.Nop);					//401: div NULL
				break;
			}

			// Miss Block
			if (i + 4 >= code.Count || code[i].Calls(missMethod))
			{
				Log.Warning("[StkOverchargin] Pawn_GetInspectString Transplier failed to find a correct sequence, most likely from the game version change. Aborting Patch.");
				break;
			}

		}

		return code;
	}

}
