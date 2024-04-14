using System.Collections.Generic;
using System.Linq;
using System.Text;
using InstantGrinder.Patches;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Utils.Torch;
using VRage.Game.ModAPI;
using VRageMath;

namespace InstantGrinder.Core
{
    public sealed class InstantGrinder
    {
        public interface IConfig
        {
            bool Enabled { get; }
            double MaxDistance { get; }
            int MaxItemCount { get; }
        }

        readonly IConfig _config;

        public InstantGrinder(IConfig config)
        {
            _config = config;
        }

        public void GrindGridByName(MyPlayer playerOrNull, string gridName, bool force, out CheckResult resultFinal, out string StatusFinal)
        {
            if (!_config.Enabled)
            {
                resultFinal = CheckResult.OFFLINE;
                StatusFinal = "";
                return;
            }

            if (!Utils.TryGetGridGroupByName(gridName, out var gridGroup))
            {
                resultFinal = CheckResult.GRID_NOT_FOUND;
                StatusFinal = gridName;
                return;
            }

            GrindGrids(playerOrNull, gridGroup, force, out CheckResult result, out string Status);

            resultFinal = result;
            StatusFinal = Status;
        }

        public void GrindGridSelected(MyPlayer playerOrNull, bool force, out CheckResult result, out string Status)
        {
            if (!_config.Enabled)
            {
                result = CheckResult.OFFLINE;
                Status = "";
                return;
            }

            if (playerOrNull == null)
            {
                result = CheckResult.NOPLAYER;
                Status = "";
                return;
            }

            if (!playerOrNull.TryGetSelectedGrid(out var grid))
            {
                result = CheckResult.GRID_NOT_FOUND;
                Status = "";
                return;
            }

            var gridGroup = MyCubeGridGroups.Static.Logical.GetGroup(grid);
            var grids = gridGroup.Nodes.Select(n => n.NodeData).ToArray();

            GrindGrids(playerOrNull, grids, force, out CheckResult result2, out string StatusInternal);

            result = result2;
            Status = StatusInternal;
        }

        void GrindGrids(IMyPlayer playerOrNull, IReadOnlyList<MyCubeGrid> gridGroup, bool force, out CheckResult result, out string StatusInternal)
        {
            var IsPlayerAdmin = playerOrNull.PromoteLevel == MyPromoteLevel.Admin;

            // don't let non-owners grind a grid
            if (!playerOrNull.OwnsAll(gridGroup))
            {
                if (!IsPlayerAdmin)
                {
                    result = CheckResult.OWNED_BY_DIFFERENT_PLAYER;
                    StatusInternal = "";
                    return;
                }
            }

            foreach (var grid in gridGroup)
            {
                // don't grind inside a safe zone (because it doesn't work)
                foreach (var safeZone in MySessionComponentSafeZones_SafeZones.Value)
                {
                    if (!safeZone.IsOutside(grid.PositionComp.GetPosition()))
                    {
                        result = CheckResult.INSAFEZONE;
                        StatusInternal = grid.DisplayName;
                        return;
                    }
                }

                // projector doesn't work either
                if (grid.Physics == null)
                {
                    result = CheckResult.PROJECTED;
                    StatusInternal = grid.DisplayName;
                    return;
                }

                // distance filter
                if (playerOrNull is MyPlayer p)
                {
                    var gridPosition = Utils.AvgPosition(gridGroup);
                    var playerPosition = p.GetPosition();
                    var distance = Vector3D.Distance(gridPosition, playerPosition);
                    if (distance > _config.MaxDistance)
                    {
                        result = CheckResult.TOOFAR;
                        StatusInternal = grid.DisplayName;
                        return;
                    }
                }
            }

            // don't grind multiple grids at once, unless specified
            if (gridGroup.Count > 1 && !force)
            {
                var msgBuilder = new StringBuilder();
                msgBuilder.AppendLine("Multiple grids found:");

                foreach (var grid in gridGroup)
                {
                    msgBuilder.AppendLine($" + {grid.DisplayName}");
                }

                result = CheckResult.TOO_MANY_GRIDS;
                StatusInternal = msgBuilder.ToString();
                return;
            }

            // don't grind too many items, unless specified
            var itemCount = Utils.GetItemCount(gridGroup);

            if (itemCount > _config.MaxItemCount && !force)
            {
                var msgBuilder = new StringBuilder();
                msgBuilder.AppendLine($"Too many items: {itemCount}");

                result = CheckResult.TOOMANYITEMS;
                StatusInternal = msgBuilder.ToString();
                return;
            }

            var msgBuilderFinal = new StringBuilder();

            foreach (var grid in gridGroup)
            {
                msgBuilderFinal.AppendLine($" + {grid.DisplayName}");
            }

            if (playerOrNull is MyPlayer player)
            {
                GrindGridsIntoPlayerInventory(gridGroup, player);
            }
            else // nobody will receive the items
            {
                GrindGrids(gridGroup);
            }

            result = CheckResult.OK;
            StatusInternal = msgBuilderFinal.ToString();
        }

        void GrindGrids(IEnumerable<MyCubeGrid> gridGroup)
        {
            foreach (var block in gridGroup.SelectMany(g => g.CubeBlocks))
            {
                Utils.GrindBlock(block);
            }
        }

        void GrindGridsIntoPlayerInventory(IEnumerable<MyCubeGrid> gridGroup, MyPlayer player)
        {
            var playerInventory = player.Character.GetInventory();
            var blocks = gridGroup.SelectMany(g => g.CubeBlocks).ToArray();

            // save inventory items first
            foreach (var block in blocks)
            {
                if (block.FatBlock == null) continue;
                Utils.CopyItemsIntoInventory(block.FatBlock, playerInventory);
            }

            playerInventory.Refresh();

            // then grind them down
            foreach (var block in blocks)
            {
                Utils.GrindBlockIntoInventory(block, playerInventory);
            }

            playerInventory.Refresh();
        }
    }
}