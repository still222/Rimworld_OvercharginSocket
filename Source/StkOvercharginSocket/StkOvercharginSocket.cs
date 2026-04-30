using Multiplayer.API;
using Verse;

namespace StkOvercharginSocket;

[StaticConstructorOnStartup]
public static class Startup
{
	static Startup()
	{
		//var harmony = new Harmony("stk.overcharged.socket");
		//harmony.PatchAll();

		if (MP.enabled)
			MP.RegisterSyncMethod(typeof(CompPowerLevel), nameof(CompPowerLevel.SetPowerLevel));
	}
}