using RimWorld;
using Verse;

namespace StkOvercharginSocket;

[DefOf]
public static class ResearchDefOf
{
	//public static ResearchProjectDef BasicMechtech;
	public static ResearchProjectDef StandardMechtech;
	public static ResearchProjectDef HighMechtech;
	public static ResearchProjectDef UltraMechtech;

}

public static class MechTechUtility
{
	public static int GetLevel()
	{
		if (ResearchDefOf.UltraMechtech.IsFinished) return 4;
		if (ResearchDefOf.HighMechtech.IsFinished) return 3;
		if (ResearchDefOf.StandardMechtech.IsFinished) return 2;
		else return 1;
	}

}
