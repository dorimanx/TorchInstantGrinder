using NLog;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using Utils.Torch;
using VRage.Game.ModAPI;

namespace InstantGrinder
{
    [Category("grind")]
    public sealed class InstantGrinderCommandModule : CommandModule
    {
        //static readonly Logger Log = LogManager.GetCurrentClassLogger();
        InstantGrinderPlugin Plugin => (InstantGrinderPlugin) Context.Plugin;
        InstantGrinderConfig Config => Plugin.Config;
        Core.InstantGrinder Grinder => Plugin.Grinder;

        [Command("configs", "List of configs.")]
        [Permission(MyPromoteLevel.None)]
        public void Configs() => this.GetOrSetProperty(Config);

        [Command("commands", "List of commands.")]
        [Permission(MyPromoteLevel.None)]
        public void Commands() => this.ShowCommands();

        [Command("name", "Grind a grid by name.")]
        [Permission(MyPromoteLevel.None)]
        public void GrindByName(string gridName, bool force = false) => this.CatchAndReport(() =>
        {
            Grinder.GrindGridByName(Context.Player as MyPlayer, gridName, force, out CheckResult result, out string status);

            if (result != CheckResult.OK)
            {
                if (status != "")
                {
                    WriteResponse(result);
                    Context.Respond($"{status}");
                }
                else
                    WriteResponse(result);

                return;
            }

            Context.Respond($"Finished grinding grid: {gridName}");
        });

        [Command("this", "Grind a grid that the player is looking at or seated on.")]
        [Permission(MyPromoteLevel.None)]
        public void GrindThis(bool force = false) => this.CatchAndReport(() =>
        {
            Grinder.GrindGridSelected(Context.Player as MyPlayer, force, out CheckResult result, out string status);

            if (result != CheckResult.OK)
            {
                if (status != "")
                {
                    WriteResponse(result);
                    Context.Respond($"{status}");
                }
                else
                    WriteResponse(result);

                return;
            }

            Context.Respond($"Finished grinding grid {status}");
        });

        private void WriteResponse(CheckResult result)
        {
            switch (result)
            {
                case CheckResult.TOO_MANY_GRIDS:
                    Context.Respond("Found multiple Grids, add true to command to force grind.");
                    break;

                case CheckResult.OWNED_BY_DIFFERENT_PLAYER:
                    Context.Respond("Grid seems to be owned by a different player.\nOr you dont own connected grid!");
                    break;

                case CheckResult.GRID_NOT_FOUND:
                    Context.Respond("Grid not found");
                    break;

                case CheckResult.OFFLINE:
                    Context.Respond("Plugin is offline");
                    break;
                    
                case CheckResult.NOPLAYER:
                    Context.Respond("Player must be in game!");
                    break;

                case CheckResult.INSAFEZONE:
                    Context.Respond("Grid in SafeZone!");
                    break;

                case CheckResult.PROJECTED:
                    Context.Respond("Cant grind Projected Grid! turn off/remove projectors.");
                    break;

                case CheckResult.TOOFAR:
                    Context.Respond("Grid is too far from you!");
                    break;

                case CheckResult.TOOMANYITEMS:
                    Context.Respond("Too many items in cargo, remove them");
                    break;
            }
        }
    }
}