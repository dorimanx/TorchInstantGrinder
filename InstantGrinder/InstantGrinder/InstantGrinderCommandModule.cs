﻿using System;
using System.Text;
using InstantGrinder.Reflections;
using NLog;
using Sandbox.Game;
using Sandbox.Game.World;
using Torch.API.Managers;
using Torch.Commands;
using Torch.Commands.Permissions;
using TorchUtils;
using VRage.Game.ModAPI;
using VRageMath;

namespace InstantGrinder
{
    [Category(Cmd_Category)]
    public sealed class InstantGrinderCommandModule : CommandModule
    {
        const string Cmd_Category = "grind";
        const string Cmd_GrindByName = "name";
        const string Cmd_Enable = "enable";
        const string Cmd_Disable = "disable";
        const string Cmd_Help = "help";
        static readonly string HelpSentence = $"See !{Cmd_Category} {Cmd_Help}.";

        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        InstantGrinderPlugin Plugin => (InstantGrinderPlugin) Context.Plugin;

        [Command(Cmd_Enable, "Enable plugin.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Enable() => this.CatchAndReport(() =>
        {
            Plugin.IsEnabled = true;
            Context.Respond("Enabled Instant Grinder plugin.");
        });

        [Command(Cmd_Disable, "Disable plugin.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Disable() => this.CatchAndReport(() =>
        {
            Plugin.IsEnabled = false;
            Context.Respond("Disabled Instant Grinder plugin.");
        });

        [Command(Cmd_GrindByName, "Grind a grid and transfer components to player's character inventory.")]
        [Permission(MyPromoteLevel.None)]
        public void GrindByName(string gridName) => this.CatchAndReport(() =>
        {
            var option = new GrindByNameCommandOption(Context.Args);
            Log.Info($"force: {option.Force}, as_player: {option.AsPlayer}");

            if (!Plugin.IsEnabled && Context.Player.PromoteLevel > MyPromoteLevel.None)
            {
                Context.Respond("Plugin is disabled.", Color.Red);
                return;
            }

            var player = Context.Player;
            if (player == null)
            {
                throw new Exception("Can only be called by a player");
            }

            if (!Plugin.TryGetGridGroupByName(gridName, out var gridGroup))
            {
                if (Plugin.TryGetPlayerByName(gridName, out var foundPlayer))
                {
                    var myPlayerName = Context.Player.DisplayName;
                    var msgBuilder = new StringBuilder();
                    msgBuilder.AppendLine("WARNING!!");
                    msgBuilder.AppendLine($"{myPlayerName} tried to grind you! xD");
                    msgBuilder.AppendLine("Grind them back with command:");
                    msgBuilder.AppendLine($">> !grind name \"{myPlayerName}\"");
                    SendMessageToPlayer(foundPlayer, Color.Red, msgBuilder.ToString());

                    Context.Respond($"You've sent a death threat to {gridName}.");
                    return;
                }

                Context.Respond($"Grid not found by name: \"{gridName}\". Try double quotes (\"foo bar\"). {HelpSentence}", Color.Yellow);
                return;
            }

            if (player.PromoteLevel == MyPromoteLevel.None || option.AsPlayer)
            {
                if (!player.OwnsAll(gridGroup))
                {
                    Context.Respond($"Grid found, but not yours: \"{gridName}\". You need to be a \"big owner\". {HelpSentence}", Color.Yellow);
                    return;
                }
            }

            // limit command inside a safe zone
            var safeZones = MySessionComponentSafeZones_SafeZones.Value;
            foreach (var safeZone in safeZones)
            foreach (var grid in gridGroup)
            {
                var isOutside = safeZone.IsOutside(grid);
                if (!isOutside) // Colliding with a safe zone
                {
                    Context.Respond($"Grid found, but in a safe zone: \"{gridName}\". You need to exit the safe zone. {HelpSentence}", Color.Yellow);
                    return;
                }
            }

            if (gridGroup.Length > 1 && !option.Force)
            {
                var msgBuilder = new StringBuilder();
                msgBuilder.AppendLine("Multiple grids found:");
                foreach (var grid in gridGroup)
                {
                    msgBuilder.AppendLine($" + {grid.DisplayName}");
                }

                msgBuilder.AppendLine();
                msgBuilder.AppendLine($"To proceed, type !{Cmd_Category} {Cmd_GrindByName} \"{gridName}\" {GrindByNameCommandOption.ForceOption}");
                Context.Respond(msgBuilder.ToString(), Color.Yellow);
                return;
            }

            Plugin.GridGridGroup((MyPlayer) player, gridGroup);

            Context.Respond($"Finished grinding: \"{gridName}\"", Color.White);
            var playerInventory = (MyInventory) player.Character.GetInventory();
            if ((playerInventory.CurrentMass > playerInventory.MaxMass ||
                playerInventory.CurrentVolume > playerInventory.MaxVolume) && !Plugin.GridInventoryIsEmpty)
            {
                Context.Respond("Your character inventory is full. Your Grid has some more items in cargos! When your character inventory is empty! run the grind command again.", Color.Yellow);
            }
            else if (Plugin.GridWasGrinded)
            {
                Context.Respond("Your character inventory is full. Store your items as soon as possible. Your grid is now in your backpack!", Color.Yellow);
            }
            else if (Plugin.GridInventoryIsEmpty && playerInventory.GetItemsCount() >= 20 && !Plugin.GridWasGrinded)
            {
                Context.Respond("Your character inventory needs to be empty before grinding your grid. Store your items in other cargo. then run the command again, when you are ready.", Color.Yellow);
            }
        });

        void SendMessageToPlayer(MyPlayer player, Color color, string message)
        {
            var chat = Plugin.Torch.Managers.GetManager<IChatManagerServer>();
            chat.SendMessageAsOther(null, message, color, player.SteamId());
        }

        [Command(Cmd_Help, "Show help.")]
        [Permission(MyPromoteLevel.None)]
        public void Help()
        {
            var msgBuilder = new StringBuilder();
            msgBuilder.AppendLine("Command list:");

            var commands = Context.Torch.GetPluginCommands(Cmd_Category, Context.Player?.PromoteLevel);
            foreach (var command in commands)
            {
                msgBuilder.AppendLine();
                msgBuilder.AppendLine($"{command.SyntaxHelp}");
                msgBuilder.AppendLine($" -- {command.HelpText}");
            }

            Context.Respond(msgBuilder.ToString());
        }
    }
}