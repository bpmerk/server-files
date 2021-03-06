using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using ProtoBuf;

/**********************************************************************
*
*   2.1.0   -   Npc displaynames will now be propperly used
*           -   Npc without names will use their translated prefab name or custom name
*           -   Fixed -1 notification when blown up
*           -   When blown up will show what explosive is used
*           -   Removed old Blownup sequence and using the regular kill messages,
*               since the weaponcheck check is updated with explosives
*           -   Added Animal kills to the feed (by example of crazyR17)
*   2.1.1   -   Removed obsolete Blown messages from language file
*           -   Reverted distance check
*           -   Hotfix
*           
***********************************************************************/
namespace Oxide.Plugins
{
    [Info("Simple Kill Feed", "Krungh Crow", "2.1.1")]
    [Description("A simple kill feed, that displays in the top right corner various kill events.")]
    public class SimpleKillFeed : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin Clans;
        private bool _isClansReborn;
        private bool _isClansIo;
        private readonly Dictionary<uint, string> _itemNameMapping = new Dictionary<uint, string>();
        private GameObject _holder;
        private KillQueue _killQueue;
        private static SKFConfig _config;
        private static SimpleKillFeedData _data;

        #endregion

        #region Config

        private class SKFConfig
        {
            [JsonProperty("Show Traps and Entitys in Kill Feed")]
            public bool EnableEntityFeed;
            [JsonProperty("Show Animals in Kill Feed")]
            public bool EnableAnimalFeed;
            [JsonProperty("Show Npcs in Kill Feed")]
            public bool EnableNpcFeed;
            [JsonProperty("Show suicides in KillFeed (Default: true)")]
            public bool EnableSuicides;
            [JsonProperty("Show radiation kills in KillFeed (Default: true)")]
            public bool EnableRadiationKills;
            [JsonProperty("Chat Icon Id (Steam profile ID)")]
            public ulong IconId;
            [JsonProperty("Max messages in feed (Default: 5)")]
            public int MaxFeedMessages;
            [JsonProperty("Max player name length in feed (Default: 18)")]
            public int MaxPlayerNameLength;
            [JsonProperty("Feed message TTL in seconds (Default: 7)")]
            public int FeedMessageTtlSec;
            [JsonProperty("Allow kill messages in chat along with kill feed")]
            public bool EnableChatFeed;
            [JsonProperty("Log PvP Kill events")]
            public bool EnableLogging;
            [JsonProperty("Height ident (space between messages). Default: 0.0185")]
            public float HeightIdent;
            [JsonProperty("Feed Position - Anchor Max. (Default: 0.995 0.986")]
            public string AnchorMax;
            [JsonProperty("Feed Position - Anchor Min. (Default: 0.723 0.964")]
            public string AnchorMin;
            [JsonProperty("Font size of kill feed (Default: 12)")]
            public int FontSize;
            [JsonProperty("Outline Text Size (Default: 0.5 0.5)")]
            public string OutlineSize;
            [JsonProperty("Default color for distance (if too far from any from the list). Default: #FF8000")]
            public string DefaultDistanceColor;
            [JsonProperty("Distance Colors List (Certain color will apply if distance is <= than specified)")]
            public DistanceColor[] DistanceColors;
            [JsonProperty("Custom Entity Names, you can remove or add more!")]
            public Dictionary<string, string> Ents = new Dictionary<string, string>();
            [JsonProperty("Custom Animal Names, you can remove or add more!")]
            public Dictionary<string, string> Animal = new Dictionary<string, string>();
            [JsonProperty("Custom Weapon Names, you can add more!")]
            public Dictionary<string, string> Weapons = new Dictionary<string, string>();
            [JsonProperty("Custom Npc Names, you can add more!")]
            public Dictionary<string, string> Npcs = new Dictionary<string, string>();

            [OnDeserialized]
            internal void OnDeserialized(StreamingContext ctx) => Array.Sort(DistanceColors, (o1, o2) => o1.DistanceThreshold.CompareTo(o2.DistanceThreshold));

            public class DistanceColor
            {
                public int DistanceThreshold;
                public string Color;
                public bool TestDistance(int distance) => distance <= DistanceThreshold;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<SKFConfig>();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new SKFConfig
            {
                EnableEntityFeed = true,
                EnableAnimalFeed = true,
                EnableNpcFeed = true,
                EnableSuicides = true,
                EnableRadiationKills = true,
                IconId = 76561197960839785UL,
                MaxFeedMessages = 5,
                MaxPlayerNameLength = 18,
                FeedMessageTtlSec = 7,
                EnableChatFeed = true,
                EnableLogging = false,
                HeightIdent = 0.0185f,
                AnchorMax = "0.995 0.986",
                AnchorMin = "0.723 0.964",
                FontSize = 12,
                OutlineSize = "0.5 0.5",
                DefaultDistanceColor = "#FF8000",
                DistanceColors = new[]
                {
                    new SKFConfig.DistanceColor
                    {
                        Color = "#FFFFFF",
                        DistanceThreshold = 50
                    },
                    new SKFConfig.DistanceColor
                    {
                        Color = "#91D6FF",
                        DistanceThreshold = 100
                    },
                    new SKFConfig.DistanceColor
                    {
                        Color = "#FFFF00",
                        DistanceThreshold = 150
                    }
                },
                Ents = new Dictionary<string, string>()
                {
                    { "autoturret_deployed","Auto Turret" },
                    { "flameturret.deployed","Flame Turret"},
                    { "guntrap.deployed","Gun Trap"},
                    { "landmine","Landmine"},
                    { "beartrap","Bear Trap"},
                    { "sam_site_turret_deployed","Sam Site Turret"},
                    { "patrolhelicopter","Helicopter"},
                    { "bradleyapc","Bradley APC"}
                },
                Animal = new Dictionary<string, string>()
                {
                    { "bear","Bear" },
                    { "polarbear","PolarBear" },
                    { "wolf","Wolf" },
                    { "stag","Stag"},
                    { "boar","Boar" },
                    { "chicken","Chicken" },
                    { "horse","Horse"},
                    { "simpleshark","Shark" }
                },
                Weapons = new Dictionary<string, string>()
                {
                    { "Assault Rifle","Ak-47" },
                    { "LR-300 Assault Rifle","LR-300" },
                    { "L96 Rifle","L96" },
                    { "Bolt Action Rifle","Bolt" },
                    { "Semi-Automatic Rifle","Semi-AR" },
                    { "Semi-Automatic Pistol","Semi-AP" },
                    { "Spas-12 Shotgun","Spas-12" },
                    { "M92 Pistol","M92" }
                },
                Npcs = new Dictionary<string, string>()
                {
                    { "scientist","Scientist Npc" }
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Data (ProtoBuf)

        [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
        private class SimpleKillFeedData
        {
            public HashSet<ulong> DisabledUsers = new HashSet<ulong>();
        }

        private void LoadData()
        {
            _data = ProtoStorage.Load<SimpleKillFeedData>(nameof(SimpleKillFeed)) ?? new SimpleKillFeedData();
        }

        private void SaveData()
        {
            if (_data == null) return;
            ProtoStorage.Save(_data, nameof(SimpleKillFeed));
        }

        #endregion

        #region ChatCommand

        [ChatCommand("feed")]
        private void ToggleFeed(BasePlayer player)
        {
            if (!_data.DisabledUsers.Contains(player.userID))
            {
                _data.DisabledUsers.Add(player.userID);
                Player.Message(player, _("Disabled", player), null, _config.IconId);
            }
            else
            {
                _data.DisabledUsers.Remove(player.userID);
                Player.Message(player, _("Enabled", player), null, _config.IconId);
            }
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _isClansReborn = Clans != null && Clans.Author.Contains("k1lly0u");
            _isClansIo = Clans != null && Clans.Author.Contains("playrust.io / dcode");
            foreach (var blueprint in ItemManager.bpList.Where(bp => bp.targetItem.category == ItemCategory.Weapon || bp.targetItem.category == ItemCategory.Tool))
            {
                var md = blueprint.targetItem.GetComponent<ItemModEntity>();
                if (!md)
                    continue;
                if (!_itemNameMapping.ContainsKey(md.entityPrefab.resourceID))
                    _itemNameMapping.Add(md.entityPrefab.resourceID, blueprint.targetItem.displayName.english);
            }
            _holder = new GameObject("SKFHolder");
            UnityEngine.Object.DontDestroyOnLoad(_holder);
            _killQueue = _holder.AddComponent<KillQueue>();
            Pool.FillBuffer<KillEvent>(_config.MaxFeedMessages);
        }

        private void Init() => LoadData();

        private void Unload()
        {
            _killQueue = null;
            UnityEngine.Object.Destroy(_holder);
            _holder = null;
            for (var i = 0; i < _config.MaxFeedMessages; i++)
                KillQueue.RemoveKillCui($"kf-{i}");
            _config = null;
            Pool.directory.Remove(typeof(KillEvent));
            SaveData();
            _data = null;
        }

        private void OnEntityDeath(BaseCombatEntity victim, HitInfo hitInfo)
        {
            if (victim == null || victim is BasePlayer || !IsAnimal(victim)) return;

            BasePlayer attacker = hitInfo.InitiatorPlayer;

            if (attacker == null || attacker.IsNpc || IsAnimal(attacker)) return;
            if (!_config.EnableAnimalFeed) return;

            string VictimName = _config.Animal[victim.ShortPrefabName];
            string AttackerName = SanitizeName(GetClan(attacker) + attacker.displayName);
            string WeaponName = GetCustomWeaponName(hitInfo);
            var distance = (int)Vector3.Distance(attacker.transform.position, victim.transform.position);
            _killQueue.OnDeath(attacker, null, string.Format(_("MsgFeedKillAnimalFromPlayer"), AttackerName, VictimName, WeaponName, GetDistanceColor(distance), distance));
        }

        private void OnPlayerDeath(BasePlayer victim, HitInfo hitInfo)
        {
            if (victim == null) return;
            if (hitInfo == null)
            {
                var wAttacker = victim.lastAttacker?.ToPlayer();
                if (wAttacker != null && victim.IsWounded())
                {
                    OnDeathFromWounds(wAttacker, victim);
                }
                return;
            }

            if (IsTrap(hitInfo.Initiator))
            {
                if (!_config.EnableEntityFeed) return;
                OnKilledByEnt(hitInfo.Initiator, victim); return;
            }

            if (IsAnimal(hitInfo.Initiator))
            {
                if (!_config.EnableAnimalFeed) return;
                OnKilledByAnimal(hitInfo.Initiator, victim); return;
            }

            if (IsRadiation(hitInfo))
            {
                if (!_config.EnableRadiationKills) return;
                OnKilledByRadiation(victim); return;
            }

            var distance = !hitInfo.IsProjectile() ? (int)Vector3.Distance(hitInfo.PointStart, hitInfo.HitPositionWorld) : (int)hitInfo.ProjectileDistance;

            var attacker = hitInfo.InitiatorPlayer;

            if (attacker == null) return;

            if (attacker == victim)
            {
                if (!_config.EnableSuicides)
                    return;
                OnSuicide(victim); return;
            }

            else if (IsFlame(hitInfo))
            {
                OnBurned(attacker, victim);
            }

            else
            {
                OnKilled(attacker, victim, hitInfo, distance);
            }
        }

        #endregion

        #region Oxide Lang

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>
        {
            {"MsgAttacker", "You killed <color=#ff686b>{0}</color> from {1}m in <color=#ff686b>{2}</color>."},
            {"MsgVictim", "<color=#ff686b>{0}</color> killed you from {1}m with their {2} to <color=#ff686b>{3}</color>."},
            {"MsgFeedKill", "<color=#00ff00>{0}</color> killed <color=#ff686b>{1}</color>, <color=#ff686b>{2}</color>, <color=#ff686b>{3}</color><color={4}>({5}m)</color>"},
            {"MsgFeedKillNpc", "<color=#00ff00>{0}</color> killed <color=#ff686b>{1}</color>, <color={2}>({3}m)</color>"},
            {"MsgFeedKillAnimalFromPlayer", "<color=#00ff00>{0}</color> killed a <color=#ff686b>{1}</color>, <color=#ff686b>{2}</color>, <color={3}>({4}m)</color>"},

            {"MsgAtkWounded", "You wounded <color=#ff686b>{0}</color> till death."},
            {"MsgVictimWounded", "<color=#ff686b>{0}</color> has wounded you till death."},
            {"MsgFeedWounded", "<color=#00ff00>{0}</color> finished <color=#ff686b>{1}</color>"},

            {"MsgAtkBurned", "You burned <color=#ff686b>{0}</color> alive!"},
            {"MsgVictimBurned", "<color=#ff686b>{0}</color> has burned you alive!"},
            {"MsgFeedBurned", "<color=#00ff00>{0}</color> burned <color=#ff686b>{1}</color>!"},

            {"MsgFeedKillEnt", "<color=#ff686b>{0}</color> was killed by <color=orange>{1}</color>"},
            {"MsgFeedKillAnimal", "<color=#ff686b>{0}</color> was killed by <color=orange>{1}</color>"},

            {"MsgFeedKillSuicide", "<color=#ff686b>{0}</color> committed <color=orange>Suicide</color>"},
            {"MsgFeedKillRadiation", "<color=#ff686b>{0}</color> died to <color=orange>Radiation</color>"},

            {"Enabled", "KillFeed Enabled"},
            {"Disabled", "KillFeed Disabled"}
        }, this);

        #endregion

        #region Kill Events

        private void OnKilled(BasePlayer attacker, BasePlayer victim, HitInfo hitInfo, int dist)
        {
            var HitBone = hitInfo.boneArea.ToString();
            if (HitBone == "-1") HitBone = "Body";
            if (attacker.IsNpc)
            {
                if (!_config.EnableNpcFeed) return;
                var npc = attacker;
                _killQueue.OnDeath(victim, null, string.Format(_("MsgFeedKillNpc"), CustomNpcName(npc), SanitizeName(GetClan(victim) + victim.displayName), GetDistanceColor(dist), dist));
                return;
            }

            if (victim.IsNpc)
            {
                if (!_config.EnableNpcFeed) return;
                var npc = victim;
                _killQueue.OnDeath(attacker, null, string.Format(_("MsgFeedKill"), SanitizeName(GetClan(attacker) + attacker.displayName), CustomNpcName(npc), GetCustomWeaponName(hitInfo), HitBone, GetDistanceColor(dist), dist));
                return;
            }
            if (_config.EnableChatFeed)
            {
                if (!_data.DisabledUsers.Contains(attacker.userID))
                    Player.Message(attacker, _("MsgAttacker", attacker), null, _config.IconId, GetClan(victim) + victim.displayName, dist, HitBone);
                if (!_data.DisabledUsers.Contains(victim.userID))
                    Player.Message(victim, _("MsgVictim", victim), null, _config.IconId, GetClan(attacker) + attacker.displayName, dist, GetCustomWeaponName(hitInfo), HitBone);
            }
            _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedKill"), SanitizeName(GetClan(attacker) + attacker.displayName), SanitizeName(GetClan(victim) + victim.displayName), GetCustomWeaponName(hitInfo), HitBone, GetDistanceColor(dist), dist));

            if (!_config.EnableLogging) return;
            var sfkLog = new StringBuilder($"{DateTime.Now}: ({attacker.UserIDString}){attacker.displayName} killed ({victim.UserIDString}){victim.displayName} from {dist}m in {HitBone}");
            LogToFile("SimpleKillFeed", sfkLog.ToString(), this);
        }

        private void OnSuicide(BasePlayer victim) => _killQueue.OnDeath(victim, null, string.Format(_("MsgFeedKillSuicide"), SanitizeName(GetClan(victim) + victim.displayName)));

        private void OnKilledByRadiation(BasePlayer victim) => _killQueue.OnDeath(victim, null, string.Format(_("MsgFeedKillRadiation"), SanitizeName(GetClan(victim) + victim.displayName)));

        private void OnKilledByEnt(BaseEntity attacker, BasePlayer victim) => _killQueue.OnDeath(victim, null, string.Format(_("MsgFeedKillEnt"), SanitizeName(GetClan(victim) + victim.displayName), CustomEntName(attacker)));

        private void OnKilledByAnimal(BaseEntity attacker, BasePlayer victim) => _killQueue.OnDeath(victim, null, string.Format(_("MsgFeedKillAnimal"), SanitizeName(GetClan(victim) + victim.displayName), CustomAnimalName(attacker)));

        private void OnDeathFromWounds(BasePlayer attacker, BasePlayer victim)
        {
            if (_config.EnableChatFeed)
            {
                if (!_data.DisabledUsers.Contains(attacker.userID))
                    Player.Message(attacker, _("MsgAtkWounded", attacker), null, _config.IconId, GetClan(victim) + victim.displayName);
                if (!_data.DisabledUsers.Contains(victim.userID))
                    Player.Message(victim, _("MsgVictimWounded", victim), null, _config.IconId, GetClan(attacker) + attacker.displayName);
            }
            _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedWounded"), SanitizeName(GetClan(attacker) + attacker.displayName), SanitizeName(GetClan(victim) + victim.displayName)));

            if (!_config.EnableLogging) return;
            var sfkLog = new StringBuilder($"{DateTime.Now}: ({attacker.UserIDString}){attacker.displayName} finished ({victim.UserIDString}){victim.displayName}");
            LogToFile("SimpleKillFeed", sfkLog.ToString(), this);
        }

        private void OnBurned(BasePlayer attacker, BasePlayer victim)
        {
            if (victim.IsNpc)
            {
                if (!_config.EnableNpcFeed) return;
                var npc = victim;
                _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedBurned"), SanitizeName(GetClan(attacker) + attacker.displayName), CustomNpcName(npc)));
                return;
            }

            if (attacker.IsNpc)
            {
                if (!_config.EnableNpcFeed) return;
                var npc = attacker;
                _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedBurned"), CustomNpcName(npc), SanitizeName(GetClan(victim) + victim.displayName)));
                return;
            }
            if (_config.EnableChatFeed)
            {
                if (!_data.DisabledUsers.Contains(attacker.userID))
                    Player.Message(attacker, _("MsgAtkBurned", attacker), null, _config.IconId, GetClan(victim) + victim.displayName);
                if (!_data.DisabledUsers.Contains(victim.userID))
                    Player.Message(victim, _("MsgVictimBurned", victim), null, _config.IconId, GetClan(attacker) + attacker.displayName);
            }
            _killQueue.OnDeath(victim, attacker, string.Format(_("MsgFeedBurned"), SanitizeName(GetClan(attacker) + attacker.displayName), SanitizeName(GetClan(victim) + victim.displayName)));

            if (!_config.EnableLogging) return;
            var sfkLog = new StringBuilder($"{DateTime.Now}: ({attacker.UserIDString}){attacker.displayName} burned ({victim.UserIDString}){victim.displayName}");
            LogToFile("SimpleKillFeed", sfkLog.ToString(), this);
        }

        #endregion

        #region UI

        private class KillEvent : Pool.IPooled
        {
            public int DisplayUntil;
            public string Text;

            public KillEvent Init(string text, int displayUntil)
            {
                Text = text;
                DisplayUntil = displayUntil;
                return this;
            }

            public void EnterPool()
            {
                Text = null;
                DisplayUntil = 0;
            }

            public void LeavePool() { }
        }

        private class KillQueue : MonoBehaviour
        {
            private readonly WaitForSeconds _secondDelay = new WaitForSeconds(1f);
            private readonly Queue<KillEvent> _queue = new Queue<KillEvent>(_config.MaxFeedMessages);
            private readonly CuiOutlineComponent _outlineStatic = new CuiOutlineComponent { Distance = _config.OutlineSize, Color = "0 0 0 1" };
            private readonly CuiRectTransformComponent[] _rectTransformStatic = new CuiRectTransformComponent[_config.MaxFeedMessages];
            private readonly CuiTextComponent[] _textStatic = new CuiTextComponent[_config.MaxFeedMessages];
            private readonly CuiElementContainer _cui = new CuiElementContainer();
            private bool _needsRedraw;
            private int _currentlyDrawn;

            public void OnDeath(BasePlayer victim, BasePlayer attacker, string text)
            {
                if (_queue.Count == _config.MaxFeedMessages)
                    DequeueEvent(_queue.Dequeue());
                PushEvent(Pool.Get<KillEvent>().Init(text, Epoch.Current + _config.FeedMessageTtlSec));
            }

            private void PushEvent(KillEvent evt)
            {
                _queue.Enqueue(evt);
                _needsRedraw = true;
                DoProccessQueue();
            }

            private void Start()
            {
                for (var i = 0; i < _config.MaxFeedMessages; i++)
                {
                    _rectTransformStatic[i] = new CuiRectTransformComponent
                    {
                        AnchorMax =
                            $"{_config.AnchorMax.Split(Convert.ToChar(' '))[0]} {float.Parse(_config.AnchorMax.Split(Convert.ToChar(' '))[1]) - (_config.HeightIdent * i)}",
                        AnchorMin =
                            $"{_config.AnchorMin.Split(Convert.ToChar(' '))[0]} {float.Parse(_config.AnchorMin.Split(Convert.ToChar(' '))[1]) - (_config.HeightIdent * i)}"
                    };
                    _textStatic[i] = new CuiTextComponent { Align = TextAnchor.MiddleRight, FontSize = _config.FontSize, Text = string.Empty };
                }
                StartCoroutine(ProccessQueue());
            }

            private void DequeueEvent(KillEvent evt)
            {
                Pool.Free(ref evt);
                _needsRedraw = true;
            }

            private void DoProccessQueue()
            {
                while (_queue.Count > 0 && _queue.Peek().DisplayUntil < Epoch.Current)
                    DequeueEvent(_queue.Dequeue());

                if (!_needsRedraw)
                    return;
                var toBeRemoved = _currentlyDrawn;
                _currentlyDrawn = 0;
                foreach (var killEvent in _queue)
                {
                    var cuiText = _textStatic[_currentlyDrawn];
                    cuiText.Text = killEvent.Text;
                    _cui.Add(new CuiElement
                    {
                        Name = $"kf-{_currentlyDrawn}",
                        Parent = "Hud",
                        Components =
                        {
                            cuiText,
                            _rectTransformStatic[_currentlyDrawn],
                            _outlineStatic
                        }
                    });
                    if (++_currentlyDrawn == _config.MaxFeedMessages)
                        break;
                }
                _needsRedraw = false;
                SendKillCui(_cui, toBeRemoved);
                _cui.Clear();
            }

            private IEnumerator ProccessQueue()
            {
                while (!Interface.Oxide.IsShuttingDown)
                {
                    DoProccessQueue();
                    yield return _secondDelay;
                }
            }

            private static void SendKillCui(CuiElementContainer cui, int toBeRemoved)
            {
                var json = cui.ToJson();
                foreach (var plr in BasePlayer.activePlayerList)
                {
                    for (var i = toBeRemoved; i > 0; i--)
                        CuiHelper.DestroyUi(plr, $"kf-{i - 1}");
                    if (!_data.DisabledUsers.Contains(plr.userID))
                        CuiHelper.AddUi(plr, json);
                }
            }

            public static void RemoveKillCui(string name)
            {
                foreach (var plr in BasePlayer.activePlayerList) 
                    CuiHelper.DestroyUi(plr, name);
            }
        }

        #endregion

        #region Utils

        private string _(string msg, BasePlayer player = null) => lang.GetMessage(msg, this, player?.UserIDString);

        private string GetCustomWeaponName(HitInfo hitInfo)
        {
            var name = GetWeaponName(hitInfo);
            if (string.IsNullOrEmpty(name))
                return null;

            string translatedName;
            if (_config.Weapons.TryGetValue(name, out translatedName))
                return translatedName;

            _config.Weapons.Add(name, name);
            Config.WriteObject(_config);
            return name;
        }

        private string CustomNpcName(BasePlayer npc)
        {
            var name = npc.ShortPrefabName;
            if (string.IsNullOrEmpty(name))
                return null;
            if (npc.displayName != npc.userID.ToString())
                return npc.displayName;
            string translatedName;
            if (_config.Npcs.TryGetValue(name, out translatedName))
                return translatedName;

            _config.Npcs.Add(name, name);
            Config.WriteObject(_config);
            return name;
        }

        private string CustomEntName(BaseEntity attacker)
        {
            var name = attacker.ShortPrefabName;
            if (string.IsNullOrEmpty(name))
                return null;
            string translatedName;
            if (_config.Ents.TryGetValue(name, out translatedName))
                return translatedName;

            _config.Npcs.Add(name, name);
            Config.WriteObject(_config);
            return name;
        }

        private string CustomAnimalName(BaseEntity attacker)
        {
            var name = attacker.ShortPrefabName;
            if (string.IsNullOrEmpty(name))
                return null;
            string translatedName;
            if (_config.Animal.TryGetValue(name, out translatedName))
                return translatedName;

            _config.Npcs.Add(name, name);
            Config.WriteObject(_config);
            return name;
        }

        private string GetWeaponName(HitInfo hitInfo)
        {
            var wpnName = "??Unknown??";
            if (hitInfo.Weapon == null)
            {
                if (hitInfo.WeaponPrefab.prefabID == 3717106868) wpnName = "Flamethrower";
                if (hitInfo.WeaponPrefab.prefabID == 3898309212) wpnName = "C4";
                if (hitInfo.WeaponPrefab.prefabID == 3046924118) wpnName = "Rocket";
                if (hitInfo.WeaponPrefab.prefabID == 1217937936) wpnName = "HV Rocket";
                if (hitInfo.WeaponPrefab.prefabID == 2742759844) wpnName = "Satchel";
                if (hitInfo.WeaponPrefab.prefabID == 2144399804) wpnName = "Beancan";
                if (hitInfo.WeaponPrefab.prefabID == 1859672190) wpnName = "Shell";
                if (hitInfo.WeaponPrefab.prefabID == 1128089209) wpnName = "Grenade";
                return wpnName;
            }

            if (hitInfo.Weapon != null)
            {
                var wpn = hitInfo.Weapon;
                var item = wpn.GetItem();
                if (item != null)
                    wpnName = item.info.displayName.english;
            }
            else if (hitInfo.WeaponPrefab != null)
            {
                if (!_itemNameMapping.TryGetValue(hitInfo.WeaponPrefab.prefabID, out wpnName))
                    PrintWarning($"GetWeaponName failed. Unresolved prefab {hitInfo.WeaponPrefab.prefabID} ({hitInfo.damageTypes.GetMajorityDamageType()}/{hitInfo.damageTypes.Get(hitInfo.damageTypes.GetMajorityDamageType())})");
            }
            else
                PrintWarning($"GetWeaponName failed. Weapon and WeaponPrefab is null? ({hitInfo.damageTypes.GetMajorityDamageType()})");
            return wpnName;
        }

        private static bool IsExplosion(HitInfo hit) => (hit.WeaponPrefab != null && (hit.WeaponPrefab.ShortPrefabName.Contains("grenade") || hit.WeaponPrefab.ShortPrefabName.Contains("explosive")))
                                                        || hit.damageTypes.GetMajorityDamageType() == DamageType.Explosion || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Explosion));

        private static bool IsFlame(HitInfo hit) => hit.damageTypes.GetMajorityDamageType() == DamageType.Heat || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Heat));

        private static bool IsRadiation(HitInfo hit) => hit.damageTypes.GetMajorityDamageType() == DamageType.Radiation || (!hit.damageTypes.IsBleedCausing() && hit.damageTypes.Has(DamageType.Radiation));

        private static bool IsTrap(BaseEntity ent) => ent != null && _config.Ents.ContainsKey(ent.ShortPrefabName);

        private static bool IsAnimal(BaseEntity animal) => animal?.ShortPrefabName != null && _config.Animal.ContainsKey(animal.ShortPrefabName);

        private static string GetDistanceColor(int dist)
        {
            foreach (var distanceColor in _config.DistanceColors)
            {
                if (distanceColor.TestDistance(dist))
                    return distanceColor.Color;
            }
            return _config.DefaultDistanceColor ?? "white";
        }

        private string GetClan(BasePlayer player)
        {
            if (_isClansReborn || _isClansIo || Clans == null) return null;
            var clan = (string)Clans.Call("GetClanOf", player.UserIDString);
            if (clan == null) return null;
            var format = string.Format("[" + clan + "] ");
            return format;
        }

        private static string SanitizeName(string name)
        {
            if (name.Length > _config.MaxPlayerNameLength)
                name = name.Substring(0, _config.MaxPlayerNameLength).Trim();
            return name.Replace("\"", "''");
        }
        #endregion
    }
}
