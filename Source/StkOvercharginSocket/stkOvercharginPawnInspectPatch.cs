using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace StkOvercharginSocket;

// Default inspect string of a chraging mech just displays 50f/100f
[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetInspectString))]
public static class Patch_Pawn_GetInspectString
{
	static readonly MethodInfo getCharging =
		AccessTools.Method(
			typeof(MechTechUtility),
			nameof(MechTechUtility.GetChargingPercentPerHour)
		);
	static readonly MethodInfo isChargingMethod =
		AccessTools.Method(
			typeof(RestUtility),
			nameof(RestUtility.IsCharging)
		);

	[HarmonyTranspiler]
	static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
	{
		var code = new List<CodeInstruction>(instructions);

		//Log.Message(string.Join("\n", code.Select((x, i) => $"{i}: {x}")));

		//if (this.IsCharging())
		//{
		//	  taggedString += " (+" + "PerDay".Translate((50f / maxLevel).ToStringPercent()) + ")";
		//}

		bool chargingInstr = false;
		for (int i = 0; i < code.Count; i++)
		{
			if (!chargingInstr)
			{
				if (code[i].opcode == OpCodes.Call &&
					(MethodInfo)code[i].operand == isChargingMethod)
						chargingInstr = true;

				continue;
			}

			if (code[i].opcode == OpCodes.Ldstr &&
				(string)code[i].operand == "PerDay")
			{
				code[i].operand = "PerHour";									//398: ldstr "PerDay"
				code[i + 1] = new CodeInstruction(OpCodes.Ldarg_0);				//399: ldc.r4 50
				code[i + 2] = new CodeInstruction(OpCodes.Call, getCharging);	//400: ldloc.s 13 (System.Single)
				code[i + 3] = new CodeInstruction(OpCodes.Nop);					//401: div NULL
				break;
			}

		}

		return code;
	}

}
