using BackseatDriver;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Common.Math;

using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace BackseatDriver
{
    public delegate void onEnemyInfoChanged();
    public delegate void onCasting(string enemyName, ulong enemyId, uint castId);

    public delegate void onActionEffect(ulong enemyEntityId, uint actionId);
    public unsafe sealed class ActionEffectReceiveHook : IDisposable
    {
        private Plugin plugin;
        private readonly IObjectTable objects;
        private readonly Hook<ReceiveDelegate> hook;

        public event onActionEffect? OnActionEffect;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ReceiveDelegate(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId* targetEntityIds);

        public ActionEffectReceiveHook(IGameInteropProvider interop, IObjectTable objects, Plugin plugin)
        {
            this.objects = objects;

            nint addr = (nint)ActionEffectHandler.MemberFunctionPointers.Receive;
            hook = interop.HookFromAddress<ReceiveDelegate>(addr, Detour);

            hook.Enable();
            this.plugin = plugin;
        }

        private void Detour(uint casterEntityId,
                            Character* casterPtr,
                            Vector3* targetPos,
                            ActionEffectHandler.Header* header,
                            ActionEffectHandler.TargetEffects* effects,
                            FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId* targetEntityIds)
        {
            hook.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);

            uint actionId = header != null ? header->ActionId : 0;
            OnActionEffect?.Invoke(casterEntityId, actionId);
        }

        public void Dispose()
        {
            hook.Dispose();
        }
    }

    public sealed record EntityCastState
    {
        public ulong entityId { get; set; }
        public uint actionId { get; set; }
    }

    public unsafe sealed class CastWatcher
    {
        private Dictionary<ulong, EntityCastState> currWatchedEntities = new();
        Plugin plugin;

        public event onActionEffect? OnActionEffect;

        public CastWatcher(Plugin plugin)
        {
            this.plugin = plugin;
        }

        public void setWatchedEntities(List<ulong> watchedEntities)
        {
            currWatchedEntities.Clear();
            foreach (ulong entityId in watchedEntities)
            {
                currWatchedEntities.Add(entityId, new EntityCastState { entityId = entityId, actionId = 0 });
            }
        }

        public void watch()
        {
            foreach (var o in Plugin.Objects)
            {
                if (o != null && currWatchedEntities.ContainsKey(o.EntityId))
                {
                    IGameObject? go = o;
                    if (go.Address == nint.Zero)
                    {
                        continue;
                    }

                    var battleChar = (BattleChara*)go.Address;
                    var castInfo = battleChar->Character.GetCastInfo();

                    if (castInfo == null)
                    {
                        continue;
                    }

                    if (!currWatchedEntities.TryGetValue(o.EntityId, out var entityState))
                    {
                        continue;
                    }

                    if (castInfo->ActionId != entityState.actionId)
                    {
                        OnActionEffect?.Invoke(entityState.entityId, castInfo->ActionId);
                        entityState.actionId = castInfo->ActionId;
                    }
                }
            }
        }
    }

        public sealed record EnemyInfo
    {
        public string Name { get; init; } = string.Empty;
        public ulong DataId { get; init; }
        public uint EntityId { get; init; }
        public uint castId { get; set; }
    }

    public class EnemiesTracker : IDisposable
    {
        private Plugin plugin;
        private Configuration config;
        private ActionEffectReceiveHook? actionEffectReceiveHook;
        private CastWatcher? castWatcher;

        private int lastScanTS;

        public List<EnemyInfo> currEnemies { get; private set; } = new();

        private event onEnemyInfoChanged? OnEnemyInfoChanged;
        private event onCasting? OnCasting;

        public EnemiesTracker(Plugin plugin, Configuration config)
        {
            this.plugin = plugin;
            this.config = config;
            actionEffectReceiveHook = new ActionEffectReceiveHook(Plugin.Interop, Plugin.Objects, this.plugin);
            castWatcher = new CastWatcher(this.plugin);

            lastScanTS = 0;

            actionEffectReceiveHook.OnActionEffect += onActionEffect;
            castWatcher.OnActionEffect += onActionEffect;
        }

        public void Dispose() => actionEffectReceiveHook?.Dispose();

        public void registerOnChangeCb(onEnemyInfoChanged cb)
        {
            OnEnemyInfoChanged += cb;
        }
        public void registerOnCastCb(onCasting cb)
        {
            OnCasting += cb;
        }
        private void onActionEffect(ulong enemyEntityId, uint actionId)
        {
            foreach (var enemy in currEnemies)
            {
                if (enemy.EntityId == enemyEntityId)
                {
                    // since we get the action event from both the actionevent packets and monitoring frames the timing can differ
                    // e.g. silencing attacks will be picked up by the frames as the cast starts, and after it actually fires then we get the actioneventpacket
                    if (enemy.castId != actionId)
                    {
                        enemy.castId = actionId;
                        if (actionId != 0) OnCasting?.Invoke(enemy.Name, enemy.DataId, enemy.castId);
                    }
                    
                    break;
                }
            }
        }
        public void scan()
        {
            var elapsed = Environment.TickCount - lastScanTS;

            if (Math.Abs(elapsed) < 250)
            {
                return;
            }

            lastScanTS = Environment.TickCount;

            var hostiles = Plugin.Objects
                .Where(o => o is IBattleNpc bnpc
                && o.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc
                && bnpc.BattleNpcKind == Dalamud.Game.ClientState.Objects.Enums.BattleNpcSubKind.Enemy
                && o.IsTargetable)
                .Cast<IBattleNpc>()
                .ToArray();

            if (hostiles.Length == 0)
            {
                return;
            }

            // sort by HP
            var sortedHostiles = hostiles.OrderByDescending(o => o.MaxHp).Take(5).ToList();

            var somethingChanged = sortedHostiles.Count != currEnemies.Count;

            for (int i = 0; i < sortedHostiles.Count && !somethingChanged; i++)
            {
                somethingChanged = sortedHostiles[i].EntityId != currEnemies[i].EntityId;
            }

            if (somethingChanged)
            {
                currEnemies.Clear();
                List<ulong> entityIds = new List<ulong>();

                foreach (var hostile in sortedHostiles)
                {
                    currEnemies.Add(new EnemyInfo { Name = hostile.Name.TextValue, DataId = hostile.DataId, EntityId = hostile.EntityId, castId = hostile.CastActionId });
                    entityIds.Add(hostile.EntityId);
                }

                castWatcher?.setWatchedEntities(entityIds);
                OnEnemyInfoChanged?.Invoke();
            }

            castWatcher?.watch();
        }
    }
}
