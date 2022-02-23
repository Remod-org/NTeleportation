using System;
using UnityEngine;
using System.Text.RegularExpressions;
using Oxide.Core.Configuration;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core;
using Newtonsoft.Json;
using System.Collections;
using Newtonsoft.Json.Converters;
using Rust;
using System.Linq;
using Oxide.Game.Rust;
using System.Globalization;
using Facepunch;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("RTeleportation", "RFC1920", "1.0.92", ResourceId = 1832)]
    // Thanks to the original author, Nogrod.
    internal class RTeleportation : RustPlugin
    {
        private static readonly Vector3 Up = Vector3.up;
        private static readonly Vector3 Down = Vector3.down;
        private const string NewLine = "\n";
        private const string ConfigDefaultPermVip = "rteleportation.vip";
        private const string PermHome = "rteleportation.home";
        private const string PermTpR = "rteleportation.tpr";
        private const string PermDeleteHome = "rteleportation.deletehome";
        private const string PermHomeHomes = "rteleportation.homehomes";
        private const string PermImportHomes = "rteleportation.importhomes";
        private const string PermRadiusHome = "rteleportation.radiushome";
        private const string PermTp = "rteleportation.tp";
        private const string PermTpB = "rteleportation.tpb";
        private const string PermTpConsole = "rteleportation.tpconsole";
        private const string PermTpHome = "rteleportation.tphome";
        private const string PermTpTown = "rteleportation.tptown";
        private const string PermTpOutpost = "rteleportation.tpoutpost";
        private const string PermTpBandit = "rteleportation.tpbandit";
        private const string PermTpN = "rteleportation.tpn";
        private const string PermTpL = "rteleportation.tpl";
        private const string PermTpRemove = "rteleportation.tpremove";
        private const string PermTpSave = "rteleportation.tpsave";
        private const string PermWipeHomes = "rteleportation.wipehomes";
        private const string PermCraftHome = "rteleportation.crafthome";
        private const string PermCraftTown = "rteleportation.crafttown";
        private const string PermCraftOutpost = "rteleportation.craftoutpost";
        private const string PermCraftBandit = "rteleportation.craftbandit";
        private const string PermCraftTpR = "rteleportation.crafttpr";
        private DynamicConfigFile dataAdmin;
        private DynamicConfigFile dataHome;
        private DynamicConfigFile dataTPR;
        private DynamicConfigFile dataTown;
        private DynamicConfigFile dataOutpost;
        private DynamicConfigFile dataBandit;
        private Dictionary<ulong, AdminData> Admin;
        private Dictionary<ulong, HomeData> Home;
        private Dictionary<ulong, TeleportData> TPR;
        private Dictionary<ulong, TeleportData> Town;
        private Dictionary<ulong, TeleportData> Outpost;
        private Dictionary<ulong, TeleportData> Bandit;
        private bool changedAdmin;
        private bool changedHome;
        private bool changedTPR;
        private bool changedTown;
        private bool changedOutpost;
        private bool changedBandit;
        private ConfigData configData;
        private float boundary;
        private readonly int triggerLayer = LayerMask.GetMask("Trigger");
        private readonly int groundLayer = LayerMask.GetMask("Terrain", "World");
        private readonly int buildingLayer = LayerMask.GetMask("Terrain", "World", "Construction", "Deployed");
        private readonly int blockLayer = LayerMask.GetMask("Construction");
        private readonly Dictionary<ulong, TeleportTimer> TeleportTimers = new Dictionary<ulong, TeleportTimer>();
        private readonly Dictionary<ulong, Timer> PendingRequests = new Dictionary<ulong, Timer>();
        private readonly Dictionary<ulong, BasePlayer> PlayersRequests = new Dictionary<ulong, BasePlayer>();
        private readonly Dictionary<int, string> ReverseBlockedItems = new Dictionary<int, string>();
        private readonly HashSet<ulong> teleporting = new HashSet<ulong>();
        private SortedDictionary<string, Vector3> monPos  = new SortedDictionary<string, Vector3>();
        private SortedDictionary<string, Vector3> monSize = new SortedDictionary<string, Vector3>();
        private SortedDictionary<string, Vector3> cavePos  = new SortedDictionary<string, Vector3>();

        [PluginReference]
        private readonly Plugin Clans, Economics, ServerRewards, Friends, RustIO;

        private void DoLog(string message)
        {
            if (configData.debug) Puts(message);
        }

        private class ConfigData
        {
            public SettingsData Settings { get; set; }
            public GameVersionData GameVersion { get; set; }
            public AdminSettingsData Admin { get; set; }
            public HomesSettingsData Home { get; set; }
            public TPRData TPR { get; set; }
            public TownData Town { get; set; }
            public TownData Outpost { get; set; }
            public TownData Bandit { get; set; }
            public VersionNumber Version { get; set; }
            public bool debug;
        }

        private class SettingsData
        {
            public string ChatName { get; set; }
            public bool HomesEnabled { get; set; }
            public bool TPREnabled { get; set; }
            public bool TownEnabled { get; set; }
            public bool OutpostEnabled { get; set; }
            public bool BanditEnabled { get; set; }
            public bool InterruptTPOnHurt { get; set; }
            public bool InterruptTPOnCold { get; set; }
            public bool InterruptTPOnHot { get; set; }
            public bool InterruptTPOnHostile { get; set; }
            public bool InterruptTPOnSafe { get; set; }
            public bool InterruptTPOnBalloon { get; set; }
            public bool InterruptTPOnCargo { get; set; }
            public bool InterruptTPOnRig { get; set; }
            public bool InterruptTPOnExcavator { get; set; }
            public bool InterruptTPOnLift { get; set; }
            public bool InterruptTPOnMonument { get; set; }
            public bool InterruptTPOnMounted { get; set; }
            public bool InterruptTPOnSwimming { get; set; }
            public bool InterruptAboveWater{ get; set; }
            public bool StrictFoundationCheck { get; set; }
            public float CaveDistanceSmall { get; set; }
            public float CaveDistanceMedium { get; set; }
            public float CaveDistanceLarge { get; set; }
            public float DefaultMonumentSize { get; set; }
            public float MinimumTemp { get; set; }
            public float MaximumTemp { get; set; }
            public Dictionary<string, string> BlockedItems { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string BypassCMD { get; set; }
            public bool UseEconomics { get; set; }
            public bool UseServerRewards { get; set; }
            public bool WipeOnUpgradeOrChange { get; set; }
            public bool AutoGenOutpost { get; set; }
            public bool AutoGenBandit { get; set; }
        }

        private class GameVersionData
        {
            public int Network { get; set; }
            public int Save { get; set; }
            public string Level { get; set; }
            public string LevelURL { get; set; }
            public int WorldSize { get; set; }
            public int Seed { get; set; }
        }

        private class AdminSettingsData
        {
            public bool AnnounceTeleportToTarget { get; set; }
            public bool UseableByAdmins { get; set; }
            public bool UseableByModerators { get; set; }
            public int LocationRadius { get; set; }
            public int TeleportNearDefaultDistance { get; set; }
        }

        private class HomesSettingsData
        {
            public int HomesLimit { get; set; }
            public Dictionary<string, int> VIPHomesLimits { get; set; }
            public int Cooldown { get; set; }
            public int Countdown { get; set; }
            public int DailyLimit { get; set; }
            public Dictionary<string, int> VIPDailyLimits { get; set; }
            public Dictionary<string, int> VIPCooldowns { get; set; }
            public Dictionary<string, int> VIPCountdowns { get; set; }
            public int LocationRadius { get; set; }
            public bool ForceOnTopOfFoundation { get; set; }
            public bool CheckFoundationForOwner { get; set; }
            public bool UseFriends { get; set; }
            public bool UseClans { get; set; }
            public bool UseTeams { get; set; }
            public bool UsableOutOfBuildingBlocked { get; set; }
            public bool UsableIntoBuildingBlocked { get; set; }
            public bool CupOwnerAllowOnBuildingBlocked { get; set; }
            public bool AllowIceberg { get; set; }
            public bool AllowCave { get; set; }
            public bool AllowCraft { get; set; }
            public bool AllowAboveFoundation { get; set; }
            public bool CheckValidOnList { get; set; }
            public int Pay { get; set; }
            public int Bypass { get; set; }
        }

        private class TPRData
        {
            public int Cooldown { get; set; }
            public int Countdown { get; set; }
            public int DailyLimit { get; set; }
            public Dictionary<string, int> VIPDailyLimits { get; set; }
            public Dictionary<string, int> VIPCooldowns { get; set; }
            public Dictionary<string, int> VIPCountdowns { get; set; }
            public int RequestDuration { get; set; }
            public bool OffsetTPRTarget { get; set; }
            public bool AutoAcceptTPR { get; set; }
            public bool BlockTPAOnCeiling { get; set; }
            public bool UsableOutOfBuildingBlocked { get; set; }
            public bool UsableIntoBuildingBlocked { get; set; }
            public bool CupOwnerAllowOnBuildingBlocked { get; set; }
            public bool AllowCraft { get; set; }
            public int Pay { get; set; }
            public int Bypass { get; set; }
        }

        private class TownData
        {
            public int Cooldown { get; set; }
            public int Countdown { get; set; }
            public int DailyLimit { get; set; }
            public Dictionary<string, int> VIPDailyLimits { get; set; }
            public Dictionary<string, int> VIPCooldowns { get; set; }
            public Dictionary<string, int> VIPCountdowns { get; set; }
            public Vector3 Location { get; set; }
            public bool UsableOutOfBuildingBlocked { get; set; }
            public bool AllowCraft { get; set; }
            public int Pay { get; set; }
            public int Bypass { get; set; }
        }

        private class AdminData
        {
            [JsonProperty("pl")]
            public Vector3 PreviousLocation { get; set; }

            [JsonProperty("l")]
            public Dictionary<string, Vector3> Locations { get; set; } = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        }

        private class HomeData
        {
            [JsonProperty("l")]
            public Dictionary<string, Vector3> Locations { get; set; } = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);

            [JsonProperty("t")]
            public TeleportData Teleports { get; set; } = new TeleportData();
        }

        private class TeleportData
        {
            [JsonProperty("a")]
            public int Amount { get; set; }

            [JsonProperty("d")]
            public string Date { get; set; }

            [JsonProperty("t")]
            public int Timestamp { get; set; }
        }

        private class TeleportTimer
        {
            public Timer Timer { get; set; }
            public BasePlayer OriginPlayer { get; set; }
            public BasePlayer TargetPlayer { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            Config.Settings.Converters = new JsonConverter[] { new UnityVector3Converter() };
            Config.WriteObject(new ConfigData
            {
                Settings = new SettingsData
                {
                    ChatName = "<color=red>Teleportation</color>: ",
                    HomesEnabled = true,
                    TPREnabled = true,
                    TownEnabled = true,
                    OutpostEnabled = true,
                    BanditEnabled = true,
                    InterruptTPOnHurt = true,
                    InterruptTPOnCold = false,
                    InterruptTPOnHot = false,
                    InterruptTPOnHostile = false,
                    MinimumTemp = 0f,
                    MaximumTemp = 40f,
                    InterruptTPOnSafe = true,
                    InterruptTPOnBalloon = true,
                    InterruptTPOnCargo = true,
                    InterruptTPOnRig = false,
                    InterruptTPOnExcavator = false,
                    InterruptTPOnLift = true,
                    InterruptTPOnMonument = false,
                    InterruptTPOnMounted = true,
                    InterruptTPOnSwimming = true,
                    InterruptAboveWater = false,
                    StrictFoundationCheck = false,
                    CaveDistanceSmall = 40f,
                    CaveDistanceMedium = 60f,
                    CaveDistanceLarge = 100f,
                    DefaultMonumentSize = 50f,
                    BypassCMD = "pay",
                    UseEconomics = false,
                    UseServerRewards = false,
                    WipeOnUpgradeOrChange = false,
                    AutoGenOutpost = false,
                    AutoGenBandit = false
                },
                GameVersion = new GameVersionData
                {
                    Network = Convert.ToInt32(Protocol.network),
                    Save = Convert.ToInt32(Protocol.save),
                    Level = ConVar.Server.level,
                    LevelURL = ConVar.Server.levelurl,
                    WorldSize = ConVar.Server.worldsize,
                    Seed = ConVar.Server.seed
                },
                Admin = new AdminSettingsData
                {
                    AnnounceTeleportToTarget = false,
                    UseableByAdmins = true,
                    UseableByModerators = true,
                    LocationRadius = 25,
                    TeleportNearDefaultDistance = 30
                },
                Home = new HomesSettingsData
                {
                    HomesLimit = 2,
                    VIPHomesLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    Cooldown = 600,
                    Countdown = 15,
                    DailyLimit = 5,
                    VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    LocationRadius = 25,
                    ForceOnTopOfFoundation = true,
                    CheckFoundationForOwner = true,
                    UseFriends = true,
                    UseClans = true,
                    UseTeams = true,
                    AllowAboveFoundation = true,
                    CheckValidOnList = false,
                    CupOwnerAllowOnBuildingBlocked = true
                },
                TPR = new TPRData
                {
                    Cooldown = 600,
                    Countdown = 15,
                    DailyLimit = 5,
                    VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    RequestDuration = 30,
                    BlockTPAOnCeiling = true,
                    OffsetTPRTarget = true,
                    AutoAcceptTPR = false,
                    CupOwnerAllowOnBuildingBlocked = true
                },
                Town = new TownData
                {
                    Cooldown = 600,
                    Countdown = 15,
                    DailyLimit = 5,
                    VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } }
                },
                Outpost = new TownData
                {
                    Cooldown = 600,
                    Countdown = 15,
                    DailyLimit = 5,
                    VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } }
                },
                Bandit = new TownData
                {
                    Cooldown = 600,
                    Countdown = 15,
                    DailyLimit = 5,
                    VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                    VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } }
                },
                Version = Version
            }, true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AdminTP", "You teleported to {0}!"},
                {"AdminTPTarget", "{0} teleported to you!"},
                {"AdminTPPlayers", "You teleported {0} to {1}!"},
                {"AdminTPPlayer", "{0} teleported you to {1}!"},
                {"AdminTPPlayerTarget", "{0} teleported {1} to you!"},
                {"AdminTPCoordinates", "You teleported to {0}!"},
                {"AdminTPTargetCoordinates", "You teleported {0} to {1}!"},
                {"AdminTPOutOfBounds", "You tried to teleport to a set of coordinates outside the map boundaries!"},
                {"AdminTPBoundaries", "X and Z values need to be between -{0} and {0} while the Y value needs to be between -100 and 2000!"},
                {"AdminTPLocation", "You teleported to {0}!"},
                {"AdminTPLocationSave", "You have saved the current location!"},
                {"AdminTPLocationRemove", "You have removed the location {0}!"},
                {"AdminLocationList", "The following locations are available:"},
                {"AdminLocationListEmpty", "You haven't saved any locations!"},
                {"AdminTPBack", "You've teleported back to your previous location!"},
                {"AdminTPBackSave", "Your previous location has been saved, use /tpb to teleport back!"},
                {"AdminTPTargetCoordinatesTarget", "{0} teleported you to {1}!"},
                {"AdminTPConsoleTP", "You were teleported to {0}"},
                {"AdminTPConsoleTPPlayer", "You were teleported to {0}"},
                {"AdminTPConsoleTPPlayerTarget", "{0} was teleported to you!"},
                {"HomeTP", "You teleported to your home '{0}'!"},
                {"HomeAdminTP", "You teleported to {0}'s home '{1}'!"},
                {"HomeSave", "You have saved the current location as your home!"},
                {"HomeNoFoundation", "You can only use a home location on a foundation!"},
                {"HomeFoundationNotOwned", "You can't use home on someone else's house."},
                {"HomeFoundationUnderneathFoundation", "You can't use home on a foundation that is underneath another foundation."},
                {"HomeFoundationNotFriendsOwned", "You or a friend need to own the house to use home!"},
                {"HomeRemovedInvalid", "Your home '{0}' was removed because not on a foundation or not owned!"},
                {"HighWallCollision", "High Wall Collision!"},
                {"HomeRemovedInsideBlock", "Your home '{0}' was removed because inside a foundation!"},
                {"HomeRemove", "You have removed your home {0}!"},
                {"HomeDelete", "You have removed {0}'s home '{1}'!"},
                {"HomeList", "The following homes are available:"},
                {"HomeListEmpty", "You haven't saved any homes!"},
                {"HomeMaxLocations", "Unable to set your home here, you have reached the maximum of {0} homes!"},
                {"HomeQuota", "You have set {0} of the maximum {1} homes!"},
                {"HomeTPStarted", "Teleporting to your home {0} in {1} seconds!"},
                {"PayToHome", "Standard payment of {0} applies to all home teleports!"},
                {"PayToTown", "Standard payment of {0} applies to all town teleports!"},
                {"PayToTPR", "Standard payment of {0} applies to all tprs!"},
                {"HomeTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"HomeTPCooldownBypass", "Your teleport was currently on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"HomeTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"HomeTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"HomeTPCooldownBypassP2", "Type /home NAME {0}." },
                {"HomeTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"HomeTPAmount", "You have {0} home teleports left today!"},
                {"HomesListWiped", "You have wiped all the saved home locations!"},
                {"HomeTPBuildingBlocked", "You can't set your home if you are not allowed to build in this zone!"},
                {"HomeTPSwimming", "You can't set your home while swimming!"},
                {"HomeTPCrafting", "You can't set your home while crafting!"},
                {"Request", "You've requested a teleport to {0}!"},
                {"RequestTarget", "{0} requested to be teleported to you! Use '/tpa' to accept!"},
                {"PendingRequest", "You already have a request pending, cancel that request or wait until it gets accepted or times out!"},
                {"PendingRequestTarget", "The player you wish to teleport to already has a pending request, try again later!"},
                {"NoPendingRequest", "You have no pending teleport request!"},
                {"AcceptOnRoof", "You can't accept a teleport while you're on a ceiling, get to ground level!"},
                {"Accept", "{0} has accepted your teleport request! Teleporting in {1} seconds!"},
                {"AcceptTarget", "You've accepted the teleport request of {0}!"},
                {"NotAllowed", "You are not allowed to use this command!"},
                {"Success", "You teleported to {0}!"},
                {"SuccessTarget", "{0} teleported to you!"},
                {"Cancelled", "Your teleport request to {0} was cancelled!"},
                {"CancelledTarget", "{0} teleport request was cancelled!"},
                {"TPCancelled", "Your teleport was cancelled!"},
                {"TPCancelledTarget", "{0} cancelled teleport!"},
                {"TPYouCancelledTarget", "You cancelled {0} teleport!"},
                {"TimedOut", "{0} did not answer your request in time!"},
                {"TimedOutTarget", "You did not answer {0}'s teleport request in time!"},
                {"TargetDisconnected", "{0} has disconnected, your teleport was cancelled!"},
                {"TPRCooldown", "Your teleport requests are currently on cooldown. You'll have to wait {0} to send your next teleport request."},
                {"TPRCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"TPRCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"TPRCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"TPMoney", "{0} deducted from your account!"},
                {"TPNoMoney", "You do not have {0} in any account!"},
                {"TPRCooldownBypassP2", "Type /tpr {0}." },
                {"TPRCooldownBypassP2a", "Type /tpr NAME {0}." },
                {"TPRLimitReached", "You have reached the daily limit of {0} teleport requests today!"},
                {"TPRAmount", "You have {0} teleport requests left today!"},
                {"TPRTarget", "Your target is currently not available!"},
                {"TPDead", "You can't teleport while being dead!"},
                {"TPWounded", "You can't teleport while wounded!"},
                {"TPTooCold", "You're too cold to teleport!"},
                {"TPTooHot", "You're too hot to teleport!"},
                {"TPHostile", "Can't teleport to outpost or bandit when hostile!"},
                {"HostileTimer", "Teleport available in {0} minutes."},
                {"TPMounted", "You can't teleport while seated!"},
                {"TPBuildingBlocked", "You can't teleport while in a building blocked zone!"},
                {"TPAboveWater", "You can't teleport while above water!"},
                {"TPTargetBuildingBlocked", "You can't teleport in a building blocked zone!"},
                {"TPTargetInsideBlock", "You can't teleport into a foundation!"},
                {"TPSwimming", "You can't teleport while swimming!"},
                {"TPCargoShip", "You can't teleport from the cargo ship!"},
                {"TPOilRig", "You can't teleport from the oil rig!"},
                {"TPExcavator", "You can't teleport from the excavator!"},
                {"TPHotAirBalloon", "You can't teleport to or from a hot air balloon!"},
                {"TPLift", "You can't teleport while in an elevator or bucket lift!"},
                {"TPBucketLift", "You can't teleport while in a bucket lift!"},
                {"TPRegLift", "You can't teleport while in an elevator!"},
                {"TPSafeZone", "You can't teleport from a safezone!"},
                {"TPCrafting", "You can't teleport while crafting!"},
                {"TPBlockedItem", "You can't teleport while carrying: {0}!"},
                {"TooCloseToMon", "You can't teleport so close to the {0}!"},
                {"TooCloseToCave", "You can't teleport so close to a cave!"},
                {"HomeTooCloseToCave", "You can't set home so close to a cave!"},
                {"TownTP", "You teleported to town!"},
                {"TownTPNotSet", "Town is currently not set!"},
                {"TownTPDisabled", "Town is currently not enabled!"},
                {"TownTPLocation", "You have set the town location to {0}!"},
                {"TownTPStarted", "Teleporting to town in {0} seconds!"},
                {"TownTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"TownTPCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"TownTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"TownTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"TownTPCooldownBypassP2", "Type /town {0}." },
                {"TownTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"TownTPAmount", "You have {0} town teleports left today!"},

                {"OutpostTP", "You teleported to the outpost!"},
                {"OutpostTPNotSet", "Outpost is currently not set!"},
                {"OutpostTPDisabled", "Outpost is currently not enabled!"},
                {"OutpostTPLocation", "You have set the outpost location to {0}!"},
                {"OutpostTPStarted", "Teleporting to the outpost in {0} seconds!"},
                {"OutpostTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"OutpostTPCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"OutpostTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"OutpostTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"OutpostTPCooldownBypassP2", "Type /outpost {0}." },
                {"OutpostTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"OutpostTPAmount", "You have {0} outpost teleports left today!"},

                {"BanditTP", "You teleported to bandit town!"},
                {"BanditTPNotSet", "Bandit is currently not set!"},
                {"BanditTPDisabled", "Bandit is currently not enabled!"},
                {"BanditTPLocation", "You have set the bandit town location to {0}!"},
                {"BanditTPStarted", "Teleporting to bandit town in {0} seconds!"},
                {"BanditTPCooldown", "Your teleport is currently on cooldown. You'll have to wait {0} for your next teleport."},
                {"BanditTPCooldownBypass", "Your teleport request was on cooldown. You chose to bypass that by paying {0} from your balance."},
                {"BanditTPCooldownBypassF", "Your teleport is currently on cooldown. You do not have sufficient funds - {0} - to bypass."},
                {"BanditTPCooldownBypassP", "You may choose to pay {0} to bypass this cooldown." },
                {"BanditTPCooldownBypassP2", "Type /bandit {0}." },
                {"BanditTPLimitReached", "You have reached the daily limit of {0} teleports today!"},
                {"BanditTPAmount", "You have {0} bandit town teleports left today!"},

                {"Interrupted", "Your teleport was interrupted!"},
                {"InterruptedTarget", "{0}'s teleport was interrupted!"},
                {"Unlimited", "Unlimited"},
                {
                    "TPInfoGeneral", string.Join(NewLine, new[]
                    {
                        "Please specify the module you want to view the info of.",
                        "The available modules are: ",
                    })
                },
                {
                    "TPHelpGeneral", string.Join(NewLine, new[]
                    {
                        "/tpinfo - Shows limits and cooldowns.",
                        "Please specify the module you want to view the help of.",
                        "The available modules are: ",
                    })
                },
                {
                    "TPHelpadmintp", string.Join(NewLine, new[]
                    {
                        "As an admin you have access to the following commands:",
                        "/tp \"targetplayer\" - Teleports yourself to the target player.",
                        "/tp \"player\" \"targetplayer\" - Teleports the player to the target player.",
                        "/tp x y z - Teleports you to the set of coordinates.",
                        "/tpl - Shows a list of saved locations.",
                        "/tpl \"location name\" - Teleports you to a saved location.",
                        "/tpsave \"location name\" - Saves your current position as the location name.",
                        "/tpremove \"location name\" - Removes the location from your saved list.",
                        "/tpb - Teleports you back to the place where you were before teleporting.",
                        "/home radius \"radius\" - Find all homes in radius.",
                        "/home delete \"player name|id\" \"home name\" - Remove a home from a player.",
                        "/home tp \"player name|id\" \"name\" - Teleports you to the home location with the name 'name' from the player.",
                        "/home homes \"player name|id\" - Shows you a list of all homes from the player."
                    })
                },
                {
                    "TPHelphome", string.Join(NewLine, new[]
                    {
                        "With the following commands you can set your home location to teleport back to:",
                        "/home add \"name\" - Saves your current position as the location name.",
                        "/home list - Shows you a list of all the locations you have saved.",
                        "/home remove \"name\" - Removes the location of your saved homes.",
                        "/home \"name\" - Teleports you to the home location."
                    })
                },
                {
                    "TPHelptpr", string.Join(NewLine, new[]
                    {
                        "With these commands you can request to be teleported to a player or accept someone else's request:",
                        "/tpr \"player name\" - Sends a teleport request to the player.",
                        "/tpa - Accepts an incoming teleport request.",
                        "/tpc - Cancel teleport or request."
                    })
                },
                {
                    "TPSettingsGeneral", string.Join(NewLine, new[]
                    {
                        "Please specify the module you want to view the settings of. ",
                        "The available modules are:",
                    })
                },
                {
                    "TPSettingshome", string.Join(NewLine, new[]
                    {
                        "Home System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}",
                        "Amount of saved Home locations: {2}"
                    })
                },
                {
                    "TPSettingstpr", string.Join(NewLine, new[]
                    {
                        "TPR System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}"
                    })
                },
                {
                    "TPSettingstown", string.Join(NewLine, new[]
                    {
                        "Town System has the current settings enabled:",
                        "Time between teleports: {0}",
                        "Daily amount of teleports: {1}"
                    })
                },
                {"PlayerNotFound", "The specified player couldn't be found please try again!"},
                {"MultiplePlayers", "Found multiple players: {0}"},
                {"CantTeleportToSelf", "You can't teleport to yourself!"},
                {"CantTeleportPlayerToSelf", "You can't teleport a player to himself!"},
                {"TeleportPending", "You can't initiate another teleport while you have a teleport pending!"},
                {"TeleportPendingTarget", "You can't request a teleport to someone who's about to teleport!"},
                {"LocationExists", "A location with this name already exists at {0}!"},
                {"LocationExistsNearby", "A location with the name {0} already exists near this position!"},
                {"LocationNotFound", "Couldn't find a location with that name!"},
                {"NoPreviousLocationSaved", "No previous location saved!"},
                {"HomeExists", "You have already saved a home location by this name!"},
                {"HomeExistsNearby", "A home location with the name {0} already exists near this position!"},
                {"HomeNotFound", "Couldn't find your home with that name!"},
                {"InvalidCoordinates", "The coordinates you've entered are invalid!"},
                {"InvalidHelpModule", "Invalid module supplied!"},
                {"InvalidCharacter", "You have used an invalid character, please limit yourself to the letters a to z and numbers."},
                {
                    "SyntaxCommandTP", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tp command as follows:",
                        "/tp \"targetplayer\" - Teleports yourself to the target player.",
                        "/tp \"player\" \"targetplayer\" - Teleports the player to the target player.",
                        "/tp x y z - Teleports you to the set of coordinates.",
                        "/tp \"player\" x y z - Teleports the player to the set of coordinates."
                    })
                },
                {
                    "SyntaxCommandTPL", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpl command as follows:",
                        "/tpl - Shows a list of saved locations.",
                        "/tpl \"location name\" - Teleports you to a saved location."
                    })
                },
                {
                    "SyntaxCommandTPSave", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpsave command as follows:",
                        "/tpsave \"location name\" - Saves your current position as 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPRemove", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpremove command as follows:",
                        "/tpremove \"location name\" - Removes the location with the name 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPN", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpn command as follows:",
                        "/tpn \"targetplayer\" - Teleports yourself the default distance behind the target player.",
                        "/tpn \"targetplayer\" \"distance\" - Teleports you the specified distance behind the target player."
                    })
                },
                {
                    "SyntaxCommandSetHome", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home add command as follows:",
                        "/home add \"name\" - Saves the current location as your home with the name 'name'."
                    })
                },
                {
                    "SyntaxCommandRemoveHome", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home remove command as follows:",
                        "/home remove \"name\" - Removes the home location with the name 'name'."
                    })
                },
                {
                    "SyntaxCommandHome", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home command as follows:",
                        "/home \"name\" - Teleports yourself to your home with the name 'name'.",
                        "/home \"name\" pay - Teleports yourself to your home with the name 'name', avoiding cooldown by paying for it.",
                        "/home add \"name\" - Saves the current location as your home with the name 'name'.",
                        "/home list - Shows you a list of all your saved home locations.",
                        "/home remove \"name\" - Removes the home location with the name 'name'."
                    })
                },
                {
                    "SyntaxCommandHomeAdmin", string.Join(NewLine, new[]
                    {
                        "/home radius \"radius\" - Shows you a list of all homes in radius(10).",
                        "/home delete \"player name|id\" \"name\" - Removes the home location with the name 'name' from the player.",
                        "/home tp \"player name|id\" \"name\" - Teleports you to the home location with the name 'name' from the player.",
                        "/home homes \"player name|id\" - Shows you a list of all homes from the player."
                    })
                },
                {
                    "SyntaxCommandTown", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /town command as follows:",
                        "/town - Teleports yourself to town.",
                        "/town pay - Teleports yourself to town, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandTownAdmin", string.Join(NewLine, new[]
                    {
                        "/town set - Saves the current location as town.",
                    })
                },
                {
                    "SyntaxCommandOutpost", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /town command as follows:",
                        "/outpost - Teleports yourself to the Outpost.",
                        "/outpost pay - Teleports yourself to the Outpost, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandOutpostAdmin", string.Join(NewLine, new[]
                    {
                        "/outpost set - Saves the current location as Outpost.",
                    })
                },
                {
                    "SyntaxCommandBandit", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /bandit command as follows:",
                        "/bandit - Teleports yourself to the Bandit Town.",
                        "/bandit pay - Teleports yourself to the Bandit Town, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandBanditAdmin", string.Join(NewLine, new[]
                    {
                        "/bandit set - Saves the current location as Bandit Town.",
                    })
                },
                {
                    "SyntaxCommandHomeDelete", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home delete command as follows:",
                        "/home delete \"player name|id\" \"name\" - Removes the home location with the name 'name' from the player."
                    })
                },
                {
                    "SyntaxCommandHomeAdminTP", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home tp command as follows:",
                        "/home tp \"player name|id\" \"name\" - Teleports you to the home location with the name 'name' from the player."
                    })
                },
                {
                    "SyntaxCommandHomeHomes", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home homes command as follows:",
                        "/home homes \"player name|id\" - Shows you a list of all homes from the player."
                    })
                },
                {
                    "SyntaxCommandListHomes", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /home list command as follows:",
                        "/home list - Shows you a list of all your saved home locations."
                    })
                },
                {
                    "SyntaxCommandTPR", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpr command as follows:",
                        "/tpr \"player name\" - Sends out a teleport request to 'player name'."
                    })
                },
                {
                    "SyntaxCommandTPA", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpa command as follows:",
                        "/tpa - Accepts an incoming teleport request."
                    })
                },
                {
                    "SyntaxCommandTPC", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the /tpc command as follows:",
                        "/tpc - Cancels an teleport request."
                    })
                },
                {
                    "SyntaxConsoleCommandToPos", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the teleport.topos console command as follows:",
                        " > teleport.topos \"player\" x y z"
                    })
                },
                {
                    "SyntaxConsoleCommandToPlayer", string.Join(NewLine, new[]
                    {
                        "A Syntax Error Occurred!",
                        "You can only use the teleport.toplayer console command as follows:",
                        " > teleport.toplayer \"player\" \"target player\""
                    })
                },
                {"LogTeleport", "{0} teleported to {1}."},
                {"LogTeleportPlayer", "{0} teleported {1} to {2}."},
                {"LogTeleportBack", "{0} teleported back to previous location."}
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AdminTP", "Ты телепортировался в {0}!"},
                {"AdminTPTarget", "{0} телепортировался к тебе!"},
                {"AdminTPPlayers", "Ты телепортировался {0} к {1}!"},
                {"AdminTPPlayer", "{0} телепортировался вам {1}!"},
                {"AdminTPPlayerTarget", "{0} телепортированный {1} для вас!"},
                {"AdminTPCoordinates", "Ты телепортировался в {0}!"},
                {"AdminTPTargetCoordinates", "Ты телепортировался {0} к {1}!"},
                {"AdminTPOutOfBounds", "Вы пытались телепортироваться в набор координат за пределами границ карты!"},
                {"AdminTPBoundaries", "X и Z ценности должны быть между -{0} и {0} а Y значение должно быть между -100 и 2000!"},
                {"AdminTPLocation", "Ты телепортировался в {0}!"},
                {"AdminTPLocationSave", "Вы сохранили текущее местоположение!"},
                {"AdminTPLocationRemove", "Вы удалили местоположение {0}!"},
                {"AdminLocationList", "Доступны следующие местоположения,"},
                {"AdminLocationListEmpty", "Вы не сохранили никаких мест!"},
                {"AdminTPBack", "Ты телепортировался обратно на прежнее место!"},
                {"AdminTPBackSave", "Ваше предыдущее местоположение было сохранено, используйте /tpb телепортироваться обратно!"},
                {"AdminTPTargetCoordinatesTarget", "{0} телепортировался вам {1}!"},
                {"AdminTPConsoleTP", "Тебя телепортировали в {0}"},
                {"AdminTPConsoleTPPlayer", "Тебя телепортировали в {0}"},
                {"AdminTPConsoleTPPlayerTarget", "{0} телепортировался к тебе!"},
                {"HomeTP", "Ты телепортировался в свой дом '{0}'!"},
                {"HomeAdminTP", "Ты телепортировался в {0}'s дом '{1}'!"},
                {"HomeSave", "Вы сохранили текущее местоположение в качестве своего дома!"},
                {"HomeNoFoundation", "Вы можете использовать только расположение дома на фундаменте!"},
                {"HomeFoundationNotOwned", "Ты не можешь использовать дом на чужом доме."},
                {"HomeFoundationUnderneathFoundation", "Вы не можете использовать home на фундаменте, который находится под другим фундаментом."},
                {"HomeFoundationNotFriendsOwned", "Вы или друг должны владеть домом, чтобы использовать дом!"},
                {"HomeRemovedInvalid", "Твой дом '{0}' убрали, потому что не на фундаменте или не в собственности!"},
                {"HomeRemovedInsideBlock", "Твой дом '{0}' убрали, потому что внутри фундамент!"},
                {"HomeRemove", "Вы удалили свой дом {0}!"},
                {"HomeDelete", "Вы удалили {0}'s дом '{1}'!"},
                {"HomeList", "Доступны следующие дома,"},
                {"HomeListEmpty", "Вы не спасли ни одного дома!"},
                {"HomeMaxLocations", "Не в состоянии установить свой дом здесь, вы достигли максимума {0} дома!"},
                {"HomeQuota", "Вы установили {0} максимума {1} дома!"},
                {"HomeTPStarted", "Телепортация в ваш дом {0} в {1} секунды!"},
                {"PayToHome", "Стандартная оплата {0} применяется ко всем домашним телепортам!"},
                {"PayToTown", "Стандартная оплата {0} применяется ко всем городским телепортам!"},
                {"PayToTPR", "Стандартная оплата {0} распространяться на всех tprs!"},
                {"HomeTPCooldown", "Ваш телепорт в настоящее время находится на перезарядке. Вам придется подождать {0} для следующего телепорта."},
                {"HomeTPCooldownBypass", "Ваш телепорт в настоящее время был на перезарядке. Вы решили обойти это, заплатив {0} с вашего баланса."},
                {"HomeTPCooldownBypassF", "Ваш телепорт в настоящее время находится на перезарядке. У вас недостаточно средств - {0} - обходить."},
                {"HomeTPCooldownBypassP", "Вы можете заплатить {0} чтобы обойти это охлаждение."},
                {"HomeTPCooldownBypassP2", "Тип /home NAME {0}."},
                {"HomeTPLimitReached", "Вы достигли дневного предела {0} телепорты сегодня!"},
                {"HomeTPAmount", "У вас есть {0} домашние телепорты ушли сегодня!"},
                {"HomesListWiped", "Вы стерли все сохраненные домашние местоположения!"},
                {"HomeTPBuildingBlocked", "Вы не можете установить свой дом, если вам не разрешено строить в этой зоне!"},
                {"HomeTPSwimming", "Вы не можете установить свой дом во время плавания!"},
                {"HomeTPCrafting", "Вы не можете установить свой дом во время крафта!"},
                {"Request", "Вы запросили телепорт на {0}!"},
                {"RequestTarget", "{0} просят телепортироваться к вам! Использовать '/tpa' принять!"},
                {"PendingRequest", "У вас уже есть запрос в ожидании, отменить этот запрос или ждать, пока он не будет принят или тайм-аут!"},
                {"PendingRequestTarget", "Игрок, которому вы хотите телепортироваться, уже имеет ожидающий запрос, повторите попытку позже!"},
                {"NoPendingRequest", "У вас нет запроса на телепортацию!"},
                {"AcceptOnRoof", "Вы не можете принять телепорт, пока вы на потолке, добраться до уровня земли!"},
                {"Accept", "{0} принял ваш запрос на телепортацию! Телепортироваться в {1} секунды!"},
                {"AcceptTarget", "Вы приняли запрос на телепортацию {0}!"},
                {"NotAllowed", "Вы не имеете права использовать эту команду!"},
                {"Success", "Ты телепортировался в {0}!"},
                {"SuccessTarget", "{0} телепортировался к тебе!"},
                {"Cancelled", "Ваш телепорт запрос {0} был отменен!"},
                {"CancelledTarget", "{0} запрос на телепортацию был отменен!"},
                {"TPCancelled", "Твой телепорт был отменен!"},
                {"TPCancelledTarget", "{0} отменен телепорт!"},
                {"TPYouCancelledTarget", "Вы отменили {0} телепортацию!"},
                {"TimedOut", "{0} не ответил на ваш запрос!"},
                {"TimedOutTarget", "Ты не ответил. {0}'s телепортируйте запрос вовремя!"},
                {"TargetDisconnected", "{0} отключился, телепорт отменен!"},
                {"TPRCooldown", "Ваши запросы на телепортацию в настоящее время находятся на перезарядке. Вам придется подождать {0} отправить следующий запрос на телепортацию."},
                {"TPRCooldownBypass", "Ваш запрос на телепортацию был на перезарядке. Вы решили обойти это, заплатив {0} с вашего баланса."},
                {"TPRCooldownBypassF", "Ваш телепорт в настоящее время находится на перезарядке. У вас недостаточно средств - {0} - обходить."},
                {"TPRCooldownBypassP", "Вы можете заплатить {0} чтобы обойти это охлаждение."},
                {"TPMoney", "{0} вычитается из вашего счета!"},
                {"TPNoMoney", "У вас нет {0} в любом аккаунте!"},
                {"TPRCooldownBypassP2", "Тип /tpr {0}."},
                {"TPRCooldownBypassP2a", "Тип /tpr NAME {0}."},
                {"TPRLimitReached", "Вы достигли дневного предела {0} телепорт просит сегодня!"},
                {"TPRAmount", "У вас есть {0} телепорт просит оставить сегодня!"},
                {"TPRTarget", "Ваша цель в настоящее время недоступна!"},
                {"TPDead", "Ты не можешь телепортироваться, будучи мертвым!"},
                {"TPWounded", "Ты не можешь телепортироваться, пока ранен!"},
                {"TPTooCold", "Ты слишком замерз, чтобы телепортироваться!"},
                {"TPTooHot", "Ты слишком горячий, чтобы телепортироваться!"},
                {"TPMounted", "Ты не можешь телепортироваться сидя!"},
                {"TPBuildingBlocked", "Вы не можете телепортироваться в заблокированной зоне здания!"},
                {"TPTargetBuildingBlocked", "Вы не можете телепортироваться в заблокированной зоне здания!"},
                {"TPTargetInsideBlock", "Ты не можешь телепортироваться в Фонд!"},
                {"TPSwimming", "Ты не можешь телепортироваться во время плавания!"},
                {"TPCargoShip", "Ты не можешь телепортироваться с грузового корабля!"},
                {"TPOilRig", "Ты не можешь телепортироваться с нефтяная вышка!"},
                {"TPExcavator", "Ты не можешь телепортироваться с экскаватор!"},
                {"TPHotAirBalloon", "Вы не можете телепортироваться на воздушный шар или с него!"},
                {"TPLift", "Вы не можете телепортироваться в лифте или ковшовом подъемнике!"},
                {"TPBucketLift", "Вы не можете телепортироваться, находясь в ковшовом подъемнике!"},
                {"TPRegLift", "Ты не можешь телепортироваться в лифте!"},
                {"TPSafeZone", "Ты не можешь телепортироваться из безопасной зоны!"},
                {"TPCrafting", "Вы не можете телепортироваться во время крафта!"},
                {"TPBlockedItem", "Вы не можете телепортироваться во время переноски, {0}!"},
                {"TooCloseToMon", "Ты не можешь телепортироваться так близко к {0}!"},
                {"TooCloseToCave", "Ты не можешь телепортироваться так близко к пещере!"},
                {"HomeTooCloseToCave", "Нельзя сидеть дома так близко к пещере!"},
                {"TownTP", "Ты телепортировался в город!"},
                {"TownTPNotSet", "Город в настоящее время не установлен!"},
                {"TownTPDisabled", "Город в настоящее время не включен!"},
                {"TownTPLocation", "Вы установили местоположение города в {0}!"},
                {"TownTPStarted", "Телепортация в город в {0} секунды!"},
                {"TownTPCooldown", "Ваш телепорт в настоящее время находится на перезарядке. Вам придется подождать {0} для следующего телепорта."},
                {"TownTPCooldownBypass", "Ваш запрос на телепортацию был на перезарядке. Вы решили обойти это, заплатив {0} с вашего баланса."},
                {"TownTPCooldownBypassF", "Ваш телепорт в настоящее время находится на перезарядке. У вас недостаточно средств - {0} - обходить."},
                {"TownTPCooldownBypassP", "Вы можете заплатить {0} чтобы обойти это охлаждение."},
                {"TownTPCooldownBypassP2", "Тип /town {0}."},
                {"TownTPLimitReached", "Вы достигли дневного предела {0} телепорты сегодня!"},
                {"TownTPAmount", "У вас есть {0} городские телепорты ушли сегодня!"},
                {"Interrupted", "Ваш телепорт был прерван!"},
                {"InterruptedTarget", "{0}'s телепорт был прерван!"},
                {"Unlimited", "Unlimited"},
                {
                    "TPInfoGeneral", string.Join(NewLine, new[]
                    {
                        "Пожалуйста, укажите модуль, который вы хотите просмотреть.",
                        "Имеющиеся модули, "
                    })
                },
                {
                    "TPHelpGeneral", string.Join(NewLine, new[]
                    {
                        "/tpinfo - Показывает ограничения и кулдауны.",
                        "Пожалуйста, укажите модуль, который вы хотите просмотреть в справке.",
                        "Имеющиеся модули, "
                    })
                },
                {
                    "TPHelpadmintp", string.Join(NewLine, new[]
                    {
                        "Как администратор Вы имеете доступ к следующим командам,",
                        "/tp \"targetplayer\" - Телепортирует себя к целевому игроку.",
                        "/tp \"player\" \"targetplayer\" - Телепортирует игрока к целевому игроку.",
                        "/tp x y z - Телепортирует вас в набор координат.",
                        "/tpl - Показывает список сохраненных местоположений.",
                        "/tpl \"location name\" - Телепортирует вас в сохраненное место.",
                        "/tpsave \"location name\" - Сохраняет текущую позицию в качестве имени местоположения.",
                        "/tpremove \"location name\" - Удаляет местоположение из сохраненного списка.",
                        "/tpb - Телепортирует вас обратно в то место, где вы были до телепортации.",
                        "/home radius \"radius\" - Найти все дома в радиусе.",
                        "/home delete \"player name|id\" \"home name\" - Удалите дом из плеера.",
                        "/home tp \"player name|id\" \"name\" - Телепортирует вас на главную локацию с именем 'name' от игрока.",
                        "/home homes \"player name|id\" - Показывает список всех домов из плеера."
                    })
                },
                {
                    "TPHelphome", string.Join(NewLine, new[]
                    {
                        "С помощью следующих команд вы можете установить свое домашнее местоположение для телепортации обратно в,",
                        "/home add \"name\" - Сохраняет текущую позицию в качестве имени местоположения.",
                        "/home list - Показывает список всех сохраненных местоположений.",
                        "/home remove \"name\" - Удаляет местоположение сохраненных домов.",
                        "/home \"name\" - Телепортирует вас на родное место."
                    })
                },
                {
                    "TPHelptpr", string.Join(NewLine, new[]
                    {
                        "С помощью этих команд вы можете запросить телепортацию к игроку или принять чей-либо запрос,",
                        "/tpr \"player name\" - Отправляет запрос на телепортацию игроку.",
                        "/tpa - Принимает входящий запрос телепорта.",
                        "/tpc - Отменить телепортацию или запрос."
                    })
                },
                {
                    "TPSettingsGeneral", string.Join(NewLine, new[]
                    {
                        "Пожалуйста, укажите модуль, который вы хотите просмотреть настройки.",
                        "Имеющиеся модули,"
                    })
                },
                {
                    "TPSettingshome", string.Join(NewLine, new[]
                    {
                        "В домашней системе включены текущие настройки,",
                        "Время между телепортами, {0}",
                        "Ежедневное количество телепортов, {1}",
                        "Количество сохраненных домашних местоположений, {2}"
                    })
                },
                {
                    "TPSettingstpr", string.Join(NewLine, new[]
                    {
                        "TPR В системе включены текущие настройки,",
                        "Время между телепортами, {0}",
                        "Ежедневное количество телепортов, {1}"
                    })
                },
                {
                    "TPSettingstown", string.Join(NewLine, new[]
                    {
                        "Town В системе включены текущие настройки,",
                        "Время между телепортами, {0}",
                        "Ежедневное количество телепортов, {1}"
                    })
                },
                {"PlayerNotFound", "Не удалось найти указанного игрока, повторите попытку!"},
                {"MultiplePlayers", "Найдено несколько игроков, {0}"},
                {"CantTeleportToSelf", "Ты не можешь телепортироваться к себе!"},
                {"CantTeleportPlayerToSelf", "Вы не можете телепортировать игрока к себе!"},
                {"TeleportPending", "Вы не можете инициировать другой телепорт, пока у вас есть телепорт в ожидании!"},
                {"TeleportPendingTarget", "Ты не можешь просить телепортации у того, кто собирается телепортироваться!"},
                {"LocationExists", "Место с таким именем уже существует в {0}!"},
                {"LocationExistsNearby", "Место с именем {0} уже существует рядом с этой позицией!"},
                {"LocationNotFound", "Не мог найти место с таким именем!"},
                {"NoPreviousLocationSaved", "Предыдущее местоположение не сохранено!"},
                {"HomeExists", "Вы уже сохранили расположение дома под этим именем!"},
                {"HomeExistsNearby", "Расположение дома с именем {0} уже существует рядом с этой позицией!"},
                {"HomeNotFound", "Не мог найти свой дом с таким именем!"},
                {"InvalidCoordinates", "Введенные вами координаты недействительны!"},
                {"InvalidHelpModule", "Неверный модуль поставляется!"},
                {"InvalidCharacter", "Вы использовали недопустимый символ, пожалуйста, ограничьте себя буквами от А до Я и цифрами."},
                {
                    "SyntaxCommandTP", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tp команда следующим образом,",
                        "/tp \"targetplayer\" - Телепортирует себя к целевому игроку.",
                        "/tp \"player\" \"targetplayer\" - Телепортирует игрока к целевому игроку.",
                        "/tp x y z - Телепортирует вас в набор координат.",
                        "/tp \"player\" x y z - Телепортирует игрока в набор координат."
                    })
                },
                {
                    "SyntaxCommandTPL", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpl команда следующим образом,",
                        "/tpl - Показывает список сохраненных местоположений.",
                        "/tpl \"location name\" - Телепортирует вас в сохраненное место."
                    })
                },
                {
                    "SyntaxCommandTPSave", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpsave команда следующим образом,",
                        "/tpsave \"location name\" - Сохраняет текущую позицию как 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPRemove", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpremove команда следующим образом,",
                        "/tpremove \"location name\" - Удаляет расположение с именем 'location name'."
                    })
                },
                {
                    "SyntaxCommandTPN", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpn команда следующим образом,",
                        "/tpn \"targetplayer\" - Телепортирует себя на расстояние по умолчанию позади целевого игрока.",
                        "/tpn \"targetplayer\" \"distance\" - Телепортирует вас на указанное расстояние позади целевого игрока."
                    })
                },
                {
                    "SyntaxCommandSetHome", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home add команда следующим образом,",
                        "/home add \"name\" - Сохранение текущего местоположения в качестве вашего дома с именем 'name'."
                    })
                },
                {
                    "SyntaxCommandRemoveHome", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home remove команда следующим образом,",
                        "/home remove \"name\" - Удаляет расположение дома с именем 'name'."
                    })
                },
                {
                    "SyntaxCommandHome", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home команда следующим образом,",
                        "/home \"name\" - Телепортирует себя в свой дом с именем 'name'.",
                        "/home \"name\" pay - Телепортирует себя в свой дом с именем 'name', избежать перезарядки, заплатив за это.",
                        "/home add \"name\" - Сохранение текущего местоположения в качестве вашего дома с именем 'name'.",
                        "/home list - Показывает список всех сохраненных домашних местоположений.",
                        "/home remove \"name\" - Удаляет расположение дома с именем 'name'."
                    })
                },
                {
                    "SyntaxCommandHomeAdmin", string.Join(NewLine, new[]
                    {
                        "/home radius \"radius\" - Показывает список всех домов в radius(10).",
                        "/home delete \"player name|id\" \"name\" - Удаляет расположение дома с именем 'name' от игрока.",
                        "/home tp \"player name|id\" \"name\" - Телепортирует вас на главную локацию с именем 'name' от игрока.",
                        "/home homes \"player name|id\" - Показывает список всех домов из плеера."
                    })
                },
                {
                    "SyntaxCommandTown", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /town команда следующим образом,",
                        "/town - Teleports yourself to town.",
                        "/town pay - Teleports yourself to town, paying the penalty."
                    })
                },
                {
                    "SyntaxCommandTownAdmin", string.Join(NewLine, new[]
                    {
                        "/town set - Сохраняет текущее местоположение как town."
                    })
                },
                {
                    "SyntaxCommandHomeDelete", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home delete команда следующим образом,",
                        "/home delete \"player name|id\" \"name\" - Удаляет расположение дома с именем 'name' от игрока."
                    })
                },
                {
                    "SyntaxCommandHomeAdminTP", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home tp команда следующим образом,",
                        "/home tp \"player name|id\" \"name\" - Телепортирует вас на главную локацию с именем 'name' от игрока."
                    })
                },
                {
                    "SyntaxCommandHomeHomes", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home homes команда следующим образом,",
                        "/home homes \"player name|id\" - Показывает список всех домов из плеера."
                    })
                },
                {
                    "SyntaxCommandListHomes", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /home list команда следующим образом,",
                        "/home list - Показывает список всех сохраненных домашних местоположений."
                    })
                },
                {
                    "SyntaxCommandTPR", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpr команда следующим образом,",
                        "/tpr \"player name\" - Отправляет запрос на телепортацию 'player name'."
                    })
                },
                {
                    "SyntaxCommandTPA", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpa команда следующим образом,",
                        "/tpa - Принимает входящий запрос телепорта."
                    })
                },
                {
                    "SyntaxCommandTPC", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только /tpc команда следующим образом,",
                        "/tpc - Отменяет телепорт запросу."
                    })
                },
                {
                    "SyntaxConsoleCommandToPos", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только teleport.topos console команда следующим образом,",
                        " > teleport.topos \"player\" x y z"
                    })
                },
                {
                    "SyntaxConsoleCommandToPlayer", string.Join(NewLine, new[]
                    {
                        "Произошла Синтаксическая Ошибка!",
                        "Вы можете использовать только teleport.toplayer console команда следующим образом,",
                        " > teleport.toplayer \"player\" \"target player\""
                    })
                },
                {"LogTeleport", "{0} телепортироваться {1}."},
                {"LogTeleportPlayer", "{0} телепортированный {1} к {2}."},
                {"LogTeleportBack", "{0} телепортировался на прежнее место."}
            }, this, "ru");
        }

        private void Loaded()
        {
            Config.Settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            Config.Settings.Converters = new JsonConverter[] { new UnityVector3Converter() };
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch(Exception)
            {
                Puts("Corrupt config, loading default...");
                LoadDefaultConfig();
            }

            if(configData.Version != Version)
            {
                if(configData.Home.VIPHomesLimits == null)
                {
                    configData.Home.VIPHomesLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Home.VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Home.VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.TPR.VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.TPR.VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Town.VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Town.VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Outpost.VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Outpost.VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Bandit.VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Bandit.VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                }
                if(configData.Home.VIPCountdowns == null)
                {
                    configData.Home.VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.TPR.VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Town.VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Outpost.VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                    configData.Bandit.VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } };
                }
                if(configData.Version <= new VersionNumber(1, 0, 4))
                {
                    configData.Home.AllowAboveFoundation = true;
                }
                if(configData.Version < new VersionNumber(1, 0, 14))
                {
                    configData.Home.UsableIntoBuildingBlocked = true;
                    configData.TPR.UsableIntoBuildingBlocked = true;
                }
                if(configData.Version < new VersionNumber(1, 0, 58))
                {
                    configData.Settings.InterruptTPOnMounted = true;
                    configData.Settings.InterruptTPOnSwimming = true;
                }
                if(configData.Version < new VersionNumber(1, 0, 69))
                {
                    configData.Settings.InterruptAboveWater = false;
                }
                if(configData.Version < new VersionNumber(1, 0, 70))
                {
                    configData.Settings.AutoGenOutpost = false;
                    configData.Settings.AutoGenBandit  = false;
                    configData.Settings.OutpostEnabled = false;
                    configData.Settings.BanditEnabled  = false;

                    configData.Outpost = new TownData
                    {
                        Cooldown = 600,
                        Countdown = 15,
                        DailyLimit = 5,
                        VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                        VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                        VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } }
                    };
                    configData.Bandit = new TownData
                    {
                        Cooldown = 600,
                        Countdown = 15,
                        DailyLimit = 5,
                        VIPDailyLimits = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                        VIPCooldowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } },
                        VIPCountdowns = new Dictionary<string, int> { { ConfigDefaultPermVip, 5 } }
                    };
                }
                if(configData.Version < new VersionNumber(1, 0, 77))
                {
                    configData.Settings.StrictFoundationCheck = false;
                }
                if(configData.Version < new VersionNumber(1, 0, 80))
                {
                    configData.Settings.InterruptTPOnHostile = false;
                }
                if(configData.Settings.MaximumTemp < 1)
                {
                    configData.Settings.MaximumTemp = 40f;
                }
                if(configData.Settings.DefaultMonumentSize < 1)
                {
                    configData.Settings.DefaultMonumentSize = 50f;
                }
                if(configData.Settings.CaveDistanceSmall < 1)
                {
                    configData.Settings.CaveDistanceSmall = 40f;
                }
                if(configData.Settings.CaveDistanceMedium < 1)
                {
                    configData.Settings.CaveDistanceMedium = 60f;
                }
                if(configData.Settings.CaveDistanceLarge < 1)
                {
                    configData.Settings.CaveDistanceLarge = 100f;
                }
                if(configData.GameVersion == null)
                {
                    configData.GameVersion = new GameVersionData();
                }
                if(configData.GameVersion.Save < 1)
                {
                    configData.GameVersion.Network = Convert.ToInt32(Protocol.network);
                    configData.GameVersion.Save = Convert.ToInt32(Protocol.save);
                    configData.GameVersion.Level = ConVar.Server.level;
                    configData.GameVersion.LevelURL = ConVar.Server.levelurl;
                    configData.GameVersion.WorldSize = ConVar.Server.worldsize;
                    configData.GameVersion.Seed = ConVar.Server.seed;
                    configData.Settings.WipeOnUpgradeOrChange = false;
                }
                configData.Version = Version;
                Config.WriteObject(configData, true);
            }
            dataAdmin = GetFile(nameof(RTeleportation) + "Admin");
            Admin = dataAdmin.ReadObject<Dictionary<ulong, AdminData>>();
            dataHome = GetFile(nameof(RTeleportation) + "Home");
            Home = dataHome.ReadObject<Dictionary<ulong, HomeData>>();

            dataTPR = GetFile(nameof(RTeleportation) + "TPR");
            TPR = dataTPR.ReadObject<Dictionary<ulong, TeleportData>>();
            dataTown = GetFile(nameof(RTeleportation) + "Town");
            Town = dataTown.ReadObject<Dictionary<ulong, TeleportData>>();
            dataOutpost = GetFile(nameof(RTeleportation) + "Outpost");
            Outpost = dataOutpost.ReadObject<Dictionary<ulong, TeleportData>>();
            dataBandit = GetFile(nameof(RTeleportation) + "Bandit");
            Bandit = dataBandit.ReadObject<Dictionary<ulong, TeleportData>>();
            cmd.AddConsoleCommand("teleport.toplayer", this, ccmdTeleport);
            cmd.AddConsoleCommand("teleport.topos", this, ccmdTeleport);
            permission.RegisterPermission(PermDeleteHome, this);
            permission.RegisterPermission(PermHome, this);
            permission.RegisterPermission(PermHomeHomes, this);
            permission.RegisterPermission(PermImportHomes, this);
            permission.RegisterPermission(PermRadiusHome, this);
            permission.RegisterPermission(PermTp, this);
            permission.RegisterPermission(PermTpB, this);
            permission.RegisterPermission(PermTpR, this);
            permission.RegisterPermission(PermTpConsole, this);
            permission.RegisterPermission(PermTpHome, this);
            permission.RegisterPermission(PermTpTown, this);
            permission.RegisterPermission(PermTpOutpost, this);
            permission.RegisterPermission(PermTpBandit, this);
            permission.RegisterPermission(PermTpN, this);
            permission.RegisterPermission(PermTpL, this);
            permission.RegisterPermission(PermTpRemove, this);
            permission.RegisterPermission(PermTpSave, this);
            permission.RegisterPermission(PermWipeHomes, this);
            permission.RegisterPermission(PermCraftHome, this);
            permission.RegisterPermission(PermCraftTown, this);
            permission.RegisterPermission(PermCraftOutpost, this);
            permission.RegisterPermission(PermCraftBandit, this);
            permission.RegisterPermission(PermCraftTpR, this);
            foreach (string key in configData.Home.VIPCooldowns.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.Home.VIPCountdowns.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.Home.VIPDailyLimits.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.Home.VIPHomesLimits.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.TPR.VIPCooldowns.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.TPR.VIPCountdowns.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.TPR.VIPDailyLimits.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.Town.VIPCooldowns.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.Town.VIPCountdowns.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            foreach (string key in configData.Town.VIPDailyLimits.Keys)
            {
                if (!permission.PermissionExists(key, this))
                {
                    permission.RegisterPermission(key, this);
                }
            }

            FindMonuments();
        }

        private DynamicConfigFile GetFile(string name)
        {
            DynamicConfigFile file = Interface.Oxide.DataFileSystem.GetFile(name);
            file.Settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            file.Settings.Converters = new JsonConverter[] { new UnityVector3Converter(), new CustomComparerDictionaryCreationConverter<string>(StringComparer.OrdinalIgnoreCase) };
            return file;
        }

        private void OnServerInitialized()
        {
            boundary = TerrainMeta.Size.x / 2;
            CheckPerms(configData.Home.VIPHomesLimits);
            CheckPerms(configData.Home.VIPDailyLimits);
            CheckPerms(configData.Home.VIPCooldowns);
            CheckPerms(configData.TPR.VIPDailyLimits);
            CheckPerms(configData.TPR.VIPCooldowns);
            CheckPerms(configData.Town.VIPDailyLimits);
            CheckPerms(configData.Town.VIPCooldowns);
            CheckPerms(configData.Outpost.VIPDailyLimits);
            CheckPerms(configData.Outpost.VIPCooldowns);
            CheckPerms(configData.Bandit.VIPDailyLimits);
            CheckPerms(configData.Bandit.VIPCooldowns);

            foreach(KeyValuePair<string, string> item in configData.Settings.BlockedItems)
            {
                ItemDefinition definition = ItemManager.FindItemDefinition(item.Key);
                if(definition == null)
                {
                    DoLog($"Blocked item not found: {item.Key}");
                    continue;
                }
                ReverseBlockedItems[definition.itemid] = item.Value;
            }

            configData.GameVersion.Network   = Convert.ToInt32(Protocol.network);
            configData.GameVersion.Save      = Convert.ToInt32(Protocol.save);
            configData.GameVersion.Level     = ConVar.Server.level;
            configData.GameVersion.LevelURL  = ConVar.Server.levelurl;
            configData.GameVersion.WorldSize = ConVar.Server.worldsize;
            configData.GameVersion.Seed      = ConVar.Server.seed;

            Config.WriteObject(configData, true);
        }

        private void OnNewSave(string strFilename)
        {
            if(configData.Settings.WipeOnUpgradeOrChange)
            {
                DoLog("Rust was upgraded or map changed - clearing homes and town!");
                Home.Clear();
                changedHome = true;
                configData.Town.Location = default(Vector3);
                FindMonuments();
            }
            else
            {
                DoLog("Rust was upgraded or map changed - homes, town, outpost, and bandit locations may be invalid!");
            }
        }

        private void OnServerSave()
        {
            SaveTeleportsAdmin();
            SaveTeleportsHome();
            SaveTeleportsTPR();
            SaveTeleportsTown();
            SaveTeleportsOutpost();
            SaveTeleportsBandit();
        }

        private void OnServerShutdown() => OnServerSave();

        private void Unload() => OnServerSave();

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            BasePlayer player = entity.ToPlayer();
            if(player == null || hitinfo == null)
            {
                return;
            }

            if (hitinfo.damageTypes.Has(DamageType.Fall) && teleporting.Contains(player.userID))
            {
                hitinfo.damageTypes = new DamageTypeList();
                teleporting.Remove(player.userID);
            }
            TeleportTimer teleportTimer;
            if(!TeleportTimers.TryGetValue(player.userID, out teleportTimer))
            {
                return;
            }

            DamageType major = hitinfo.damageTypes.GetMajorityDamageType();
            NextTick(() =>
            {
                if(hitinfo.damageTypes.Total() <= 0)
                {
                    return;
                }

                if (!configData.Settings.InterruptTPOnHurt)
                {
                    return;
                }

                // 1.0.84 new checks for cold/heat based on major damage for the player
                if (major == DamageType.Cold && configData.Settings.InterruptTPOnCold)
                {
                    if(player.metabolism.temperature.value <= configData.Settings.MinimumTemp)
                    {
                        PrintMsgL(teleportTimer.OriginPlayer, "TPTooCold");
                        if(teleportTimer.TargetPlayer != null)
                        {
                            PrintMsgL(teleportTimer.TargetPlayer, "InterruptedTarget", teleportTimer.OriginPlayer.displayName);
                        }
                        teleportTimer.Timer.Destroy();
                        TeleportTimers.Remove(player.userID);
                    }
                }
                else if(major == DamageType.Heat && configData.Settings.InterruptTPOnHot)
                {
                    if(player.metabolism.temperature.value >= configData.Settings.MaximumTemp)
                    {
                        PrintMsgL(teleportTimer.OriginPlayer, "TPTooHot");
                        if(teleportTimer.TargetPlayer != null)
                        {
                            PrintMsgL(teleportTimer.TargetPlayer, "InterruptedTarget", teleportTimer.OriginPlayer.displayName);
                        }
                        teleportTimer.Timer.Destroy();
                        TeleportTimers.Remove(player.userID);
                    }
                }
                else
                {
                    PrintMsgL(teleportTimer.OriginPlayer, "Interrupted");
                    if(teleportTimer.TargetPlayer != null)
                    {
                        PrintMsgL(teleportTimer.TargetPlayer, "InterruptedTarget", teleportTimer.OriginPlayer.displayName);
                    }
                    teleportTimer.Timer.Destroy();
                    TeleportTimers.Remove(player.userID);
                }
            });
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if(teleporting.Contains(player.userID))
            {
                timer.Once(3, () => teleporting.Remove(player.userID));
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            Timer reqTimer;
            if (PendingRequests.TryGetValue(player.userID, out reqTimer))
            {
                BasePlayer originPlayer = PlayersRequests[player.userID];
                PrintMsgL(originPlayer, "RequestTargetOff");
                reqTimer.Destroy();
                PendingRequests.Remove(player.userID);
                PlayersRequests.Remove(player.userID);
                PlayersRequests.Remove(originPlayer.userID);
            }
            TeleportTimer teleportTimer;
            if (TeleportTimers.TryGetValue(player.userID, out teleportTimer))
            {
                teleportTimer.Timer.Destroy();
                TeleportTimers.Remove(player.userID);
            }
            teleporting.Remove(player.userID);
        }

        private void SaveTeleportsAdmin()
        {
            if (Admin == null || !changedAdmin)
            {
                return;
            }

            dataAdmin.WriteObject(Admin);
            changedAdmin = false;
        }

        private void SaveTeleportsHome()
        {
            if (Home == null || !changedHome)
            {
                return;
            }

            dataHome.WriteObject(Home);
            changedHome = false;
        }

        private void SaveTeleportsTPR()
        {
            if (TPR == null || !changedTPR)
            {
                return;
            }

            dataTPR.WriteObject(TPR);
            changedTPR = false;
        }

        private void SaveTeleportsTown()
        {
            if (Town == null || !changedTown)
            {
                return;
            }

            dataTown.WriteObject(Town);
            changedTown = false;
        }

        private void SaveTeleportsOutpost()
        {
            if (Outpost == null || !changedOutpost)
            {
                return;
            }

            dataOutpost.WriteObject(Outpost);
            changedOutpost = false;
        }

        private void SaveTeleportsBandit()
        {
            if (Bandit == null || !changedBandit)
            {
                return;
            }

            dataBandit.WriteObject(Bandit);
            changedBandit = false;
        }

        private void SaveLocation(BasePlayer player)
        {
            if (!IsAllowed(player, PermTpB))
            {
                return;
            }

            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData))
            {
                Admin[player.userID] = adminData = new AdminData();
            }

            adminData.PreviousLocation = player.transform.position;
            changedAdmin = true;
            PrintMsgL(player, "AdminTPBackSave");
        }

        private string RandomString()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            List<char> charList = chars.ToList();

            string random = "";

            for (int i = 0; i <= UnityEngine.Random.Range(5, 10); i++)
            {
                random += charList[UnityEngine.Random.Range(0, charList.Count - 1)];
            }

            return random;
        }
        // Modified from MonumentFinder.cs by PsychoTea
        private void FindMonuments()
        {
            bool setextra = false;
            Vector3 extents = Vector3.zero;
            float realWidth = 0f;
            string name = null;
            //foreach(MonumentInfo monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            foreach(MonumentInfo monument in BaseNetworkable.serverEntities.OfType<MonumentInfo>())
            {
                if(monument.name.Contains("power_sub"))
                {
                    continue;
                }

                realWidth = 0f;
                name = null;

                if(monument.name == "OilrigAI")
                {
                    name = "Small Oilrig";
                    realWidth = 100f;
                }
                else if(monument.name == "OilrigAI2")
                {
                    name = "Large Oilrig";
                    realWidth = 200f;
                }
                else
                {
                    name = Regex.Match(monument.name, @"\w{6}\/(.+\/)(.+)\.(.+)").Groups[2].Value.Replace("_", " ").Replace(" 1", "").Titleize();
                }
                if(monPos.ContainsKey(name))
                {
                    continue;
                }

                if (cavePos.ContainsKey(name))
                {
                    name += RandomString();
                }

                extents = monument.Bounds.extents;
                DoLog($"Found {name}, extents {extents.ToString()}");

                if(realWidth > 0f)
                {
                    extents.z = realWidth;
                    DoLog($"  corrected to {extents.ToString()}");
                }

                if(monument.name.Contains("cave"))
                {
                    DoLog("  Adding to cave list");
                    cavePos.Add(name, monument.transform.position);
                }
                else if(monument.name.Contains("compound") && configData.Settings.AutoGenOutpost)
                {
                    DoLog("  Adding Outpost target");
                    List<BaseEntity> ents = new List<BaseEntity>();
                    Vis.Entities<BaseEntity>(monument.transform.position, 50, ents);
                    foreach(BaseEntity entity in ents)
                    {
                        if(entity.PrefabName.Contains("piano"))
                        {
                            configData.Outpost.Location = entity.transform.position + new Vector3(1f, 0.1f, 1f);
                            setextra = true;
                        }
                    }
                }
                else if(monument.name.Contains("bandit") && configData.Settings.AutoGenBandit)
                {
                    DoLog("  Adding BanditTown target");
                    List<BaseEntity> ents = new List<BaseEntity>();
                    Vis.Entities<BaseEntity>(monument.transform.position, 50, ents);
                    foreach(BaseEntity entity in ents)
                    {
                        if(entity.PrefabName.Contains("workbench"))
                        {
                            configData.Bandit.Location = Vector3.Lerp(monument.transform.position, entity.transform.position, 0.45f) + new Vector3(0, 1.5f, 0);
                            setextra = true;
                            break;
                        }
                    }
                }
                else
                {
                    if(extents.z < 1)
                    {
                        extents.z = configData.Settings.DefaultMonumentSize;
                    }
                    monPos.Add(name, monument.transform.position);
                    monSize.Add(name, extents);
                    DoLog($"Adding Monument: {name}, pos: {monument.transform.position.ToString()}, size: {extents.ToString()}");
                }
            }
            monPos.OrderBy(x => x.Key);
            monSize.OrderBy(x => x.Key);
            cavePos.OrderBy(x => x.Key);
            if(setextra)
            {
                // Write config so that the outpost and bandit autogen locations are available immediately.
                Config.WriteObject(configData, true);
            }
        }

        [ChatCommand("tp")]
        private void cmdChatTeleport(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermTp))
            {
                return;
            }

            BasePlayer target;
            float x, y, z;
            switch (args.Length)
            {
                case 1:
                    target = FindPlayersSingle(args[0], player);
                    if (target == null)
                    {
                        return;
                    }

                    if (target == player)
                    {
                        if (!configData.debug)
                        {
                            PrintMsgL(player, "CantTeleportToSelf");
                            return;
                        }
                        DoLog("Debug mode - allowing self teleport.");
                    }
//                    if(player.isMounted)
//                        player.DismountObject();
                    TeleportToPlayer(player, target);
                    PrintMsgL(player, "AdminTP", target.displayName);
                    DoLog(_("LogTeleport", null, player.displayName, target.displayName));
                    if (configData.Admin.AnnounceTeleportToTarget)
                    {
                        PrintMsgL(target, "AdminTPTarget", player.displayName);
                    }

                    break;
                case 2:
                    BasePlayer origin = FindPlayersSingle(args[0], player);
                    if (origin == null)
                    {
                        return;
                    }

                    target = FindPlayersSingle(args[1], player);
                    if (target == null)
                    {
                        return;
                    }

                    if (target == origin)
                    {
                        PrintMsgL(player, "CantTeleportPlayerToSelf");
                        return;
                    }
                    TeleportToPlayer(origin, target);
                    PrintMsgL(player, "AdminTPPlayers", origin.displayName, target.displayName);
                    PrintMsgL(origin, "AdminTPPlayer", player.displayName, target.displayName);
                    if (configData.Admin.AnnounceTeleportToTarget)
                    {
                        PrintMsgL(target, "AdminTPPlayerTarget", player.displayName, origin.displayName);
                    }

                    DoLog(_("LogTeleportPlayer", null, player.displayName, origin.displayName, target.displayName));
                    break;
                case 3:
                    if (!float.TryParse(args[0], out x) || !float.TryParse(args[1], out y) || !float.TryParse(args[2], out z))
                    {
                        PrintMsgL(player, "InvalidCoordinates");
                        return;
                    }
                    if (!CheckBoundaries(x, y, z))
                    {
                        PrintMsgL(player, "AdminTPOutOfBounds");
                        PrintMsgL(player, "AdminTPBoundaries", boundary);
                        return;
                    }
                    TeleportToPosition(player, x, y, z);
                    PrintMsgL(player, "AdminTPCoordinates", player.transform.position);
                    DoLog(_("LogTeleport", null, player.displayName, player.transform.position));
                    break;
                case 4:
                    target = FindPlayersSingle(args[0], player);
                    if (target == null)
                    {
                        return;
                    }

                    if (!float.TryParse(args[0], out x) || !float.TryParse(args[1], out y) || !float.TryParse(args[2], out z))
                    {
                        PrintMsgL(player, "InvalidCoordinates");
                        return;
                    }
                    if (!CheckBoundaries(x, y, z))
                    {
                        PrintMsgL(player, "AdminTPOutOfBounds");
                        PrintMsgL(player, "AdminTPBoundaries", boundary);
                        return;
                    }
                    TeleportToPosition(target, x, y, z);
                    if (player == target)
                    {
                        PrintMsgL(player, "AdminTPCoordinates", player.transform.position);
                        DoLog(_("LogTeleport", null, player.displayName, player.transform.position));
                    }
                    else
                    {
                        PrintMsgL(player, "AdminTPTargetCoordinates", target.displayName, player.transform.position);
                        if (configData.Admin.AnnounceTeleportToTarget)
                        {
                            PrintMsgL(target, "AdminTPTargetCoordinatesTarget", player.displayName, player.transform.position);
                        }

                        DoLog(_("LogTeleportPlayer", null, player.displayName, target.displayName, player.transform.position));
                    }
                    break;
                default:
                    PrintMsgL(player, "SyntaxCommandTP");
                    break;
            }
        }

        [ChatCommand("tpn")]
        private void cmdChatTeleportNear(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermTpN))
            {
                return;
            }

            switch (args.Length)
            {
                case 1:
                case 2:
                    BasePlayer target = FindPlayersSingle(args[0], player);
                    if (target == null)
                    {
                        return;
                    }

                    if (target == player)
                    {
                        if (!configData.debug)
                        {
                            PrintMsgL(player, "CantTeleportToSelf");
                            return;
                        }
                        DoLog("Debug mode - allowing self teleport.");
                    }
                    int distance;
                    if (args.Length != 2 || !int.TryParse(args[1], out distance))
                    {
                        distance = configData.Admin.TeleportNearDefaultDistance;
                    }

                    float x = UnityEngine.Random.Range(-distance, distance);
                    float z = (float)System.Math.Sqrt(System.Math.Pow(distance, 2) - System.Math.Pow(x, 2));
                    Vector3 destination = target.transform.position;
                    destination.x -= x;
                    destination.z -= z;
                    Teleport(player, GetGroundBuilding(destination));
                    PrintMsgL(player, "AdminTP", target.displayName);
                    DoLog(_("LogTeleport", null, player.displayName, target.displayName));
                    if (configData.Admin.AnnounceTeleportToTarget)
                    {
                        PrintMsgL(target, "AdminTPTarget", player.displayName);
                    }

                    break;
                default:
                    PrintMsgL(player, "SyntaxCommandTPN");
                    break;
            }
        }

        [ChatCommand("tpl")]
        private void cmdChatTeleportLocation(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermTpL))
            {
                return;
            }

            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData) || adminData.Locations.Count == 0)
            {
                PrintMsgL(player, "AdminLocationListEmpty");
                return;
            }
            switch (args.Length)
            {
                case 0:
                    PrintMsgL(player, "AdminLocationList");
                    foreach (KeyValuePair<string, Vector3> location in adminData.Locations)
                    {
                        PrintMsgL(player, $"{location.Key} {location.Value}");
                    }

                    break;
                case 1:
                    Vector3 loc;
                    if (!adminData.Locations.TryGetValue(args[0], out loc))
                    {
                        PrintMsgL(player, "LocationNotFound");
                        return;
                    }
                    Teleport(player, loc);
                    PrintMsgL(player, "AdminTPLocation", args[0]);
                    break;
                default:
                    PrintMsgL(player, "SyntaxCommandTPL");
                    break;
            }
        }

        [ChatCommand("tpsave")]
        private void cmdChatSaveTeleportLocation(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermTpSave))
            {
                return;
            }

            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandTPSave");
                return;
            }
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData))
            {
                Admin[player.userID] = adminData = new AdminData();
            }

            Vector3 location;
            if (adminData.Locations.TryGetValue(args[0], out location))
            {
                PrintMsgL(player, "LocationExists", location);
                return;
            }
            Vector3 positionCoordinates = player.transform.position;
            foreach (KeyValuePair<string, Vector3> loc in adminData.Locations)
            {
                if (Vector3.Distance(positionCoordinates, loc.Value) < configData.Admin.LocationRadius)
                {
                    PrintMsgL(player, "LocationExistsNearby", loc.Key);
                    return;
                }
            }
            adminData.Locations[args[0]] = positionCoordinates;
            PrintMsgL(player, "AdminTPLocationSave");
            changedAdmin = true;
        }

        [ChatCommand("tpremove")]
        private void cmdChatRemoveTeleportLocation(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermTpRemove))
            {
                return;
            }

            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandTPRemove");
                return;
            }
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData) || adminData.Locations.Count == 0)
            {
                PrintMsgL(player, "AdminLocationListEmpty");
                return;
            }
            if (adminData.Locations.Remove(args[0]))
            {
                PrintMsgL(player, "AdminTPLocationRemove", args[0]);
                changedAdmin = true;
                return;
            }
            PrintMsgL(player, "LocationNotFound");
        }

        [ChatCommand("tpb")]
        private void cmdChatTeleportBack(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermTpB))
            {
                return;
            }

            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandTPB");
                return;
            }
            AdminData adminData;
            if (!Admin.TryGetValue(player.userID, out adminData) || adminData.PreviousLocation == default(Vector3))
            {
                PrintMsgL(player, "NoPreviousLocationSaved");
                return;
            }

            Teleport(player, adminData.PreviousLocation);
            adminData.PreviousLocation = default(Vector3);
            changedAdmin = true;
            PrintMsgL(player, "AdminTPBack");
            DoLog(_("LogTeleportBack", null, player.displayName));
        }

        [ChatCommand("sethome")]
        private void cmdChatSetHome(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, PermHome))
            {
                return;
            }

            if (!configData.Settings.HomesEnabled)
            {
                return;
            }

            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandSetHome");
                return;
            }
            string err = CheckPlayer(player, false, CanCraftHome(player), true, "home");
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            if (!player.CanBuild())
            {
                PrintMsgL(player, "HomeTPBuildingBlocked");
                return;
            }
            if (!args[0].All(char.IsLetterOrDigit))
            {
                PrintMsgL(player, "InvalidCharacter");
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData))
            {
                Home[player.userID] = homeData = new HomeData();
            }

            int limit = GetHigher(player, configData.Home.VIPHomesLimits, configData.Home.HomesLimit);
            if (homeData.Locations.Count >= limit)
            {
                PrintMsgL(player, "HomeMaxLocations", limit);
                return;
            }
            Vector3 location;
            if (homeData.Locations.TryGetValue(args[0], out location))
            {
                PrintMsgL(player, "HomeExists", location);
                return;
            }
            Vector3 positionCoordinates = player.transform.position;
            foreach (KeyValuePair<string, Vector3> loc in homeData.Locations)
            {
                if (Vector3.Distance(positionCoordinates, loc.Value) < configData.Home.LocationRadius)
                {
                    PrintMsgL(player, "HomeExistsNearby", loc.Key);
                    return;
                }
            }
            err = CanPlayerTeleport(player);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }

            if (player.IsAdmin)
            {
                player.SendConsoleCommand("ddraw.sphere", 60f, Color.blue, GetGround(positionCoordinates), 2.5f);
            }

            err = CheckFoundation(player.userID, positionCoordinates);
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            err = CheckInsideBlock(positionCoordinates);
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            homeData.Locations[args[0]] = positionCoordinates;
            changedHome = true;
            PrintMsgL(player, "HomeSave");
            PrintMsgL(player, "HomeQuota", homeData.Locations.Count, limit);
        }

        [ChatCommand("removehome")]
        private void cmdChatRemoveHome(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, PermHome))
            {
                return;
            }

            if (!configData.Settings.HomesEnabled)
            {
                return;
            }

            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandRemoveHome");
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData) || homeData.Locations.Count == 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            if (homeData.Locations.Remove(args[0]))
            {
                changedHome = true;
                PrintMsgL(player, "HomeRemove", args[0]);
            }
            else
            {
                PrintMsgL(player, "HomeNotFound");
            }
        }

        [ChatCommand("home")]
        private void cmdChatHome(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, PermHome))
            {
                return;
            }

            if (!configData.Settings.HomesEnabled)
            {
                return;
            }

            if (args.Length == 0)
            {
                PrintMsgL(player, "SyntaxCommandHome");
                if (IsAllowed(player))
                {
                    PrintMsgL(player, "SyntaxCommandHomeAdmin");
                }

                return;
            }
            switch (args[0].ToLower())
            {
                case "add":
                    cmdChatSetHome(player, command, args.Skip(1).ToArray());
                    break;
                case "list":
                    cmdChatListHome(player, command, args.Skip(1).ToArray());
                    break;
                case "remove":
                    cmdChatRemoveHome(player, command, args.Skip(1).ToArray());
                    break;
                case "radius":
                    cmdChatHomeRadius(player, command, args.Skip(1).ToArray());
                    break;
                case "delete":
                    cmdChatHomeDelete(player, command, args.Skip(1).ToArray());
                    break;
                case "tp":
                    cmdChatHomeAdminTP(player, command, args.Skip(1).ToArray());
                    break;
                case "homes":
                    cmdChatHomeHomes(player, command, args.Skip(1).ToArray());
                    break;
                case "wipe":
                    cmdChatWipeHomes(player, command, args.Skip(1).ToArray());
                    break;
                default:
                    cmdChatHomeTP(player, command, args);
                    break;
            }
        }

        [ChatCommand("radiushome")]
        private void cmdChatHomeRadius(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermRadiusHome))
            {
                return;
            }

            float radius;
            if (args.Length != 1 || !float.TryParse(args[0], out radius))
            {
                radius = 10;
            }

            bool found = false;
            foreach (KeyValuePair<ulong, HomeData> homeData in Home)
            {
                List<string> toRemove = new List<string>();
                string target = RustCore.FindPlayerById(homeData.Key)?.displayName ?? homeData.Key.ToString();
                foreach (KeyValuePair<string, Vector3> location in homeData.Value.Locations)
                {
                    if (Vector3.Distance(player.transform.position, location.Value) <= radius)
                    {
                        if(CheckFoundation(homeData.Key, location.Value) != null)
                        {
                            toRemove.Add(location.Key);
                            continue;
                        }
                        BuildingBlock entity = GetFoundationOwned(location.Value, homeData.Key);
                        if (entity == null)
                        {
                            continue;
                        }

                        player.SendConsoleCommand("ddraw.text", 30f, Color.blue, entity.CenterPoint() + new Vector3(0, .5f), $"<size=20>{target} - {location.Key} {location.Value}</size>");
                        DrawBox(player, entity.CenterPoint(), entity.transform.rotation, entity.bounds.size);
                        PrintMsg(player, $"{target} - {location.Key} {location.Value}");
                        found = true;
                    }
                }
                foreach (string loc in toRemove)
                {
                    homeData.Value.Locations.Remove(loc);
                    changedHome = true;
                }
            }
            if (!found)
            {
                PrintMsgL(player, "HomeNoFound");
            }
        }

        [ChatCommand("deletehome")]
        private void cmdChatHomeDelete(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermDeleteHome))
            {
                return;
            }

            if (args.Length != 2)
            {
                PrintMsgL(player, "SyntaxCommandHomeDelete");
                return;
            }
            ulong userId = FindPlayersSingleId(args[0], player);
            if (userId <= 0)
            {
                return;
            }

            HomeData targetHome;
            if (!Home.TryGetValue(userId, out targetHome) || !targetHome.Locations.Remove(args[1]))
            {
                PrintMsgL(player, "HomeNotFound");
                return;
            }
            changedHome = true;
            PrintMsgL(player, "HomeDelete", args[0], args[1]);
        }

        [ChatCommand("tphome")]
        private void cmdChatHomeAdminTP(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermTpHome))
            {
                return;
            }

            if (args.Length != 2)
            {
                PrintMsgL(player, "SyntaxCommandHomeAdminTP");
                return;
            }
            ulong userId = FindPlayersSingleId(args[0], player);
            if (userId <= 0)
            {
                return;
            }

            HomeData targetHome;
            Vector3 location;
            if (!Home.TryGetValue(userId, out targetHome) || !targetHome.Locations.TryGetValue(args[1], out location))
            {
                PrintMsgL(player, "HomeNotFound");
                return;
            }
            Teleport(player, location);
            PrintMsgL(player, "HomeAdminTP", args[0], args[1]);
        }

        // Check that plugins are available and enabled for CheckEconomy()
        private bool UseEconomy()
        {
            return (configData.Settings.UseEconomics && Economics) ||
                (configData.Settings.UseServerRewards && ServerRewards);
        }

        // Check balance on multiple plugins and optionally withdraw money from the player
        private bool CheckEconomy(BasePlayer player, double bypass, bool withdraw = false, bool deposit = false)
        {
            double balance = 0;
            bool foundmoney = false;

            // Check Economics first.  If not in use or balance low, check ServerRewards below
            if(configData.Settings.UseEconomics && Economics)
            {
                balance = (double)Economics?.CallHook("Balance", player.UserIDString);
                if(balance >= bypass)
                {
                    foundmoney = true;
                    if(withdraw)
                    {
                        bool w = (bool)Economics?.CallHook("Withdraw", player.userID, bypass);
                        return w;
                    }
                    else if(deposit)
                    {
                        bool w = (bool)Economics?.CallHook("Deposit", player.userID, bypass);
                    }
                }
            }

            // No money via Economics, or plugin not in use.  Try ServerRewards.
            if(configData.Settings.UseServerRewards && ServerRewards)
            {
                object bal = ServerRewards?.Call("CheckPoints", player.userID);
                balance = Convert.ToDouble(bal);
                if(balance >= bypass && !foundmoney)
                {
                    foundmoney = true;
                    if(withdraw)
                    {
                        return (bool)ServerRewards?.Call("TakePoints", player.userID, (int)bypass);
                    }
                    else if(deposit)
                    {
                        ServerRewards?.Call("AddPoints", player.userID, (int)bypass);
                    }
                }
            }

            // Just checking balance without withdrawal - did we find anything?
            return foundmoney;
        }

        private void cmdChatHomeTP(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player, PermHome))
            {
                return;
            }

            bool paidmoney = false;
            if (!configData.Settings.HomesEnabled)
            {
                return;
            }

            if (args.Length < 1)
            {
                PrintMsgL(player, "SyntaxCommandHome");
                return;
            }
            string err = CheckPlayer(player, configData.Home.UsableOutOfBuildingBlocked, CanCraftHome(player), true, "home");
            if (err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData) || homeData.Locations.Count == 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            Vector3 location;
            if (!homeData.Locations.TryGetValue(args[0], out location))
            {
                PrintMsgL(player, "HomeNotFound");
                return;
            }
            err = CheckFoundation(player.userID, location) ?? CheckTargetLocation(player, location, configData.Home.UsableIntoBuildingBlocked, configData.Home.CupOwnerAllowOnBuildingBlocked);
            if (err != null)
            {
                PrintMsgL(player, "HomeRemovedInvalid", args[0]);
                homeData.Locations.Remove(args[0]);
                changedHome = true;
                return;
            }
            err = CheckInsideBlock(location);
            if (err != null)
            {
                PrintMsgL(player, "HomeRemovedInsideBlock", args[0]);
                homeData.Locations.Remove(args[0]);
                changedHome = true;
                return;
            }
            int timestamp = Facepunch.Math.Epoch.Current;
            string currentDate = DateTime.Now.ToString("d");
            if (homeData.Teleports.Date != currentDate)
            {
                homeData.Teleports.Amount = 0;
                homeData.Teleports.Date = currentDate;
            }
            int cooldown = GetLower(player, configData.Home.VIPCooldowns, configData.Home.Cooldown);

            if (cooldown > 0 && timestamp - homeData.Teleports.Timestamp < cooldown)
            {
                string cmdSent = "";
                bool foundmoney = CheckEconomy(player, configData.Home.Bypass);
                try
                {
                    cmdSent = args[1].ToLower();
                }
                catch {}

                bool payalso = false;
                if(configData.Home.Pay > 0)
                {
                    payalso = true;
                }
                if((configData.Settings.BypassCMD != null) && (cmdSent == configData.Settings.BypassCMD.ToLower()))
                {
                    if(foundmoney)
                    {
                        CheckEconomy(player, configData.Home.Bypass, true);
                        paidmoney = true;
                        PrintMsgL(player, "HomeTPCooldownBypass", configData.Home.Bypass);
                        if(payalso)
                        {
                            PrintMsgL(player, "PayToHome", configData.Home.Pay);
                        }
                    }
                    else
                    {
                        PrintMsgL(player, "HomeTPCooldownBypassF", configData.Home.Bypass);
                        return;
                    }
                }
                else if(UseEconomy())
                {
                    int remain = cooldown - (timestamp - homeData.Teleports.Timestamp);
                    PrintMsgL(player, "HomeTPCooldown", FormatTime(remain));
                    if(configData.Home.Bypass > 0 && configData.Settings.BypassCMD != null)
                    {
                        PrintMsgL(player, "HomeTPCooldownBypassP", configData.Home.Bypass);
                        PrintMsgL(player, "HomeTPCooldownBypassP2", configData.Settings.BypassCMD);
                    }
                    return;
                }
                else
                {
                    int remain = cooldown - (timestamp - homeData.Teleports.Timestamp);
                    PrintMsgL(player, "HomeTPCooldown", FormatTime(remain));
                    return;
                }
            }
            int limit = GetHigher(player, configData.Home.VIPDailyLimits, configData.Home.DailyLimit);
            if (limit > 0 && homeData.Teleports.Amount >= limit)
            {
                PrintMsgL(player, "HomeTPLimitReached", limit);
                return;
            }
            if (TeleportTimers.ContainsKey(player.userID))
            {
                PrintMsgL(player, "TeleportPending");
                return;
            }
            err = CanPlayerTeleport(player);
            if (err != null)
            {
                SendReply(player, err);
                return;
            }
            err = CheckItems(player);
            if (err != null)
            {
                PrintMsgL(player, "TPBlockedItem", err);
                return;
            }

            int countdown = GetLower(player, configData.Home.VIPCountdowns, configData.Home.Countdown);
            TeleportTimers[player.userID] = new TeleportTimer
            {
                OriginPlayer = player,
                Timer = timer.Once(countdown, () =>
                {
                    DoLog("Calling CheckPlayer from cmdChatHomeTP");
                    err = CheckPlayer(player, configData.Home.UsableOutOfBuildingBlocked, CanCraftHome(player), true, "home");
                    if (err != null)
                    {
                        PrintMsgL(player, "Interrupted");
                        PrintMsgL(player, err);
                        if(paidmoney)
                        {
                            paidmoney = false;
                            CheckEconomy(player, configData.Home.Bypass, false, true);
                        }
                        TeleportTimers.Remove(player.userID);
                        return;
                    }
                    err = CanPlayerTeleport(player);
                    if (err != null)
                    {
                        PrintMsgL(player, "Interrupted");
                        PrintMsgL(player, err);
                        if(paidmoney)
                        {
                            paidmoney = false;
                            CheckEconomy(player, configData.Home.Bypass, false, true);
                        }
                        TeleportTimers.Remove(player.userID);
                        return;
                    }
                    err = CheckItems(player);
                    if (err != null)
                    {
                        PrintMsgL(player, "Interrupted");
                        PrintMsgL(player, "TPBlockedItem", err);
                        if(paidmoney)
                        {
                            paidmoney = false;
                            CheckEconomy(player, configData.Home.Bypass, false, true);
                        }
                        TeleportTimers.Remove(player.userID);
                        return;
                    }
                    err = CheckFoundation(player.userID, location) ?? CheckTargetLocation(player, location, configData.Home.UsableIntoBuildingBlocked, configData.Home.CupOwnerAllowOnBuildingBlocked);
                    if (err != null)
                    {
                        PrintMsgL(player, "HomeRemovedInvalid", args[0]);
                        homeData.Locations.Remove(args[0]);
                        changedHome = true;
                        if(paidmoney)
                        {
                            paidmoney = false;
                            CheckEconomy(player, configData.Home.Bypass, false, true);
                        }
                        return;
                    }
                    err = CheckInsideBlock(location);
                    if (err != null)
                    {
                        PrintMsgL(player, "HomeRemovedInsideBlock", args[0]);
                        homeData.Locations.Remove(args[0]);
                        changedHome = true;
                        if(paidmoney)
                        {
                            paidmoney = false;
                            CheckEconomy(player, configData.Home.Bypass, false, true);
                        }
                        return;
                    }
                    if(UseEconomy())
                    {
                        if (configData.Home.Pay > 0 && !CheckEconomy(player, configData.Home.Pay))
                        {
                            PrintMsgL(player, "Interrupted");
                            PrintMsgL(player, "TPNoMoney", configData.Home.Pay);

                            TeleportTimers.Remove(player.userID);
                            return;
                        }
                        else if(configData.Home.Pay > 0)
                        {
                            bool w = CheckEconomy(player, (double)configData.Home.Pay, true);
                            PrintMsgL(player, "TPMoney", (double)configData.Home.Pay);
                        }
                    }
                    Teleport(player, location);
                    homeData.Teleports.Amount++;
                    homeData.Teleports.Timestamp = timestamp;
                    changedHome = true;
                    PrintMsgL(player, "HomeTP", args[0]);
                    if (limit > 0)
                    {
                        PrintMsgL(player, "HomeTPAmount", limit - homeData.Teleports.Amount);
                    }

                    TeleportTimers.Remove(player.userID);
                })
            };
            PrintMsgL(player, "HomeTPStarted", args[0], countdown);
        }

        [ChatCommand("listhomes")]
        private void cmdChatListHome(BasePlayer player, string command, string[] args)
        {
            if (!configData.Settings.HomesEnabled)
            {
                return;
            }

            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandListHomes");
                return;
            }
            HomeData homeData;
            if (!Home.TryGetValue(player.userID, out homeData) || homeData.Locations.Count == 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            PrintMsgL(player, "HomeList");
            if (configData.Home.CheckValidOnList)
            {
                List<string> toRemove = new List<string>();
                foreach (KeyValuePair<string, Vector3> location in homeData.Locations)
                {
                    string err = CheckFoundation(player.userID, location.Value);
                    if (err != null)
                    {
                        toRemove.Add(location.Key);
                        continue;
                    }
                    PrintMsgL(player, $"{location.Key} {location.Value}");
                }
                foreach (string loc in toRemove)
                {
                    PrintMsgL(player, "HomeRemovedInvalid", loc);
                    homeData.Locations.Remove(loc);
                    changedHome = true;
                }
                return;
            }
            foreach (KeyValuePair<string, Vector3> location in homeData.Locations)
            {
                PrintMsgL(player, $"{location.Key} {location.Value}");
            }
        }

        [ChatCommand("homehomes")]
        private void cmdChatHomeHomes(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowedMsg(player, PermHomeHomes))
            {
                return;
            }

            if (args.Length != 1)
            {
                PrintMsgL(player, "SyntaxCommandHomeHomes");
                return;
            }
            ulong userId = FindPlayersSingleId(args[0], player);
            if (userId <= 0)
            {
                return;
            }

            HomeData homeData;
            if (!Home.TryGetValue(userId, out homeData) || homeData.Locations.Count == 0)
            {
                PrintMsgL(player, "HomeListEmpty");
                return;
            }
            PrintMsgL(player, "HomeList");
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, Vector3> location in homeData.Locations)
            {
                string err = CheckFoundation(userId, location.Value);
                if (err != null)
                {
                    toRemove.Add(location.Key);
                    continue;
                }
                PrintMsgL(player, $"{location.Key} {location.Value}");
            }
            foreach (string loc in toRemove)
            {
                PrintMsgL(player, "HomeRemovedInvalid", loc);
                homeData.Locations.Remove(loc);
                changedHome = true;
            }
        }

        [ChatCommand("tpr")]
        private void cmdChatTeleportRequest(BasePlayer player, string command, string[] args)
        {
            if(!IsAllowedMsg(player, PermTpR))
            {
                return;
            }

            if (!configData.Settings.TPREnabled)
            {
                return;
            }
            //if (args.Length != 1)
            if (args.Length == 0)
            {
                PrintMsgL(player, "SyntaxCommandTPR");
                return;
            }
            List<BasePlayer> targets = FindPlayersOnline(args[0]);
            if(targets.Count == 0)
            {
                PrintMsgL(player, "PlayerNotFound");
                return;
            }
            if(targets.Count > 1)
            {
                PrintMsgL(player, "MultiplePlayers", string.Join(", ", targets.ConvertAll(p => p.displayName).ToArray()));
                return;
            }
            BasePlayer target = targets[0];
            if(target == player)
            {
                if (!configData.debug)
                {
                    PrintMsgL(player, "CantTeleportToSelf");
                    return;
                }
                DoLog("Debug mode - allowing self teleport.");
            }
            DoLog("Calling CheckPlayer from cmdChatTeleportRequest");
            string err = CheckPlayer(player, configData.TPR.UsableOutOfBuildingBlocked, CanCraftTPR(player), true, "tpr");
            if(err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            err = CheckTargetLocation(target, target.transform.position, configData.TPR.UsableIntoBuildingBlocked, configData.TPR.CupOwnerAllowOnBuildingBlocked);
            if(err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            int timestamp = Facepunch.Math.Epoch.Current;
            string currentDate = DateTime.Now.ToString("d");
            TeleportData tprData;
            if(!TPR.TryGetValue(player.userID, out tprData))
            {
                TPR[player.userID] = tprData = new TeleportData();
            }
            if(tprData.Date != currentDate)
            {
                tprData.Amount = 0;
                tprData.Date = currentDate;
            }

            int cooldown = GetLower(player, configData.TPR.VIPCooldowns, configData.TPR.Cooldown);
            if(cooldown > 0 && timestamp - tprData.Timestamp < cooldown)
            {
                string cmdSent = "";
                bool foundmoney = CheckEconomy(player, configData.TPR.Bypass);
                try
                {
                    cmdSent = args[1].ToLower();
                }
                catch {}

                bool payalso = false;
                if(configData.TPR.Pay > 0)
                {
                    payalso = true;
                }
                if((configData.Settings.BypassCMD != null) && (cmdSent == configData.Settings.BypassCMD.ToLower()))
                {
                    if(foundmoney)
                    {
                        CheckEconomy(player, configData.TPR.Bypass, true);
                        PrintMsgL(player, "TPRCooldownBypass", configData.TPR.Bypass);
                        if(payalso)
                        {
                            PrintMsgL(player, "PayToTPR", configData.TPR.Pay);
                        }
                    }
                    else
                    {
                        PrintMsgL(player, "TPRCooldownBypassF", configData.TPR.Bypass);
                        return;
                    }
                }
                else if(UseEconomy())
                {
                    int remain = cooldown - (timestamp - tprData.Timestamp);
                    PrintMsgL(player, "TPRCooldown", FormatTime(remain));
                    if(configData.TPR.Bypass > 0 && configData.Settings.BypassCMD != null)
                    {
                        PrintMsgL(player, "TPRCooldownBypassP", configData.TPR.Bypass);
                        if(payalso)
                        {
                            PrintMsgL(player, "PayToTPR", configData.TPR.Pay);
                        }
                        PrintMsgL(player, "TPRCooldownBypassP2a", configData.Settings.BypassCMD);
                    }
                    return;
                }
                else
                {
                    int remain = cooldown - (timestamp - tprData.Timestamp);
                    PrintMsgL(player, "TPRCooldown", FormatTime(remain));
                    return;
                }
            }
            int limit = GetHigher(player, configData.TPR.VIPDailyLimits, configData.TPR.DailyLimit);
            if(limit > 0 && tprData.Amount >= limit)
            {
                PrintMsgL(player, "TPRLimitReached", limit);
                return;
            }
            if(TeleportTimers.ContainsKey(player.userID))
            {
                PrintMsgL(player, "TeleportPending");
                return;
            }
            if(TeleportTimers.ContainsKey(target.userID))
            {
                PrintMsgL(player, "TeleportPendingTarget");
                return;
            }
            if(PlayersRequests.ContainsKey(player.userID))
            {
                PrintMsgL(player, "PendingRequest");
                return;
            }
            if(PlayersRequests.ContainsKey(target.userID))
            {
                PrintMsgL(player, "PendingRequestTarget");
                return;
            }
            err = CanPlayerTeleport(player);
            if(err != null)
            {
                SendReply(player, err);
                return;
            }
            err = CanPlayerTeleport(target);
            if(err != null)
            {
                PrintMsgL(player, "TPRTarget");
                return;
            }
            err = CheckItems(player);
            if(err != null)
            {
                PrintMsgL(player, "TPBlockedItem", err);
                return;
            }

            PlayersRequests[player.userID] = target;
            PlayersRequests[target.userID] = player;
            PendingRequests[target.userID] = timer.Once(configData.TPR.RequestDuration, () => RequestTimedOut(player, target));

            // AutoTPA for self in debug based on AutoAcceptTPR
            if((target == player) && configData.TPR.AutoAcceptTPR)
            {
                DoLog("AutoAcceptTPR in debug!");
                cmdChatTeleportAccept(target, "tpa", new string[0]);
                return;
            }
            // AutoTPA for friends based on AutoAcceptTPR
            if(IsFriend(player.userID, target.userID) && configData.TPR.AutoAcceptTPR)
            {
                DoLog("AutoAcceptTPR!");
                cmdChatTeleportAccept(target, "tpa", new string[0]);
                return;
            }

            PrintMsgL(player, "Request", target.displayName);
            PrintMsgL(target, "RequestTarget", player.displayName);
        }

        [ChatCommand("tpa")]
        private void cmdChatTeleportAccept(BasePlayer player, string command, string[] args)
        {
            if(!configData.Settings.TPREnabled)
            {
                return;
            }

            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandTPA");
                return;
            }
            Timer reqTimer;
            if(!PendingRequests.TryGetValue(player.userID, out reqTimer))
            {
                PrintMsgL(player, "NoPendingRequest");
                return;
            }
            DoLog("Calling CheckPlayer from cmdChatTeleportAccept");
            string err = CheckPlayer(player, false, CanCraftTPR(player), false, "tpa");
            if(err != null)
            {
                PrintMsgL(player, err);
                return;
            }
            err = CanPlayerTeleport(player);
            if(err != null)
            {
                SendReply(player, err);
                return;
            }
            BasePlayer originPlayer = PlayersRequests[player.userID];
            err = CheckTargetLocation(originPlayer, player.transform.position, configData.TPR.UsableIntoBuildingBlocked, configData.TPR.CupOwnerAllowOnBuildingBlocked);
            if(err != null)
            {
                SendReply(player, err);
                return;
            }
            if(configData.TPR.BlockTPAOnCeiling)
            {
                List<BuildingBlock> entities = GetFloor(player.transform.position);
                if(entities.Count > 0)
                {
                    PrintMsgL(player, "AcceptOnRoof");
                    return;
                }
            }
            int countdown = GetLower(originPlayer, configData.TPR.VIPCountdowns, configData.TPR.Countdown);
            PrintMsgL(originPlayer, "Accept", player.displayName, countdown);
            PrintMsgL(player, "AcceptTarget", originPlayer.displayName);
            int timestamp = Facepunch.Math.Epoch.Current;
            TeleportTimers[originPlayer.userID] = new TeleportTimer
            {
                OriginPlayer = originPlayer,
                TargetPlayer = player,
                Timer = timer.Once(countdown, () =>
                {
                    DoLog("Calling CheckPlayer from cmdChatTeleportAccept timer loop");
                    err = CheckPlayer(originPlayer, configData.TPR.UsableOutOfBuildingBlocked, CanCraftTPR(originPlayer), true, "tpa") ?? CheckPlayer(player, false, CanCraftTPR(player), true, "tpa");
                    if(err != null)
                    {
                        PrintMsgL(player, "InterruptedTarget", originPlayer.displayName);
                        PrintMsgL(originPlayer, "Interrupted");
                        PrintMsgL(originPlayer, err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    err = CheckTargetLocation(originPlayer, player.transform.position, configData.TPR.UsableIntoBuildingBlocked, configData.TPR.CupOwnerAllowOnBuildingBlocked);
                    if(err != null)
                    {
                        SendReply(player, err);
                        PrintMsgL(originPlayer, "Interrupted");
                        SendReply(originPlayer, err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    err = CanPlayerTeleport(originPlayer) ?? CanPlayerTeleport(player);
                    if(err != null)
                    {
                        SendReply(player, err);
                        PrintMsgL(originPlayer, "Interrupted");
                        SendReply(originPlayer, err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    err = CheckItems(originPlayer);
                    if(err != null)
                    {
                        PrintMsgL(player, "InterruptedTarget", originPlayer.displayName);
                        PrintMsgL(originPlayer, "Interrupted");
                        PrintMsgL(originPlayer, "TPBlockedItem", err);
                        TeleportTimers.Remove(originPlayer.userID);
                        return;
                    }
                    if(UseEconomy())
                    {
                        if(configData.TPR.Pay > 0)
                        {
                            if(!CheckEconomy(originPlayer, configData.TPR.Pay))
                            {
                                PrintMsgL(player, "InterruptedTarget", originPlayer.displayName);
                                PrintMsgL(originPlayer, "TPNoMoney", configData.TPR.Pay);
                                TeleportTimers.Remove(originPlayer.userID);
                                return;
                            }
                            else
                            {
                                CheckEconomy(originPlayer, configData.TPR.Pay, true);
                                PrintMsgL(originPlayer, "TPMoney", (double)configData.TPR.Pay);
                            }
                        }
                    }
                    Teleport(originPlayer, CheckPosition(player.transform.position));
                    TeleportData tprData = TPR[originPlayer.userID];
                    tprData.Amount++;
                    tprData.Timestamp = timestamp;
                    changedTPR = true;
                    PrintMsgL(player, "SuccessTarget", originPlayer.displayName);
                    PrintMsgL(originPlayer, "Success", player.displayName);
                    int limit = GetHigher(player, configData.TPR.VIPDailyLimits, configData.TPR.DailyLimit);
                    if(limit > 0)
                    {
                        PrintMsgL(originPlayer, "TPRAmount", limit - tprData.Amount);
                    }

                    TeleportTimers.Remove(originPlayer.userID);
                })
            };
            reqTimer.Destroy();
            PendingRequests.Remove(player.userID);
            PlayersRequests.Remove(player.userID);
            PlayersRequests.Remove(originPlayer.userID);
        }

        [ChatCommand("wipehomes")]
        private void cmdChatWipeHomes(BasePlayer player, string command, string[] args)
        {
            if(!IsAllowedMsg(player, PermWipeHomes))
            {
                return;
            }

            Home.Clear();
            changedHome = true;
            PrintMsgL(player, "HomesListWiped");
        }

        [ChatCommand("tphelp")]
        private void cmdChatTeleportHelp(BasePlayer player, string command, string[] args)
        {
            if (!configData.Settings.HomesEnabled && !configData.Settings.TPREnabled && !IsAllowedMsg(player))
            {
                return;
            }

            if (args.Length == 1)
            {
                string key = $"TPHelp{args[0].ToLower()}";
                string msg = _(key, player);
                if (key.Equals(msg))
                {
                    PrintMsgL(player, "InvalidHelpModule");
                }
                else
                {
                    PrintMsg(player, msg);
                }
            }
            else
            {
                string msg = _("TPHelpGeneral", player);
                if (IsAllowed(player))
                {
                    msg += NewLine + "/tphelp AdminTP";
                }

                if (configData.Settings.HomesEnabled)
                {
                    msg += NewLine + "/tphelp Home";
                }

                if (configData.Settings.TPREnabled)
                {
                    msg += NewLine + "/tphelp TPR";
                }

                PrintMsg(player, msg);
            }
        }

        [ChatCommand("tpinfo")]
        private void cmdChatTeleportInfo(BasePlayer player, string command, string[] args)
        {
            if (!configData.Settings.HomesEnabled && !configData.Settings.TPREnabled && !configData.Settings.TownEnabled)
            {
                return;
            }

            if (args.Length == 1)
            {
                string module = args[0].ToLower();
                string msg = _($"TPSettings{module}", player);
                int timestamp = Facepunch.Math.Epoch.Current;
                string currentDate = DateTime.Now.ToString("d");
                TeleportData teleportData;
                int limit;
                int cooldown;
                switch (module)
                {
                    case "home":
                        limit = GetHigher(player, configData.Home.VIPDailyLimits, configData.Home.DailyLimit);
                        cooldown = GetLower(player, configData.Home.VIPCooldowns, configData.Home.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player), GetHigher(player, configData.Home.VIPHomesLimits, configData.Home.HomesLimit)));
                        HomeData homeData;
                        if (!Home.TryGetValue(player.userID, out homeData))
                        {
                            Home[player.userID] = homeData = new HomeData();
                        }

                        if (homeData.Teleports.Date != currentDate)
                        {
                            homeData.Teleports.Amount = 0;
                            homeData.Teleports.Date = currentDate;
                        }
                        if (limit > 0)
                        {
                            PrintMsgL(player, "HomeTPAmount", limit - homeData.Teleports.Amount);
                        }

                        if (cooldown > 0 && timestamp - homeData.Teleports.Timestamp < cooldown)
                        {
                            int remain = cooldown - (timestamp - homeData.Teleports.Timestamp);
                            PrintMsgL(player, "HomeTPCooldown", FormatTime(remain));
                        }
                        break;
                    case "tpr":
                        limit = GetHigher(player, configData.TPR.VIPDailyLimits, configData.TPR.DailyLimit);
                        cooldown = GetLower(player, configData.TPR.VIPCooldowns, configData.TPR.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!TPR.TryGetValue(player.userID, out teleportData))
                        {
                            TPR[player.userID] = teleportData = new TeleportData();
                        }

                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0)
                        {
                            PrintMsgL(player, "TPRAmount", limit - teleportData.Amount);
                        }

                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            int remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "TPRCooldown", FormatTime(remain));
                        }
                        break;
                    case "town":
                        limit = GetHigher(player, configData.Town.VIPDailyLimits, configData.Town.DailyLimit);
                        cooldown = GetLower(player, configData.Town.VIPCooldowns, configData.Town.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!Town.TryGetValue(player.userID, out teleportData))
                        {
                            Town[player.userID] = teleportData = new TeleportData();
                        }

                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0)
                        {
                            PrintMsgL(player, "TownTPAmount", limit - teleportData.Amount);
                        }

                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            int remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "TownTPCooldown", FormatTime(remain));
                            PrintMsgL(player, "TownTPCooldownBypassP", configData.Town.Bypass);
                            PrintMsgL(player, "TownTPCooldownBypassP2", configData.Settings.BypassCMD);
                        }
                        break;
                    case "outpost":
                        limit = GetHigher(player, configData.Outpost.VIPDailyLimits, configData.Outpost.DailyLimit);
                        cooldown = GetLower(player, configData.Outpost.VIPCooldowns, configData.Outpost.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!Outpost.TryGetValue(player.userID, out teleportData))
                        {
                            Outpost[player.userID] = teleportData = new TeleportData();
                        }

                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0)
                        {
                            PrintMsgL(player, "OutpostTPAmount", limit - teleportData.Amount);
                        }

                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            int remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "OutpostTPCooldown", FormatTime(remain));
                            PrintMsgL(player, "OutpostTPCooldownBypassP", configData.Outpost.Bypass);
                            PrintMsgL(player, "OutpostTPCooldownBypassP2", configData.Settings.BypassCMD);
                        }
                        break;
                    case "bandit":
                        limit = GetHigher(player, configData.Bandit.VIPDailyLimits, configData.Bandit.DailyLimit);
                        cooldown = GetLower(player, configData.Bandit.VIPCooldowns, configData.Bandit.Cooldown);
                        PrintMsg(player, string.Format(msg, FormatTime(cooldown), limit > 0 ? limit.ToString() : _("Unlimited", player)));
                        if (!Bandit.TryGetValue(player.userID, out teleportData))
                        {
                            Bandit[player.userID] = teleportData = new TeleportData();
                        }

                        if (teleportData.Date != currentDate)
                        {
                            teleportData.Amount = 0;
                            teleportData.Date = currentDate;
                        }
                        if (limit > 0)
                        {
                            PrintMsgL(player, "BanditTPAmount", limit - teleportData.Amount);
                        }

                        if (cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
                        {
                            int remain = cooldown - (timestamp - teleportData.Timestamp);
                            PrintMsgL(player, "BanditTPCooldown", FormatTime(remain));
                            PrintMsgL(player, "BanditTPCooldownBypassP", configData.Bandit.Bypass);
                            PrintMsgL(player, "BanditTPCooldownBypassP2", configData.Settings.BypassCMD);
                        }
                        break;
                    default:
                        PrintMsgL(player, "InvalidHelpModule");
                        break;
                }
            }
            else
            {
                string msg = _("TPInfoGeneral", player);
                if (configData.Settings.HomesEnabled)
                {
                    msg += NewLine + "/tpinfo Home";
                }

                if (configData.Settings.TPREnabled)
                {
                    msg += NewLine + "/tpinfo TPR";
                }

                if (configData.Settings.TownEnabled)
                {
                    msg += NewLine + "/tpinfo Town";
                }

                if (configData.Settings.OutpostEnabled)
                {
                    msg += NewLine + "/tpinfo Outpost";
                }

                if (configData.Settings.BanditEnabled)
                {
                    msg += NewLine + "/tpinfo Bandit";
                }

                PrintMsgL(player, msg);
            }
        }

        [ChatCommand("tpc")]
        private void cmdChatTeleportCancel(BasePlayer player, string command, string[] args)
        {
            if (!configData.Settings.TPREnabled)
            {
                return;
            }

            if (args.Length != 0)
            {
                PrintMsgL(player, "SyntaxCommandTPC");
                return;
            }
            TeleportTimer teleportTimer;
            if (TeleportTimers.TryGetValue(player.userID, out teleportTimer))
            {
                teleportTimer.Timer?.Destroy();
                PrintMsgL(player, "TPCancelled");
                PrintMsgL(teleportTimer.TargetPlayer, "TPCancelledTarget", player.displayName);
                TeleportTimers.Remove(player.userID);
                return;
            }
            foreach (KeyValuePair<ulong, TeleportTimer> keyValuePair in TeleportTimers)
            {
                if (keyValuePair.Value.TargetPlayer != player)
                {
                    continue;
                }

                keyValuePair.Value.Timer?.Destroy();
                PrintMsgL(keyValuePair.Value.OriginPlayer, "TPCancelledTarget", player.displayName);
                PrintMsgL(player, "TPYouCancelledTarget", keyValuePair.Value.OriginPlayer.displayName);
                TeleportTimers.Remove(keyValuePair.Key);
                return;
            }
            BasePlayer target;
            if (!PlayersRequests.TryGetValue(player.userID, out target))
            {
                PrintMsgL(player, "NoPendingRequest");
                return;
            }
            Timer reqTimer;
            if (PendingRequests.TryGetValue(player.userID, out reqTimer))
            {
                reqTimer.Destroy();
                PendingRequests.Remove(player.userID);
            }
            else if (PendingRequests.TryGetValue(target.userID, out reqTimer))
            {
                reqTimer.Destroy();
                PendingRequests.Remove(target.userID);
                BasePlayer temp = player;
                player = target;
                target = temp;
            }
            PlayersRequests.Remove(target.userID);
            PlayersRequests.Remove(player.userID);
            PrintMsgL(player, "Cancelled", target.displayName);
            PrintMsgL(target, "CancelledTarget", player.displayName);
        }

        [ChatCommand("outpost")]
        private void cmdChatOutpost(BasePlayer player, string command, string[] args)
        {
            if(configData.Settings.OutpostEnabled)
            {
                cmdChatTown(player, "outpost", args);
            }
        }

        [ChatCommand("bandit")]
        private void cmdChatBandit(BasePlayer player, string command, string[] args)
        {
            if(configData.Settings.BanditEnabled)
            {
                cmdChatTown(player, "bandit", args);
            }
        }

        [ChatCommand("town")]
        private void cmdChatTown(BasePlayer player, string command, string[] args)
        {
            DoLog($"cmdChatTown: command={command}");
            switch(command)
            {
                case "outpost":
                    if(!IsAllowedMsg(player, PermTpOutpost))
                    {
                        return;
                    }

                    break;
                case "bandit":
                    if(!IsAllowedMsg(player, PermTpBandit))
                    {
                        return;
                    }

                    break;
                case "town":
                default:
                    if(!IsAllowedMsg(player, PermTpTown))
                    {
                        return;
                    }

                    break;
            }

            // For admin using set command
            if(args.Length == 1 && IsAllowed(player) && args[0].Equals("set", StringComparison.CurrentCultureIgnoreCase))
            {
                switch(command)
                {
                    case "outpost":
                        configData.Outpost.Location = player.transform.position;
                        Config.WriteObject(configData, true);
                        PrintMsgL(player, "OutpostTPLocation", configData.Outpost.Location);
                        break;
                    case "bandit":
                        configData.Bandit.Location = player.transform.position;
                        Config.WriteObject(configData, true);
                        PrintMsgL(player, "BanditTPLocation", configData.Bandit.Location);
                        break;
                    case "town":
                    default:
                        configData.Town.Location = player.transform.position;
                        Config.WriteObject(configData, true);
                        PrintMsgL(player, "TownTPLocation", configData.Town.Location);
                        break;
                }
                return;
            }

            bool paidmoney = false;

            // Is outpost/bandit/town usage enabled?
            if(!configData.Settings.OutpostEnabled && command == "outpost")
            {
                PrintMsgL(player, "OutpostTPDisabled");
                return;
            }
            else if(!configData.Settings.BanditEnabled && command == "bandit")
            {
                PrintMsgL(player, "BanditTPDisabled");
                return;
            }
            else if(!configData.Settings.TownEnabled && command == "town")
            {
                PrintMsgL(player, "TownTPDisabled");
                return;
            }

            // Are they trying to bypass cooldown or did they just type something else?
            if(args.Length == 1 && (!string.Equals(args[0], configData.Settings.BypassCMD, StringComparison.CurrentCultureIgnoreCase)))
            {
                string com = command ?? "town";
                string msg  = "SyntaxCommand" + char.ToUpper(com[0]) + com.Substring(1);
                PrintMsgL(player, msg);
                if(IsAllowed(player))
                {
                    PrintMsgL(player, msg + "Admin");
                }

                return;
            }

            // Is outpost/bandit/town location set?
            if(configData.Outpost.Location == default(Vector3) && command == "outpost")
            {
                PrintMsgL(player, "OutpostTPNotSet");
                return;
            }
            else if(configData.Bandit.Location == default(Vector3) && command == "bandit")
            {
                PrintMsgL(player, "BanditTPNotSet");
                return;
            }
            else if(configData.Town.Location == default(Vector3) && command == "town")
            {
                PrintMsgL(player, "TownTPNotSet");
                return;
            }

            TeleportData teleportData = new TeleportData();
            int timestamp = Facepunch.Math.Epoch.Current;
            string currentDate = DateTime.Now.ToString("d");

            string err = null;
            int cooldown = 0;
            int limit = 0;
            int targetPay = 0;
            int targetBypass = 0;
            string msgPay = null;
            string msgCooldown = null;
            string msgCooldownBypass  = null;
            string msgCooldownBypassF = null;
            string msgCooldownBypassP = null;
            string msgCooldownBypassP2 = null;
            string msgLimitReached = null;
            DoLog("Calling CheckPlayer from cmdChatTown");
            // Setup vars for checks below
            switch(command)
            {
                case "outpost":
                    err = CheckPlayer(player, configData.Outpost.UsableOutOfBuildingBlocked, CanCraftOutpost(player), true, "outpost");
                    if(err != null)
                    {
                        PrintMsgL(player, err);
                        if(err == "TPHostile")
                        {
                            float unHostileTime = (float) player.State.unHostileTimestamp;
                            float currentTime = (float) Network.TimeEx.currentTimestamp;
                            string pt = ((int)Math.Abs(unHostileTime - currentTime) / 60).ToString();
                            if((unHostileTime - currentTime) < 60)
                            {
                                pt = "<1";
                            }

                            PrintMsgL(player, "HostileTimer", pt);
                        }
                        return;
                    }
                    cooldown = GetLower(player, configData.Outpost.VIPCooldowns, configData.Outpost.Cooldown);
                    if(!Outpost.TryGetValue(player.userID, out teleportData))
                    {
                        Outpost[player.userID] = teleportData = new TeleportData();
                    }
                    if(teleportData.Date != currentDate)
                    {
                        teleportData.Amount = 0;
                        teleportData.Date = currentDate;
                    }

                    targetPay = configData.Outpost.Pay;
                    targetBypass = configData.Outpost.Bypass;

                    msgPay = "PayToOutpost";
                    msgCooldown = "OutpostTPCooldown";
                    msgCooldownBypass = "OutpostTPCooldownBypass";
                    msgCooldownBypassF = "OutpostTPCooldownBypassF";
                    msgCooldownBypassP = "OutpostTPCooldownBypassP";
                    msgCooldownBypassP2 = "OutpostTPCooldownBypassP2";
                    msgLimitReached = "OutpostTPLimitReached";
                    limit = GetHigher(player, configData.Outpost.VIPDailyLimits, configData.Outpost.DailyLimit);
                    break;
                case "bandit":
                    err = CheckPlayer(player, configData.Bandit.UsableOutOfBuildingBlocked, CanCraftBandit(player), true, "bandit");
                    if(err != null)
                    {
                        PrintMsgL(player, err);
                        if(err == "TPHostile")
                        {
                            float unHostileTime = (float) player.State.unHostileTimestamp;
                            float currentTime = (float) Network.TimeEx.currentTimestamp;
                            string pt = ((int)Math.Abs(unHostileTime - currentTime) / 60).ToString();
                            if((unHostileTime - currentTime) < 60)
                            {
                                pt = "<1";
                            }

                            PrintMsgL(player, "HostileTimer", pt);
                        }
                        return;
                    }
                    cooldown = GetLower(player, configData.Bandit.VIPCooldowns, configData.Bandit.Cooldown);
                    if(!Bandit.TryGetValue(player.userID, out teleportData))
                    {
                        Bandit[player.userID] = teleportData = new TeleportData();
                    }
                    if(teleportData.Date != currentDate)
                    {
                        teleportData.Amount = 0;
                        teleportData.Date = currentDate;
                    }
                    targetPay = configData.Bandit.Pay;
                    targetBypass = configData.Bandit.Bypass;

                    msgPay = "PayToBandit";
                    msgCooldown = "BanditTPCooldown";
                    msgCooldownBypass = "BanditTPCooldownBypass";
                    msgCooldownBypassF = "BanditTPCooldownBypassF";
                    msgCooldownBypassP = "BanditTPCooldownBypassP";
                    msgCooldownBypassP2 = "BanditTPCooldownBypassP2";
                    msgLimitReached = "BanditTPLimitReached";
                    limit = GetHigher(player, configData.Bandit.VIPDailyLimits, configData.Bandit.DailyLimit);
                    break;
                case "town":
                default:
                    err = CheckPlayer(player, configData.Town.UsableOutOfBuildingBlocked, CanCraftTown(player), true, "town");
                    if(err != null)
                    {
                        PrintMsgL(player, err);
                        return;
                    }
                    cooldown = GetLower(player, configData.Town.VIPCooldowns, configData.Town.Cooldown);
                    if(!Town.TryGetValue(player.userID, out teleportData))
                    {
                        Town[player.userID] = teleportData = new TeleportData();
                    }
                    if(teleportData.Date != currentDate)
                    {
                        teleportData.Amount = 0;
                        teleportData.Date = currentDate;
                    }
                    targetPay = configData.Town.Pay;
                    targetBypass = configData.Town.Bypass;

                    msgPay = "PayToTown";
                    msgCooldown = "TownTPCooldown";
                    msgCooldownBypass = "TownTPCooldownBypass";
                    msgCooldownBypassF = "TownTPCooldownBypassF";
                    msgCooldownBypassP = "TownTPCooldownBypassP";
                    msgCooldownBypassP2 = "TownTPCooldownBypassP2";
                    msgLimitReached = "TownTPLimitReached";
                    limit = GetHigher(player, configData.Town.VIPDailyLimits, configData.Town.DailyLimit);
                    break;
            }

            // Check and process cooldown, bypass, and payment for all modes
            if(cooldown > 0 && timestamp - teleportData.Timestamp < cooldown)
            {
                string cmdSent = "";
                bool foundmoney = CheckEconomy(player, targetBypass);
                try
                {
                    cmdSent = args[0].ToLower();
                }
                catch {}

                bool payalso = false;
                if(targetPay > 0)
                {
                    payalso = true;
                }
                if((configData.Settings.BypassCMD != null) && (cmdSent == configData.Settings.BypassCMD.ToLower()))
                {
                    if(foundmoney)
                    {
                        CheckEconomy(player, targetBypass, true);
                        paidmoney = true;
                        PrintMsgL(player, msgCooldownBypass, targetBypass);
                        if(payalso)
                        {
                            PrintMsgL(player, msgPay, targetPay);
                        }
                    }
                    else
                    {
                        PrintMsgL(player, msgCooldownBypassF, targetBypass);
                        return;
                    }
                }
                else if(UseEconomy())
                {
                    int remain = cooldown - (timestamp - teleportData.Timestamp);
                    PrintMsgL(player, msgCooldown, FormatTime(remain));
                    if(targetBypass > 0 && configData.Settings.BypassCMD != null)
                    {
                        PrintMsgL(player, msgCooldownBypassP, targetBypass);
                        PrintMsgL(player, msgCooldownBypassP2, configData.Settings.BypassCMD);
                    }
                    return;
                }
                else
                {
                    int remain = cooldown - (timestamp - teleportData.Timestamp);
                    PrintMsgL(player, msgCooldown, FormatTime(remain));
                    return;
                }
            }

            if(limit > 0 && teleportData.Amount >= limit)
            {
                PrintMsgL(player, msgLimitReached, limit);
                return;
            }
            if(TeleportTimers.ContainsKey(player.userID))
            {
                PrintMsgL(player, "TeleportPending");
                return;
            }
            err = CanPlayerTeleport(player);
            if(err != null)
            {
                SendReply(player, err);
                return;
            }
            err = CheckItems(player);
            if(err != null)
            {
                PrintMsgL(player, "TPBlockedItem", err);
                return;
            }

            int countdown = 0;
            switch(command)
            {
                case "outpost":
                    countdown = GetLower(player, configData.Outpost.VIPCountdowns, configData.Outpost.Countdown);
                    TeleportTimers[player.userID] = new TeleportTimer
                    {
                        OriginPlayer = player,
                        Timer = timer.Once(countdown, () =>
                        {
                            DoLog("Calling CheckPlayer from cmdChatTown outpost timer loop");
                            err = CheckPlayer(player, configData.Outpost.UsableOutOfBuildingBlocked, CanCraftOutpost(player), true, "outpost");
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Outpost.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CanPlayerTeleport(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Outpost.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CheckItems(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, "TPBlockedItem", err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Outpost.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            if(UseEconomy())
                            {
                                if(configData.Outpost.Pay > 0 && ! CheckEconomy(player, configData.Outpost.Pay))
                                {
                                    PrintMsgL(player, "Interrupted");
                                    PrintMsgL(player, "TPNoMoney", configData.Outpost.Pay);
                                    TeleportTimers.Remove(player.userID);
                                    return;
                                }
                                else if (configData.Outpost.Pay > 0)
                                {
                                    CheckEconomy(player, configData.Outpost.Pay, true);
                                    PrintMsgL(player, "TPMoney", (double)configData.Outpost.Pay);
                                }
                            }
                            Teleport(player, configData.Outpost.Location);
                            teleportData.Amount++;
                            teleportData.Timestamp = timestamp;

                            changedOutpost = true;
                            PrintMsgL(player, "OutpostTP");
                            if (limit > 0)
                            {
                                PrintMsgL(player, "OutpostTPAmount", limit - teleportData.Amount);
                            }

                            TeleportTimers.Remove(player.userID);
                        })
                    };
                    PrintMsgL(player, "OutpostTPStarted", countdown);
                    break;
                case "bandit":
                    countdown = GetLower(player, configData.Bandit.VIPCountdowns, configData.Bandit.Countdown);
                    TeleportTimers[player.userID] = new TeleportTimer
                    {
                        OriginPlayer = player,
                        Timer = timer.Once(countdown, () =>
                        {
                            DoLog("Calling CheckPlayer from cmdChatTown bandit timer loop");
                            err = CheckPlayer(player, configData.Bandit.UsableOutOfBuildingBlocked, CanCraftBandit(player), true, "bandit");
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Bandit.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CanPlayerTeleport(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Bandit.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CheckItems(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, "TPBlockedItem", err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Bandit.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            if(UseEconomy())
                            {
                                if(configData.Bandit.Pay > 0 && ! CheckEconomy(player, configData.Bandit.Pay))
                                {
                                    PrintMsgL(player, "Interrupted");
                                    PrintMsgL(player, "TPNoMoney", configData.Bandit.Pay);
                                    TeleportTimers.Remove(player.userID);
                                    return;
                                }
                                else if (configData.Bandit.Pay > 0)
                                {
                                    CheckEconomy(player, configData.Bandit.Pay, true);
                                    PrintMsgL(player, "TPMoney", (double)configData.Bandit.Pay);
                                }
                            }
                            Teleport(player, configData.Bandit.Location);
                            teleportData.Amount++;
                            teleportData.Timestamp = timestamp;

                            changedBandit = true;
                            PrintMsgL(player, "BanditTP");
                            if (limit > 0)
                            {
                                PrintMsgL(player, "BanditTPAmount", limit - teleportData.Amount);
                            }

                            TeleportTimers.Remove(player.userID);
                        })
                    };
                    PrintMsgL(player, "BanditTPStarted", countdown);
                    break;
                case "town":
                default:
                    countdown = GetLower(player, configData.Town.VIPCountdowns, configData.Town.Countdown);
                    TeleportTimers[player.userID] = new TeleportTimer
                    {
                        OriginPlayer = player,
                        Timer = timer.Once(countdown, () =>
                        {
                            DoLog("Calling CheckPlayer from cmdChatTown town timer loop");
                            err = CheckPlayer(player, configData.Town.UsableOutOfBuildingBlocked, CanCraftTown(player), true, "town");
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Town.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CanPlayerTeleport(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Town.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            err = CheckItems(player);
                            if (err != null)
                            {
                                PrintMsgL(player, "Interrupted");
                                PrintMsgL(player, "TPBlockedItem", err);
                                if(paidmoney)
                                {
                                    paidmoney = false;
                                    CheckEconomy(player, configData.Town.Bypass, false, true);
                                }
                                TeleportTimers.Remove(player.userID);
                                return;
                            }
                            if(UseEconomy())
                            {
                                if(configData.Town.Pay > 0 && ! CheckEconomy(player, configData.Town.Pay))
                                {
                                    PrintMsgL(player, "Interrupted");
                                    PrintMsgL(player, "TPNoMoney", configData.Town.Pay);
                                    TeleportTimers.Remove(player.userID);
                                    return;
                                }
                                else if (configData.Town.Pay > 0)
                                {
                                    CheckEconomy(player, configData.Town.Pay, true);
                                    PrintMsgL(player, "TPMoney", (double)configData.Town.Pay);
                                }
                            }
                            Teleport(player, configData.Town.Location);
                            teleportData.Amount++;
                            teleportData.Timestamp = timestamp;

                            changedTown = true;
                            PrintMsgL(player, "TownTP");
                            if (limit > 0)
                            {
                                PrintMsgL(player, "TownTPAmount", limit - teleportData.Amount);
                            }

                            TeleportTimers.Remove(player.userID);
                        })
                    };
                    PrintMsgL(player, "TownTPStarted", countdown);
                    break;
            }
        }

        private bool ccmdTeleport(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !IsAllowedMsg(arg.Player(), PermTpConsole))
            {
                return false;
            }

            HashSet<BasePlayer> players;
            switch (arg.cmd.FullName)
            {
                case "teleport.topos":
                    if (!arg.HasArgs(4))
                    {
                        arg.ReplyWith(_("SyntaxConsoleCommandToPos", arg.Player()));
                        return false;
                    }
                    players = FindPlayers(arg.GetString(0));
                    if (players.Count == 0)
                    {
                        arg.ReplyWith(_("PlayerNotFound", arg.Player()));
                        return false;
                    }
                    if (players.Count > 1)
                    {
                        arg.ReplyWith(_("MultiplePlayers", arg.Player(), string.Join(", ", players.Select(p => p.displayName).ToArray())));
                        return false;
                    }
                    BasePlayer targetPlayer = players.First();
                    float x = arg.GetFloat(1, -10000);
                    float y = arg.GetFloat(2, -10000);
                    float z = arg.GetFloat(3, -10000);
                    if (!CheckBoundaries(x, y, z))
                    {
                        arg.ReplyWith(_("AdminTPOutOfBounds", arg.Player()) + Environment.NewLine + _("AdminTPBoundaries", arg.Player(), boundary));
                        return false;
                    }
                    TeleportToPosition(targetPlayer, x, y, z);
                    if (configData.Admin.AnnounceTeleportToTarget)
                    {
                        PrintMsgL(targetPlayer, "AdminTPConsoleTP", targetPlayer.transform.position);
                    }

                    arg.ReplyWith(_("AdminTPTargetCoordinates", arg.Player(), targetPlayer.displayName, targetPlayer.transform.position));
                    DoLog(_("LogTeleportPlayer", null, arg.Player()?.displayName, targetPlayer.displayName, targetPlayer.transform.position));
                    break;
                case "teleport.toplayer":
                    if (!arg.HasArgs(2))
                    {
                        arg.ReplyWith(_("SyntaxConsoleCommandToPlayer", arg.Player()));
                        return false;
                    }
                    players = FindPlayers(arg.GetString(0));
                    if (players.Count == 0)
                    {
                        arg.ReplyWith(_("PlayerNotFound", arg.Player()));
                        return false;
                    }
                    if (players.Count > 1)
                    {
                        arg.ReplyWith(_("MultiplePlayers", arg.Player(), string.Join(", ", players.Select(p => p.displayName).ToArray())));
                        return false;
                    }
                    BasePlayer originPlayer = players.First();
                    players = FindPlayers(arg.GetString(1));
                    if (players.Count == 0)
                    {
                        arg.ReplyWith(_("PlayerNotFound", arg.Player()));
                        return false;
                    }
                    if (players.Count > 1)
                    {
                        arg.ReplyWith(_("MultiplePlayers", arg.Player(), string.Join(", ", players.Select(p => p.displayName).ToArray())));
                        return false;
                    }
                    targetPlayer = players.First();
                    if (targetPlayer == originPlayer)
                    {
                        arg.ReplyWith(_("CantTeleportPlayerToSelf", arg.Player()));
                        return false;
                    }
                    TeleportToPlayer(originPlayer, targetPlayer);
                    arg.ReplyWith(_("AdminTPPlayers", arg.Player(), originPlayer.displayName, targetPlayer.displayName));
                    PrintMsgL(originPlayer, "AdminTPConsoleTPPlayer", targetPlayer.displayName);
                    if (configData.Admin.AnnounceTeleportToTarget)
                    {
                        PrintMsgL(targetPlayer, "AdminTPConsoleTPPlayerTarget", originPlayer.displayName);
                    }

                    DoLog(_("LogTeleportPlayer", null, arg.Player()?.displayName, originPlayer.displayName, targetPlayer.displayName));
                    break;
            }
            return false;
        }

        [ConsoleCommand("teleport.importhomes")]
        private bool ccmdImportHomes(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !IsAllowedMsg(arg.Player(), PermImportHomes))
            {
                arg.ReplyWith("Not allowed.");
                return false;
            }
            DynamicConfigFile datafile = Interface.Oxide.DataFileSystem.GetFile("m-Teleportation");
            if (!datafile.Exists())
            {
                arg.ReplyWith("No m-Teleportation.json exists.");
                return false;
            }
            datafile.Load();
            Dictionary<string, object> allHomeData = datafile["HomeData"] as Dictionary<string, object>;
            if (allHomeData == null)
            {
                arg.ReplyWith("Empty HomeData.");
                return false;
            }
            int count = 0;
            foreach (KeyValuePair<string, object> kvp in allHomeData)
            {
                Dictionary<string, object> homeDataOld = kvp.Value as Dictionary<string, object>;
                if (homeDataOld == null)
                {
                    continue;
                }

                if (!homeDataOld.ContainsKey("HomeLocations"))
                {
                    continue;
                }

                Dictionary<string, object> homeList = homeDataOld["HomeLocations"] as Dictionary<string, object>;
                if (homeList == null)
                {
                    continue;
                }

                ulong userId = Convert.ToUInt64(kvp.Key);
                HomeData homeData;
                if (!Home.TryGetValue(userId, out homeData))
                {
                    Home[userId] = homeData = new HomeData();
                }

                foreach (KeyValuePair<string, object> kvp2 in homeList)
                {
                    Dictionary<string, object> positionData = kvp2.Value as Dictionary<string, object>;
                    if (positionData == null)
                    {
                        continue;
                    }

                    if (!positionData.ContainsKey("x") || !positionData.ContainsKey("y") || !positionData.ContainsKey("z"))
                    {
                        continue;
                    }

                    Vector3 position = new Vector3(Convert.ToSingle(positionData["x"]), Convert.ToSingle(positionData["y"]), Convert.ToSingle(positionData["z"]));
                    homeData.Locations[kvp2.Key] = position;
                    changedHome = true;
                    count++;
                }
            }
            arg.ReplyWith(string.Format("Imported {0} homes.", count));
            return false;
        }

        private void RequestTimedOut(BasePlayer player, BasePlayer target)
        {
            PlayersRequests.Remove(player.userID);
            PlayersRequests.Remove(target.userID);
            PendingRequests.Remove(target.userID);
            PrintMsgL(player, "TimedOut", target.displayName);
            PrintMsgL(target, "TimedOutTarget", player.displayName);
        }

        #region Util
        private string FormatTime(long seconds)
        {
            TimeSpan timespan = TimeSpan.FromSeconds(seconds);
            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
        }

        private double ConvertToRadians(double angle)
        {
            return System.Math.PI / 180 * angle;
        }
        #endregion

        #region Teleport
        public void TeleportToPlayer(BasePlayer player, BasePlayer target) => Teleport(player, target.transform.position);

        public void TeleportToPosition(BasePlayer player, float x, float y, float z) => Teleport(player, new Vector3(x, y, z));

        public void OldTeleport(BasePlayer player, Vector3 position)
        {
            SaveLocation(player);
            teleporting.Add(player.userID);
            if(player.net?.connection != null)
            {
                player.ClientRPCPlayer(null, player, "StartLoading");
            }

            StartSleeping(player);
            player.SetParent(null, true, true);
            player.MovePosition(position);

            if(player.net?.connection != null)
            {
                player.ClientRPCPlayer(null, player, "ForcePositionTo", position);
            }

            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

            player.UpdateNetworkGroup();
            //player.UpdatePlayerCollider(true, false);
            player.SendNetworkUpdateImmediate(false);
            if(player.net?.connection == null)
            {
                return;
            }

            //TODO temporary for potential rust bug
            try { player.ClearEntityQueue(null); } catch {}

            player.SendFullSnapshot();
        }

        public void Teleport(BasePlayer player, Vector3 position)
        {
            SaveLocation(player);
            teleporting.Add(player.userID);

            if(player.net?.connection != null)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            }

            player.SetParent(null, true, true);
            player.EnsureDismounted();
            player.Teleport(position);
            player.UpdateNetworkGroup();
            player.StartSleeping();
            player.SendNetworkUpdateImmediate(false);

            if(player.net?.connection != null)
            {
                player.ClientRPCPlayer(null, player, "StartLoading");
            }
        }

        private void StartSleeping(BasePlayer player)
        {
            if (player.IsSleeping())
            {
                return;
            }

            player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            if (!BasePlayer.sleepingPlayerList.Contains(player))
            {
                BasePlayer.sleepingPlayerList.Add(player);
            }

            player.CancelInvoke("InventoryUpdate");
            //player.inventory.crafting.CancelAll(true);
            //player.UpdatePlayerCollider(true, false);
        }
        #endregion

        #region Checks
        // Used by tpa only to provide for offset from the target to avoid overlap
        private Vector3 CheckPosition(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position, 2, blockLayer);
            float distance = 5f;
            BuildingBlock buildingBlock = null;
            for (int i = 0; i < hits.Length; i++)
            {
                BuildingBlock block = hits[i].GetComponentInParent<BuildingBlock>();
                if (block == null)
                {
                    continue;
                }

                string prefab = block.PrefabName;
                if (!prefab.Contains("foundation", CompareOptions.OrdinalIgnoreCase) && !prefab.Contains("floor", CompareOptions.OrdinalIgnoreCase) && !prefab.Contains("pillar", CompareOptions.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!(Vector3.Distance(block.transform.position, position) < distance))
                {
                    continue;
                }

                buildingBlock = block;
                distance = Vector3.Distance(block.transform.position, position);
            }
            if (buildingBlock == null || !configData.TPR.OffsetTPRTarget)
            {
                return position;
            }

            float blockRotation = buildingBlock.transform.rotation.eulerAngles.y;
            float[] angles = new[] { 360 - blockRotation, 180 - blockRotation };
            Vector3 location = default(Vector3);
            const double r = 2.9;
            float locationDistance = 100f;

            DoLog("CheckPosition: Finding suitable target position");
            string positions = position.ToString();
            DoLog($"CheckPosition:   Old location {positions}");
            for (int i = 0; i < angles.Length; i++)
            {
                double radians = ConvertToRadians(angles[i]);
                double newX = r * Math.Cos(radians);
                double newZ = r * Math.Sin(radians);
                if (configData.debug)
                {
                    DoLog($"CheckPosition:     Checking angle {i}");
                    string newXs = newX.ToString();
                    string newZs = newZ.ToString();
                    DoLog($"CheckPosition:     newX = {newXs}, newZ = {newZs}");
                }
                Vector3 newLoc = new Vector3((float)(buildingBlock.transform.position.x + newX), buildingBlock.transform.position.y + .2f, (float)(buildingBlock.transform.position.z + newZ));
                if (Vector3.Distance(position, newLoc) < locationDistance)
                {
                    location = newLoc;
                    locationDistance = Vector3.Distance(position, newLoc);
                    if (configData.debug)
                    {
                        string locs = newLoc.ToString();
                        DoLog($"CheckPosition:     possible new location at {locs}");
                    }
                }
            }
            if (configData.debug)
            {
                string locations = location.ToString();
                DoLog($"CheckPosition:   New location {locations}");
            }
            return location;
        }

        private string CanPlayerTeleport(BasePlayer player)
        {
            return Interface.Oxide.CallHook("CanTeleport", player) as string;
        }

        private bool CanCraftHome(BasePlayer player)
        {
            return configData.Home.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftHome);
        }

        private bool CanCraftTown(BasePlayer player)
        {
            return configData.Town.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftTown);
        }

        private bool CanCraftOutpost(BasePlayer player)
        {
            return configData.Outpost.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftOutpost);
        }

        private bool CanCraftBandit(BasePlayer player)
        {
            return configData.Bandit.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftBandit);
        }

        private bool CanCraftTPR(BasePlayer player)
        {
            return configData.TPR.AllowCraft || permission.UserHasPermission(player.UserIDString, PermCraftTpR);
        }

        public bool AboveWater(BasePlayer player)
        {
            Vector3 pos = player.transform.position;
            DoLog($"Player position: {pos.ToString()}.  Checking for water...");
            if((TerrainMeta.HeightMap.GetHeight(pos) - TerrainMeta.WaterMap.GetHeight(pos)) >= 0)
            {
                DoLog("Player not above water.");
                return false;
            }
            else
            {
                DoLog("Player is above water!");
                return true;
            }
        }

        private string NearMonument(BasePlayer player)
        {
            Vector3 pos = player.transform.position;
            string poss = pos.ToString();

            foreach(KeyValuePair<string, Vector3> entry in monPos)
            {
                string monname = entry.Key;
                Vector3 monvector = entry.Value;
                float realdistance = monSize[monname].z;
                monvector.y = pos.y;
                float dist = Vector3.Distance(pos, monvector);
                DoLog($"Checking {monname} dist: {dist.ToString()}, realdistance: {realdistance.ToString()}");
                if(dist < realdistance)
                {
                    DoLog($"Player in range of {monname}");
                    return monname;
                }
            }
            return null;
        }

        private string NearCave(BasePlayer player)
        {
            Vector3 pos = player.transform.position;
            string poss = pos.ToString();

            foreach(KeyValuePair<string, Vector3> entry in cavePos)
            {
                string cavename = entry.Key;
                float realdistance = 0f;

                if(cavename.Contains("Small"))
                {
                    realdistance = configData.Settings.CaveDistanceSmall;
                }
                else if(cavename.Contains("Large"))
                {
                    realdistance = configData.Settings.CaveDistanceLarge;
                }
                else if(cavename.Contains("Medium"))
                {
                    realdistance = configData.Settings.CaveDistanceMedium;
                }

                Vector3 cavevector = entry.Value;
                cavevector.y = pos.y;
                string cpos = cavevector.ToString();
                float dist = Vector3.Distance(pos, cavevector);

                if(dist < realdistance)
                {
                    DoLog($"NearCave: {cavename} nearby.");
                    return cavename;
                }
                else
                {
                    DoLog("NearCave: Not near this cave.");
                }
            }
            return null;
        }

        private string CheckPlayer(BasePlayer player, bool build = false, bool craft = false, bool origin = true, string mode = "home")
        {
            CargoShip onship = player.GetComponentInParent<CargoShip>();
            HotAirBalloon onballoon = player.GetComponentInParent<HotAirBalloon>();
            Lift inlift = player.GetComponentInParent<Lift>();
            Vector3 pos = player.transform.position;

            string monname = NearMonument(player);
            if (configData.Settings.InterruptTPOnMonument && monname != null)
            {
                return _("TooCloseToMon", player, monname);
            }
            bool allowcave = true;

            DoLog($"CheckPlayer(): called mode is {mode}");
            switch(mode)
            {
                case "home":
                    allowcave = configData.Home.AllowCave;
                    break;
                default:
                    DoLog("Skipping cave check...");
                    break;
            }
            if(!allowcave)
            {
                DoLog("Checking cave distance...");
                string cavename = NearCave(player);
                if(cavename != null)
                {
                    return "TooCloseToCave";
                }
            }

            if(configData.Settings.InterruptTPOnHostile && (mode == "bandit" || mode == "outpost"))
            {
                try
                {
                    BaseCombatEntity pc = player as BaseCombatEntity;
                    if(pc.IsHostile())
                    {
                        return "TPHostile";
                    }
                }
                catch {}
            }
            if(player.isMounted && configData.Settings.InterruptTPOnMounted)
            {
                return "TPMounted";
            }

            if (!player.IsAlive())
            {
                return "TPDead";
            }
            // Block if hurt if the config is enabled.  If the player is not the target in a tpa condition, allow.
            if (player.IsWounded() && origin && configData.Settings.InterruptTPOnHurt)
            {
                return "TPWounded";
            }

            if (player.metabolism.temperature.value <= configData.Settings.MinimumTemp && configData.Settings.InterruptTPOnCold)
            {
                return "TPTooCold";
            }
            if(player.metabolism.temperature.value >= configData.Settings.MaximumTemp && configData.Settings.InterruptTPOnHot)
            {
                return "TPTooHot";
            }

            if (configData.Settings.InterruptAboveWater && AboveWater(player))
            {
                return "TPAboveWater";
            }

            if (!build && !player.CanBuild())
            {
                return "TPBuildingBlocked";
            }

            if (player.IsSwimming() && configData.Settings.InterruptTPOnSwimming)
            {
                return "TPSwimming";
            }
            // This will have to do until we have a proper parent name for this
            if (monname?.Contains("Oilrig") == true && configData.Settings.InterruptTPOnRig)
            {
                return "TPOilRig";
            }

            if (monname?.Contains("Excavator") == true && configData.Settings.InterruptTPOnExcavator)
            {
                return "TPExcavator";
            }

            if (onship && configData.Settings.InterruptTPOnCargo)
            {
                return "TPCargoShip";
            }

            if (onballoon && configData.Settings.InterruptTPOnBalloon)
            {
                return "TPHotAirBalloon";
            }

            if (inlift && configData.Settings.InterruptTPOnLift)
            {
                return "TPBucketLift";
            }

            if (GetLift(pos) && configData.Settings.InterruptTPOnLift)
            {
                return "TPRegLift";
            }

            if (player.InSafeZone() && configData.Settings.InterruptTPOnSafe)
            {
                return "TPSafeZone";
            }

            if (!craft && player.inventory.crafting.queue.Count > 0)
            {
                return "TPCrafting";
            }

            return null;
        }

        private string CheckTargetLocation(BasePlayer player, Vector3 targetLocation, bool ubb, bool obb)
        {
            // ubb == UsableIntoBuildingBlocked
            // obb == CupOwnerAllowOnBuildingBlocked
            List<Collider> colliders = Pool.GetList<Collider>();
            Vis.Colliders(targetLocation, 0.2f, colliders, buildingLayer);
            bool denied = false;
            bool foundblock = false;
            int i = 0;

            foreach(Collider collider in colliders)
            {
                // First, check that there is a building block at the target
                BuildingBlock block = collider.GetComponentInParent<BuildingBlock>();
                i++;
                if(block != null)
                {
                    foundblock = true;
                    DoLog($"Found a block {i.ToString()}");
                    if(foundblock)
                    {
                        if(CheckCupboardBlock(block, player, obb))
                        {
                            denied = false;
                            DoLog("Cupboard either owned or there is no cupboard");
                        }
                        else if(ubb && (player.userID != block.OwnerID))
                        {
                            denied = false;
                            DoLog("Player does not own block, but UsableIntoBuildingBlocked=true");
                        }
                        else if(player.userID == block.OwnerID)
                        {
                            DoLog("Player owns block");

                            if(!player.IsBuildingBlocked(targetLocation, new Quaternion(), block.bounds))
                            {
                                DoLog("Player not BuildingBlocked. Likely unprotected building.");
                                denied = false;
                                break;
                            }
                            else if(ubb)
                            {
                                DoLog("Player not blocked because UsableIntoBuildingBlocked=true");
                                denied = false;
                                break;
                            }
                            else
                            {
                                DoLog("Player owns block but blocked by UsableIntoBuildingBlocked=false");
                                denied = true;
                                break;
                            }
                        }
                        else
                        {
                            DoLog("Player blocked");
                            denied = true;
                            break;
                        }
                    }
                }
            }
            Pool.FreeList(ref colliders);

            return denied ? "TPTargetBuildingBlocked" : null;
        }

        // Check that a building block is owned by/attached to a cupboard, allow tp if not blocked unless allowed by config
        private bool CheckCupboardBlock(BuildingBlock block, BasePlayer player, bool obb)
        {
            // obb == CupOwnerAllowOnBuildingBlocked
            BuildingManager.Building building = block.GetBuilding();
            if(building != null)
            {
                if (configData.debug)
                {
                    DoLog("Found building, checking privileges...");
                    DoLog($"Building ID: {building.ID}");
                }
                // cupboard overlap.  Check privs.
                if(building.buildingPrivileges == null)
                {
                    DoLog("Player has no privileges");
                    return false;
                }

                ulong hitEntityOwnerID = block.OwnerID != 0 ? block.OwnerID : 0;
                foreach(BuildingPrivlidge privs in building.buildingPrivileges)
                {
                    if(CupboardAuthCheck(privs, hitEntityOwnerID))
                    {
                        // player is authorized to the cupboard
                        DoLog("Player owns cupboard with auth");
                        return true;
                    }
                    else if(obb && player.userID == hitEntityOwnerID)
                    {
                        // player set the cupboard and is allowed in by config
                        DoLog("Player owns cupboard with no auth, but allowed by CupOwnerAllowOnBuildingBlocked=true");
                        return true;
                    }
                    else if(player.userID == hitEntityOwnerID)
                    {
                        // player set the cupboard but is blocked by config
                        DoLog("Player owns cupboard with no auth, but blocked by CupOwnerAllowOnBuildingBlocked=false");
                        return false;
                    }
                }
                DoLog("Building found but there was no auth.");
                return false;
            }
            DoLog("No cupboard or building found - we cannot tell the status of this block");
            return true;
        }

        private bool CupboardAuthCheck(BuildingPrivlidge priv, ulong hitEntityOwnerID)
        {
            foreach(ulong auth in priv.authorizedPlayers.Select(x => x.userid).ToArray())
            {
                if(auth == hitEntityOwnerID)
                {
                    DoLog("Player has auth");
                    return true;
                }
            }
            DoLog("Found no auth");
            return false;
        }

        private string CheckInsideBlock(Vector3 targetLocation)
        {
            List<BuildingBlock> blocks = Pool.GetList<BuildingBlock>();
            Vis.Entities(targetLocation + new Vector3(0, 0.25f), 0.1f, blocks, blockLayer);
            bool inside = blocks.Count > 0;
            Pool.FreeList(ref blocks);

            return inside ? "TPTargetInsideBlock" : null;
        }

        private string CheckItems(BasePlayer player)
        {
            foreach (KeyValuePair<int, string> blockedItem in ReverseBlockedItems)
            {
                if (player.inventory.containerMain.GetAmount(blockedItem.Key, true) > 0)
                {
                    return blockedItem.Value;
                }

                if (player.inventory.containerBelt.GetAmount(blockedItem.Key, true) > 0)
                {
                    return blockedItem.Value;
                }

                if (player.inventory.containerWear.GetAmount(blockedItem.Key, true) > 0)
                {
                    return blockedItem.Value;
                }
            }
            return null;
        }

        private string CheckFoundation(ulong userID, Vector3 position)
        {
            if(!configData.Home.ForceOnTopOfFoundation)
            {
                return null; // Foundation/floor not required
            }

            if (UnderneathFoundation(position))
            {
                return "HomeFoundationUnderneathFoundation";
            }

            List<BuildingBlock> entities = new List<BuildingBlock>();
            if(configData.Home.AllowAboveFoundation) // Can set on a foundation or floor
            {
                DoLog($"CheckFoundation() looking for foundation or floor at {position.ToString()}");
                entities = GetFoundationOrFloor(position);
            }
            else // Can only use foundation, not floor/ceiling
            {
                DoLog($"CheckFoundation() looking for foundation at {position.ToString()}");
                entities = GetFoundation(position);
            }

            if(entities.Count == 0)
            {
                return "HomeNoFoundation";
            }

            if (!configData.Home.CheckFoundationForOwner)
            {
                return null;
            }

            for (int i = 0; i < entities.Count; i++)
            {
                if(entities[i].OwnerID == userID)
                {
                    return null;
                }
                else if(IsFriend(userID, entities[i].OwnerID))
                {
                    return null;
                }
            }

            return "HomeFoundationNotFriendsOwned";
        }

        private BuildingBlock GetFoundationOwned(Vector3 position, ulong userID)
        {
            DoLog("GetFoundationOwned() called...");
            List<BuildingBlock> entities = GetFoundation(position);
            if(entities.Count == 0)
            {
                return null;
            }

            if (!configData.Home.CheckFoundationForOwner)
            {
                return entities[0];
            }

            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].OwnerID == userID)
                {
                    return entities[i];
                }
                else if(IsFriend(userID, entities[i].OwnerID))
                {
                    return entities[i];
                }
            }
            return null;
        }

        // Borrowed/modified from PreventLooting and Rewards
        // playerid = active player, ownerid = owner of building block, who may be offline
        private bool IsFriend(ulong playerid, ulong ownerid)
        {
            if(configData.Home.UseFriends && Friends != null)
            {
                DoLog("Checking Friends...");
                object fr = Friends?.CallHook("AreFriends", playerid, ownerid);
                if(fr != null && (bool)fr)
                {
                    DoLog("  IsFriend: true based on Friends plugin");
                    return true;
                }
            }
            if(configData.Home.UseClans && Clans != null)
            {
                DoLog("Checking Clans...");
                string playerclan = (string)Clans?.CallHook("GetClanOf", playerid);
                string ownerclan  = (string)Clans?.CallHook("GetClanOf", ownerid);
                if(playerclan == ownerclan && playerclan != null && ownerclan != null)
                {
                    DoLog("  IsFriend: true based on Clans plugin");
                    return true;
                }
            }
            if(configData.Home.UseTeams)
            {
                DoLog("Checking Rust teams...");
                BasePlayer player = BasePlayer.FindByID(playerid);
                if(player.currentTeam != (long)0)
                {
                    RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                    if(playerTeam == null)
                    {
                        return false;
                    }

                    if (playerTeam.members.Contains(ownerid))
                    {
                        DoLog("  IsFriend: true based on Rust teams");
                        return true;
                    }
                }
            }
            return false;
        }

        // Check that we are near the middle of a block.  Also check for high wall overlap
        private bool ValidBlock(BaseEntity entity, Vector3 position)
        {
            if(!configData.Settings.StrictFoundationCheck)
            {
                return true;
            }
            DoLog($"ValidBlock() called for {entity.ShortPrefabName}");
            Vector3 center = entity.CenterPoint();

            List<BaseEntity> ents = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(center, 1.5f, ents);
            foreach(BaseEntity wall in ents)
            {
                if(wall.name.Contains("external.high"))
                {
                    DoLog($"    Found: {wall.name} @ center {center.ToString()}, pos {position.ToString()}");
                    return false;
                }
            }
            DoLog($"  Checking block: {entity.name} @ center {center.ToString()}, pos: {position.ToString()}");
            if(entity.PrefabName.Contains("triangle.prefab"))
            {
                if(Math.Abs(center.x - position.x) < 0.45f && Math.Abs(center.z - position.z) < 0.45f)
                {
                    DoLog($"    Found: {entity.ShortPrefabName} @ center: {center.ToString()}, pos: {position.ToString()}");
                    return true;
                }
            }
            else if(entity.PrefabName.Contains("foundation.prefab") || entity.PrefabName.Contains("floor.prefab"))
            {
                if(Math.Abs(center.x - position.x) < 0.7f && Math.Abs(center.z - position.z) < 0.7f)
                {
                    DoLog($"    Found: {entity.ShortPrefabName} @ center: {center.ToString()}, pos: {position.ToString()}");
                    return true;
                }
            }

            return false;
        }

        private List<BuildingBlock> GetFoundation(Vector3 position)
        {
            RaycastHit hitinfo;
            List<BuildingBlock> entities = new List<BuildingBlock>();

            if(Physics.Raycast(position, Vector3.down, out hitinfo, 0.1f, blockLayer))
            {
                BaseEntity entity = hitinfo.GetEntity();
                if(entity.PrefabName.Contains("foundation") || position.y < entity.WorldSpaceBounds().ToBounds().max.y)
                {
                    if(ValidBlock(entity, position))
                    {
                        DoLog($"  GetFoundation() found {entity.PrefabName} at {entity.transform.position}");
                        entities.Add(entity as BuildingBlock);
                    }
                }
            }
            else
            {
                DoLog("  GetFoundation() none found.");
            }

            return entities;
        }

        private List<BuildingBlock> GetFloor(Vector3 position)
        {
            RaycastHit hitinfo;
            List<BuildingBlock> entities = new List<BuildingBlock>();

            if(Physics.Raycast(position, Vector3.down, out hitinfo, 0.11f, blockLayer))
            {
                BaseEntity entity = hitinfo.GetEntity();
                if(entity.PrefabName.Contains("floor"))
                {
                    DoLog($"  GetFloor() found {entity.PrefabName} at {entity.transform.position}");
                    entities.Add(entity as BuildingBlock);
                }
            }
            else
            {
                DoLog("  GetFloor() none found.");
            }

            return entities;
        }

        private List<BuildingBlock> GetFoundationOrFloor(Vector3 position)
        {
            RaycastHit hitinfo;
            List<BuildingBlock> entities = new List<BuildingBlock>();

            if(Physics.Raycast(position, Vector3.down, out hitinfo, 0.11f, blockLayer))
            {
                BaseEntity entity = hitinfo.GetEntity();
                if(entity.PrefabName.Contains("floor") || entity.PrefabName.Contains("foundation"))// || position.y < entity.WorldSpaceBounds().ToBounds().max.y))
                {
                    DoLog($"  GetFoundationOrFloor() found {entity.PrefabName} at {entity.transform.position}");
                    if(ValidBlock(entity, position))
                    {
                        entities.Add(entity as BuildingBlock);
                    }
                }
            }
            else
            {
                DoLog("  GetFoundationOrFloor() none found.");
            }

            return entities;
        }

        private bool CheckBoundaries(float x, float y, float z)
        {
            return x <= boundary && x >= -boundary && y < 2000 && y >= -100 && z <= boundary && z >= -boundary;
        }

        private Vector3 GetGround(Vector3 sourcePos)
        {
            if (!configData.Home.AllowAboveFoundation)
            {
                return sourcePos;
            }

            Vector3 newPos = sourcePos;
            newPos.y = TerrainMeta.HeightMap.GetHeight(newPos);
            sourcePos.y += .5f;
            RaycastHit hitinfo;
            bool done = false;

            DoLog("GetGround(): Looking for iceberg or cave");
            //if (Physics.SphereCast(sourcePos, .1f, Vector3.down, out hitinfo, 250, groundLayer))
            if(Physics.Raycast(sourcePos, Vector3.down, out hitinfo, 250f, groundLayer))
            {
                if((configData.Home.AllowIceberg && hitinfo.collider.name.Contains("iceberg")) || (configData.Home.AllowCave && hitinfo.collider.name.Contains("cave_")))
                {
                    DoLog("GetGround():   found iceberg or cave");
                    sourcePos.y = hitinfo.point.y;
                    done = true;
                }
                else
                {
                    MeshCollider mesh = hitinfo.collider.GetComponentInChildren<MeshCollider>();
                    if(mesh?.sharedMesh.name.Contains("rock_") == true)
                    {
                        sourcePos.y = hitinfo.point.y;
                        done = true;
                    }
                }
            }
            DoLog("GetGround(): Looking for cave or rock");
            //if(!configData.Home.AllowCave && Physics.SphereCast(sourcePos, .1f, Vector3.up, out hitinfo, 250, groundLayer) && hitinfo.collider.name.Contains("rock_"))
            if(!configData.Home.AllowCave && Physics.Raycast(sourcePos, Vector3.up, out hitinfo, 250f, groundLayer) && hitinfo.collider.name.Contains("rock_"))
            {
                DoLog("GetGround():   found cave or rock");
                sourcePos.y = newPos.y - 10;
                done = true;
            }
            return done ? sourcePos : newPos;
        }

        private bool GetLift(Vector3 position)
        {
            List<ProceduralLift> nearObjectsOfType = new List<ProceduralLift>();
            Vis.Entities(position, 0.5f, nearObjectsOfType);
            return nearObjectsOfType.Count > 0;
        }

        private Vector3 GetGroundBuilding(Vector3 sourcePos)
        {
            sourcePos.y = TerrainMeta.HeightMap.GetHeight(sourcePos);
            RaycastHit hitinfo;
            if (Physics.Raycast(sourcePos, Vector3.down, out hitinfo, buildingLayer))
            {
                sourcePos.y = Math.Max(hitinfo.point.y, sourcePos.y);
                return sourcePos;
            }
            if (Physics.Raycast(sourcePos, Vector3.up, out hitinfo, buildingLayer))
            {
                sourcePos.y = System.Math.Max(hitinfo.point.y, sourcePos.y);
            }

            return sourcePos;
        }

        private bool UnderneathFoundation(Vector3 position)
        {
            // Check for foundation half-height above where home was set
            foreach(RaycastHit hit in Physics.RaycastAll(position, Up, 2f, buildingLayer))
            {
                if (hit.GetCollider().name.Contains("foundation"))
                {
                    return true;
                }
            }
            // Check for foundation full-height above where home was set
            // Since you can't see from inside via ray, start above.
            foreach(RaycastHit hit in Physics.RaycastAll(position + Up + Up + Up + Up, Down, 2f, buildingLayer))
            {
                if (hit.GetCollider().name.Contains("foundation"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAllowed(BasePlayer player, string perm = null)
        {
            uint? playerAuthLevel = player.net?.connection?.authLevel;

            int requiredAuthLevel = 3;
            if(configData.Admin.UseableByModerators)
            {
                requiredAuthLevel = 1;
            }
            else if(configData.Admin.UseableByAdmins)
            {
                requiredAuthLevel = 2;
            }
            if (playerAuthLevel >= requiredAuthLevel)
            {
                return true;
            }

            return !string.IsNullOrEmpty(perm) && permission.UserHasPermission(player.UserIDString, perm);
        }

        private bool IsAllowedMsg(BasePlayer player, string perm = null)
        {
            if (IsAllowed(player, perm))
            {
                return true;
            }

            PrintMsg(player, "NotAllowed");
            return false;
        }

        private int GetHigher(BasePlayer player, Dictionary<string, int> limits, int limit)
        {
            foreach(KeyValuePair<string, int> l in limits)
            {
                if(permission.UserHasPermission(player.UserIDString, l.Key) && l.Value > limit)
                {
                    limit = l.Value;
                }
            }
            return limit;
        }

        private int GetLower(BasePlayer player, Dictionary<string, int> times, int time)
        {
            foreach(KeyValuePair<string, int> l in times)
            {
                if(permission.UserHasPermission(player.UserIDString, l.Key) && l.Value < time)
                {
                    time = l.Value;
                }
            }
            return time;
        }

        private void CheckPerms(Dictionary<string, int> limits)
        {
            foreach(KeyValuePair<string, int> limit in limits)
            {
                if(!permission.PermissionExists(limit.Key))
                {
                    permission.RegisterPermission(limit.Key, this);
                }
            }
        }
        #endregion

        #region Message
        private string _(string msgId, BasePlayer player, params object[] args)
        {
            string msg = lang.GetMessage(msgId, this, player?.UserIDString);
            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void PrintMsgL(BasePlayer player, string msgId, params object[] args)
        {
            if(player == null)
            {
                return;
            }

            PrintMsg(player, _(msgId, player, args));
        }

        private void PrintMsg(BasePlayer player, string msg)
        {
            if(player == null)
            {
                return;
            }

            SendReply(player, $"{configData.Settings.ChatName}{msg}");
        }
        #endregion

        #region DrawBox
        private static void DrawBox(BasePlayer player, Vector3 center, Quaternion rotation, Vector3 size)
        {
            size /= 2;
            Vector3 point1 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation);
            Vector3 point2 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation);
            Vector3 point3 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation);
            Vector3 point4 = RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation);
            Vector3 point5 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation);
            Vector3 point6 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation);
            Vector3 point7 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation);
            Vector3 point8 = RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation);

            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point3);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point1, point5);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point3);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point4, point8);

            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point6);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point5, point7);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point6, point2);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point6);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point8, point7);
            player.SendConsoleCommand("ddraw.line", 30f, Color.blue, point7, point3);
        }

        private static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation)
        {
            return (rotation * (point - pivot)) + pivot;
        }
        #endregion

        #region FindPlayer
        private ulong FindPlayersSingleId(string nameOrIdOrIp, BasePlayer player)
        {
            HashSet<BasePlayer> targets = FindPlayers(nameOrIdOrIp);
            if (targets.Count > 1)
            {
                PrintMsgL(player, "MultiplePlayers", string.Join(", ", targets.Select(p => p.displayName).ToArray()));
                return 0;
            }
            ulong userId;
            if (targets.Count == 0)
            {
                if (ulong.TryParse(nameOrIdOrIp, out userId))
                {
                    return userId;
                }

                PrintMsgL(player, "PlayerNotFound");
                return 0;
            }
            else
            {
                userId = targets.First().userID;
            }

            return userId;
        }

        private BasePlayer FindPlayersSingle(string nameOrIdOrIp, BasePlayer player)
        {
            HashSet<BasePlayer> targets = FindPlayers(nameOrIdOrIp);
            if (targets.Count == 0)
            {
                PrintMsgL(player, "PlayerNotFound");
                return null;
            }
            if (targets.Count > 1)
            {
                PrintMsgL(player, "MultiplePlayers", string.Join(", ", targets.Select(p => p.displayName).ToArray()));
                return null;
            }
            return targets.First();
        }

        private static HashSet<BasePlayer> FindPlayers(string nameOrIdOrIp)
        {
            HashSet<BasePlayer> players = new HashSet<BasePlayer>();
            if (string.IsNullOrEmpty(nameOrIdOrIp))
            {
                return players;
            }

            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString.Equals(nameOrIdOrIp))
                {
                    players.Add(activePlayer);
                }
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                {
                    players.Add(activePlayer);
                }
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(nameOrIdOrIp))
                {
                    players.Add(activePlayer);
                }
            }
            foreach (BasePlayer sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString.Equals(nameOrIdOrIp))
                {
                    players.Add(sleepingPlayer);
                }
                else if (!string.IsNullOrEmpty(sleepingPlayer.displayName) && sleepingPlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                {
                    players.Add(sleepingPlayer);
                }
            }
            return players;
        }

        private static List<BasePlayer> FindPlayersOnline(string nameOrIdOrIp)
        {
            List<BasePlayer> players = new List<BasePlayer>();
            if (string.IsNullOrEmpty(nameOrIdOrIp))
            {
                return players;
            }

            foreach (BasePlayer activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString.Equals(nameOrIdOrIp))
                {
                    players.Add(activePlayer);
                }
                else if (!string.IsNullOrEmpty(activePlayer.displayName) && activePlayer.displayName.Contains(nameOrIdOrIp, CompareOptions.IgnoreCase))
                {
                    players.Add(activePlayer);
                }
                else if (activePlayer.net?.connection != null && activePlayer.net.connection.ipaddress.Equals(nameOrIdOrIp))
                {
                    players.Add(activePlayer);
                }
            }
            return players;
        }
        #endregion

        #region API
        private Dictionary<string, Vector3> GetHomes(object playerObj)
        {
            if(playerObj == null)
            {
                return null;
            }

            if (playerObj is string)
            {
                playerObj = Convert.ToUInt64(playerObj);
            }

            if (!(playerObj is ulong))
            {
                throw new ArgumentException("playerObj");
            }

            ulong playerId = (ulong)playerObj;
            HomeData homeData;
            if (!Home.TryGetValue(playerId, out homeData) || homeData.Locations.Count == 0)
            {
                return null;
            }

            return homeData.Locations;
        }

        private int GetLimitRemaining(BasePlayer player, string type)
        {
            if(player == null || string.IsNullOrEmpty(type))
            {
                return 0;
            }

            string currentDate = DateTime.Now.ToString("d");
            int limit;
            int remaining = -1;
            switch (type.ToLower())
            {
                case "home":
                    limit = GetHigher(player, configData.Home.VIPDailyLimits, configData.Home.DailyLimit);
                    HomeData homeData;
                    if(!Home.TryGetValue(player.userID, out homeData))
                    {
                        Home[player.userID] = homeData = new HomeData();
                    }
                    if(homeData.Teleports.Date != currentDate)
                    {
                        homeData.Teleports.Amount = 0;
                        homeData.Teleports.Date = currentDate;
                    }
                    if(limit > 0)
                    {
                        remaining = limit - homeData.Teleports.Amount;
                    }
                    break;
                case "town":
                    limit = GetHigher(player, configData.Town.VIPDailyLimits, configData.Town.DailyLimit);
                    TeleportData townData;
                    if(!Town.TryGetValue(player.userID, out townData))
                    {
                        Town[player.userID] = townData = new TeleportData();
                    }
                    if(townData.Date != currentDate)
                    {
                        townData.Amount = 0;
                        townData.Date = currentDate;
                    }
                    if(limit > 0)
                    {
                        remaining = limit - townData.Amount;
                    }
                    break;
                case "outpost":
                    limit = GetHigher(player, configData.Outpost.VIPDailyLimits, configData.Outpost.DailyLimit);
                    TeleportData outpostData;
                    if(!Outpost.TryGetValue(player.userID, out outpostData))
                    {
                        Outpost[player.userID] = outpostData = new TeleportData();
                    }
                    if(outpostData.Date != currentDate)
                    {
                        outpostData.Amount = 0;
                        outpostData.Date = currentDate;
                    }
                    if(limit > 0)
                    {
                        remaining = limit - outpostData.Amount;
                    }
                    break;
                case "bandit":
                    limit = GetHigher(player, configData.Bandit.VIPDailyLimits, configData.Bandit.DailyLimit);
                    TeleportData banditData;
                    if(!Bandit.TryGetValue(player.userID, out banditData))
                    {
                        Bandit[player.userID] = banditData = new TeleportData();
                    }
                    if(banditData.Date != currentDate)
                    {
                        banditData.Amount = 0;
                        banditData.Date = currentDate;
                    }
                    if(limit > 0)
                    {
                        remaining = limit - banditData.Amount;
                    }
                    break;
                case "tpr":
                    limit = GetHigher(player, configData.TPR.VIPDailyLimits, configData.TPR.DailyLimit);
                    TeleportData tprData;
                    if(!TPR.TryGetValue(player.userID, out tprData))
                    {
                        TPR[player.userID] = tprData = new TeleportData();
                    }
                    if(tprData.Date != currentDate)
                    {
                        tprData.Amount = 0;
                        tprData.Date = currentDate;
                    }
                    if(limit > 0)
                    {
                        remaining = limit - tprData.Amount;
                    }
                    break;
            }
            return remaining;
        }

        private int GetCooldownRemaining(BasePlayer player, string type)
        {
            if(player == null || string.IsNullOrEmpty(type))
            {
                return 0;
            }

            string currentDate = DateTime.Now.ToString("d");
            int timestamp = Facepunch.Math.Epoch.Current;
            int cooldown;
            int remaining = -1;
            switch(type.ToLower())
            {
                case "home":
                    cooldown = GetLower(player, configData.Home.VIPCooldowns, configData.Home.Cooldown);
                    HomeData homeData;
                    if(!Home.TryGetValue(player.userID, out homeData))
                    {
                        Home[player.userID] = homeData = new HomeData();
                    }
                    if(homeData.Teleports.Date != currentDate)
                    {
                        homeData.Teleports.Amount = 0;
                        homeData.Teleports.Date = currentDate;
                    }
                    if(cooldown > 0 && timestamp - homeData.Teleports.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - homeData.Teleports.Timestamp);
                    }
                    break;
                case "town":
                    cooldown = GetLower(player, configData.Town.VIPCooldowns, configData.Town.Cooldown);
                    TeleportData townData;
                    if(!Town.TryGetValue(player.userID, out townData))
                    {
                        Town[player.userID] = townData = new TeleportData();
                    }
                    if(townData.Date != currentDate)
                    {
                        townData.Amount = 0;
                        townData.Date = currentDate;
                    }
                    if(cooldown > 0 && timestamp - townData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - townData.Timestamp);
                    }
                    break;
                case "outpost":
                    cooldown = GetLower(player, configData.Outpost.VIPCooldowns, configData.Outpost.Cooldown);
                    TeleportData outpostData;
                    if(!Outpost.TryGetValue(player.userID, out outpostData))
                    {
                        Outpost[player.userID] = outpostData = new TeleportData();
                    }
                    if(outpostData.Date != currentDate)
                    {
                        outpostData.Amount = 0;
                        outpostData.Date = currentDate;
                    }
                    if(cooldown > 0 && timestamp - outpostData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - outpostData.Timestamp);
                    }
                    break;
                case "bandit":
                    cooldown = GetLower(player, configData.Bandit.VIPCooldowns, configData.Bandit.Cooldown);
                    TeleportData banditData;
                    if(!Bandit.TryGetValue(player.userID, out banditData))
                    {
                        Bandit[player.userID] = banditData = new TeleportData();
                    }
                    if(banditData.Date != currentDate)
                    {
                        banditData.Amount = 0;
                        banditData.Date = currentDate;
                    }
                    if(cooldown > 0 && timestamp - banditData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - banditData.Timestamp);
                    }
                    break;
                case "tpr":
                    cooldown = GetLower(player, configData.TPR.VIPCooldowns, configData.TPR.Cooldown);
                    TeleportData tprData;
                    if(!TPR.TryGetValue(player.userID, out tprData))
                    {
                        TPR[player.userID] = tprData = new TeleportData();
                    }
                    if(tprData.Date != currentDate)
                    {
                        tprData.Amount = 0;
                        tprData.Date = currentDate;
                    }
                    if(cooldown > 0 && timestamp - tprData.Timestamp < cooldown)
                    {
                        remaining = cooldown - (timestamp - tprData.Timestamp);
                    }
                    break;
            }
            return remaining;
        }
        #endregion

        private class UnityVector3Converter : JsonConverter
        {
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                Vector3 vector = (Vector3)value;
                writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.String)
                {
                    string[] values = reader.Value.ToString().Trim().Split(' ');
                    return new Vector3(Convert.ToSingle(values[0]), Convert.ToSingle(values[1]), Convert.ToSingle(values[2]));
                }
                JObject o = JObject.Load(reader);
                return new Vector3(Convert.ToSingle(o["x"]), Convert.ToSingle(o["y"]), Convert.ToSingle(o["z"]));
            }

            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(Vector3);
            }
        }

        private class CustomComparerDictionaryCreationConverter<T> : CustomCreationConverter<IDictionary>
        {
            private readonly IEqualityComparer<T> comparer;

            public CustomComparerDictionaryCreationConverter(IEqualityComparer<T> comparer)
            {
                if (comparer == null)
                {
                    throw new ArgumentNullException(nameof(comparer));
                }

                this.comparer = comparer;
            }

            public override bool CanConvert(Type objectType)
            {
                return HasCompatibleInterface(objectType) && HasCompatibleConstructor(objectType);
            }

            private static bool HasCompatibleInterface(Type objectType)
            {
                return objectType.GetInterfaces().Any(i => HasGenericTypeDefinition(i, typeof(IDictionary<,>)) && typeof(T).IsAssignableFrom(i.GetGenericArguments().First()));
            }

            private static bool HasGenericTypeDefinition(Type objectType, Type typeDefinition)
            {
                return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeDefinition;
            }

            private static bool HasCompatibleConstructor(Type objectType)
            {
                return objectType.GetConstructor(new[] { typeof(IEqualityComparer<T>) }) != null;
            }

            public override IDictionary Create(Type objectType)
            {
                return Activator.CreateInstance(objectType, comparer) as IDictionary;
            }
        }

        [HookMethod("SendHelpText")]
        private void SendHelpText(BasePlayer player)
        {
            PrintMsgL(player, "<size=14>RTeleportation</size> by <color=#ce422b>RFC1920</color>\n<color=#ffd479>/sethome NAME</color> - Set home on current foundation\n<color=#ffd479>/home NAME</color> - Go to one of your homes\n<color=#ffd479>/home list</color> - List your homes\n<color=#ffd479>/town</color> - Go to town, if set\n/tpb - Go back to previous location\n/tpr PLAYER - Request teleport to PLAYER\n/tpa - Accept teleport request");
        }
    }
}
