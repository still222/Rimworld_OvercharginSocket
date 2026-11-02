using System.Reflection;
using HarmonyLib;
using Multiplayer.API;
using Verse;

namespace StkOvercharginSocket;

[StaticConstructorOnStartup]
public static class Startup
{
	static Startup()
	{
		var harmony = new Harmony("stk.overcharged.socket");
		harmony.PatchAll(Assembly.GetExecutingAssembly());

		if (MP.enabled)
			MP.RegisterAll();
	}

}
