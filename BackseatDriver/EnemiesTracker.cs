using BackseatDriver;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackseatDriver
{
    public delegate void onEnemyInfoChanged();
    public delegate void onCasting(string enemyName, ulong enemyId, uint castId);

    public sealed record EnemyInfo
    {
        public string Name { get; init; } = string.Empty;
        public ulong DataId { get; init; }
        public uint EntityId { get; init; }
        public uint castId { get; set; }
    }

    public class EnemiesTracker
    {
        private Plugin Plugin;
        private Configuration Configuration;

        private int lastScanTS;

        public List<EnemyInfo> currEnemies { get; private set; } = new();

        private onEnemyInfoChanged onEnemyInfoChanged;
        private onCasting onCasting;

        public EnemiesTracker(Plugin plugin, Configuration config)
        {
            Plugin = plugin;
            Configuration = config;

            lastScanTS = 0;

            onEnemyInfoChanged = onEnemiesChanged;
            onCasting = onCast;
        }

        private void onCast(string enemyName, ulong enemyId, uint castId)
        {
            return;
        }
        private void onEnemiesChanged()
        {
            return;
        }

        public void registerOnChangeCb(onEnemyInfoChanged cb)
        {
            onEnemyInfoChanged += cb;
        }
        public void registerOnCastCb(onCasting cb)
        {
            onCasting += cb;
        }
        public void scan()
        {
            var elapsed = Environment.TickCount - lastScanTS;
            if (elapsed < 500)
            {
                return;
            }

            lastScanTS = Environment.TickCount;

            var hostiles = Plugin.Objects
                .Where(o => o is IBattleNpc bnpc
                && o.ObjectKind == ObjectKind.BattleNpc
                && bnpc.BattleNpcKind == BattleNpcSubKind.Enemy
                && o.IsTargetable)
                .Cast<IBattleNpc>()
                .ToArray();

            if (hostiles.Length == 0)
            {
                return;
            }

            // sort by HP
            var sortedHostiles = hostiles.OrderBy(o => o.CurrentHp).Take(5).ToList();

            var somethingChanged = sortedHostiles.Count != currEnemies.Count;

            for (int i = 0; i < sortedHostiles.Count && !somethingChanged; i++)
            {
                somethingChanged = sortedHostiles[i].EntityId != currEnemies[i].EntityId;
            }

            if (somethingChanged)
            {
                currEnemies.Clear();
                foreach (var hostile in sortedHostiles)
                {
                    currEnemies.Add(new EnemyInfo { Name = hostile.Name.TextValue, DataId = hostile.DataId, EntityId = hostile.EntityId, castId = hostile.CastActionId });
                }

                onEnemyInfoChanged();

                /*Plugin.Log.Info($"New Hostiles: {hostiles.Length}");
                foreach (var hostile in hostiles)
                {
                    Plugin.Log.Info($"Hostile {hostile.Name.TextValue} - DID {hostile.DataId} - EID {hostile.EntityId}");
                }
                foreach (var curr in currEnemies)
                {
                    Plugin.Log.Info($"CurrHostile {curr.Name} - DID {curr.DataId} - EID {curr.EntityId}");
                }*/
            }

            for (int i = 0; i < sortedHostiles.Count; i++)
            {
                if (sortedHostiles[i].CastActionId != currEnemies[i].castId)
                {
                    currEnemies[i].castId = sortedHostiles[i].CastActionId;
                    if (currEnemies[i].castId != 0)
                    {
                        onCasting(currEnemies[i].Name, currEnemies[i].DataId, currEnemies[i].castId);
                    }
                }
            }
        }
    }
}
