using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using EvuMiscCompat.Core;
using HarmonyLib;
using SoftReferenceableAssets;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EvuMiscCompat;

static class LocationResetPlayer
{
    const string PluginGuid = "com.orianaventure.mod.LocationReset";

    const string DungeonSplitterGuid = "dungeon_splitter";

    const string PolicyRpc = "EvuMiscCompat.DungeonReset.Policy";

    const string PolicyRequestRpc = "EvuMiscCompat.DungeonReset.PolicyRequest";

    const string ConsumeRpc = "EvuMiscCompat.DungeonReset.Consume";

    const string ConsumedRpc = "EvuMiscCompat.DungeonReset.Consumed";

    const float SkyHeight = 4000f;

    static readonly CooldownLedger Ledger = new CooldownLedger();

    static Plugin _plugin = null!;

    static ConfigEntry<bool> _enabled = null!;

    static ConfigEntry<int> _cooldownMinutes = null!;

    static ConfigEntry<string> _confirmation = null!;

    static ConfigEntry<float> _searchRadius = null!;

    static MethodInfo? _tryReset;

    static PropertyInfo? _instance;

    static bool _rpc;

    static bool _policyRequested;

    static bool _policyMissing;

    static bool _popup;

    static bool _closing;

    static float _pendingSince;

    static Rules? _remote;

    static DungeonCandidate? _pending;

    internal static bool ResetReady { get; private set; }

    internal static void Bind(Plugin plugin)
    {
        _plugin = plugin;
        _enabled = plugin.Config.Bind(
            "Location reset",
            "Enabled",
            true,
            "Allow players to reset the nearest dungeon from chat with resetdungeon. Turn this off to leave dungeon resets to Venture Location Reset's admin console command.");
        _cooldownMinutes = plugin.Config.Bind(
            "Location reset",
            "Cooldown minutes",
            10,
            new ConfigDescription(
                "Minutes a player must wait between force resets. 0 disables the limit.",
                new AcceptableValueRange<int>(0, 1440)));
        _confirmation = plugin.Config.Bind(
            "Location reset",
            "Require confirmation",
            "On nearby activity",
            new ConfigDescription(
                "Always asks before every reset. On nearby activity asks when a grave, player-built piece, or terrain change is in the reset area. Never skips the dialog. A player inside the area always blocks the reset.",
                new AcceptableValueList<string>("Always", "On nearby activity", "Never")));
        _searchRadius = plugin.Config.Bind(
            "Location reset",
            "Search radius",
            20f,
            new ConfigDescription(
                "How close a player must be to a dungeon entrance, in meters.",
                new AcceptableValueRange<float>(1f, 100f)));
        BindReset();
    }

    internal static bool TryConsume(string text)
    {
        if (!LocationResetRules.IsResetDungeonCommand(text))
        {
            return false;
        }

        if (!ResetReady)
        {
            return false;
        }

        if (_popup && !UnifiedPopup.IsVisible())
        {
            _popup = false;
        }

        if (_pending != null && Time.time - _pendingSince > 5f)
        {
            _pending = null;
        }

        if (_popup || _pending != null)
        {
            Say("A dungeon reset is already pending.");
            return true;
        }

        if (!TryRules(out var rules, out var unavailable))
        {
            Say(unavailable);
            return true;
        }

        if (!rules.Enabled)
        {
            Say("Player dungeon reset is turned off.");
            return true;
        }

        Run(rules);
        return true;
    }

    internal static void EnsureRpc()
    {
        if (_rpc || ZRoutedRpc.instance == null)
        {
            return;
        }

        ZRoutedRpc.instance.Register<bool, int, int, float>(PolicyRpc, OnPolicy);
        ZRoutedRpc.instance.Register(PolicyRequestRpc, (Action<long>)OnPolicyRequest);
        ZRoutedRpc.instance.Register(ConsumeRpc, (Action<long>)OnConsume);
        ZRoutedRpc.instance.Register<bool, int>(ConsumedRpc, OnConsumed);
        _rpc = true;
    }

    internal static void SendPolicy(long peerId)
    {
        EnsureRpc();
        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        var rules = LocalRules();
        ZRoutedRpc.instance.InvokeRoutedRPC(
            peerId,
            PolicyRpc,
            rules.Enabled,
            rules.CooldownMinutes,
            (int)rules.Confirmation,
            rules.SearchRadius);
    }

    internal static void RequestPolicy()
    {
        EnsureRpc();
        if (ZNet.instance == null || ZNet.instance.IsServer() || ZRoutedRpc.instance == null)
        {
            return;
        }

        if (_remote != null || _policyRequested)
        {
            return;
        }

        _policyRequested = true;
        ZRoutedRpc.instance.InvokeRoutedRPC(PolicyRequestRpc);
        _plugin.StartCoroutine(PolicyTimeout());
    }

    static void BindReset()
    {
        ResetReady = false;
        if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out var plugin) || plugin.Instance == null)
        {
            Plugin.Log.LogInfo("Venture Location Reset is not loaded. Player dungeon reset is idle.");
            return;
        }

        var type = plugin.Instance.GetType().Assembly.GetType("VentureValheim.LocationReset.LocationReset");
        _instance = type?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        _tryReset = type?.GetMethod("TryReset", new[] { typeof(LocationProxy), typeof(int), typeof(bool) });
        ResetReady = _instance != null && _tryReset != null;
        if (ResetReady)
        {
            Plugin.Log.LogInfo("Player dungeon reset is available.");
            return;
        }

        Plugin.Log.LogWarning("Venture Location Reset is loaded, but TryReset could not be found.");
    }

    static IEnumerator PolicyTimeout()
    {
        yield return new WaitForSeconds(5f);
        if (_remote == null)
        {
            _policyMissing = true;
            _policyRequested = false;
        }
    }

    static bool TryRules(out Rules rules, out string unavailable)
    {
        if (ZNet.instance != null && !ZNet.instance.IsServer())
        {
            if (_remote == null)
            {
                RequestPolicy();
                rules = LocalRules();
                unavailable = _policyMissing
                    ? "This server does not allow player dungeon resets."
                    : "Still waiting on the server for dungeon reset rules.";
                return false;
            }

            rules = _remote;
            unavailable = "";
            return true;
        }

        rules = LocalRules();
        unavailable = "";
        return true;
    }

    static Rules LocalRules()
    {
        if (!LocationResetRules.TryParseConfirmation(_confirmation.Value, out var confirmation))
        {
            confirmation = ConfirmationMode.OnNearbyActivity;
        }

        return new Rules(
            _enabled.Value,
            Math.Max(0, _cooldownMinutes.Value),
            confirmation,
            LocationResetRules.ClampSearchRadius(_searchRadius.Value));
    }

    static void OnPolicy(long sender, bool enabled, int cooldownMinutes, int confirmation, float searchRadius)
    {
        if (confirmation < 0 || confirmation > (int)ConfirmationMode.Never)
        {
            confirmation = (int)ConfirmationMode.OnNearbyActivity;
        }

        _remote = new Rules(
            enabled,
            Math.Max(0, cooldownMinutes),
            (ConfirmationMode)confirmation,
            LocationResetRules.ClampSearchRadius(searchRadius));
        _policyMissing = false;
    }

    static void OnPolicyRequest(long sender)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        SendPolicy(sender);
    }

    static void OnConsume(long sender)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        var rules = LocalRules();
        if (!rules.Enabled)
        {
            Reply(sender, false, -1);
            return;
        }

        var decision = Ledger.TryStart(sender, DateTimeOffset.UtcNow, rules.CooldownMinutes);
        Reply(sender, decision.Allowed, decision.RemainingSeconds);
    }

    static void OnConsumed(long sender, bool allowed, int remainingSeconds)
    {
        var target = _pending;
        _pending = null;
        if (target == null)
        {
            return;
        }

        if (!allowed)
        {
            Say(remainingSeconds < 0 ? "Player dungeon reset is turned off." : RemainingText(remainingSeconds));
            return;
        }

        if (PlayersInArea(target))
        {
            Say("A player is in the reset area.");
            return;
        }

        Finish(target);
    }

    static void Reply(long sender, bool allowed, int remainingSeconds)
    {
        if (ZRoutedRpc.instance == null)
        {
            return;
        }

        ZRoutedRpc.instance.InvokeRoutedRPC(sender, ConsumedRpc, allowed, remainingSeconds);
    }

    static void Run(Rules rules)
    {
        var player = Player.m_localPlayer;
        if (player == null || ZoneSystem.instance == null)
        {
            Say("Dungeon reset is not available yet.");
            return;
        }

        var target = FindNearest(player.transform.position, rules.SearchRadius);
        if (target == null)
        {
            Say("No dungeon is close enough to reset.");
            return;
        }

        if (target.Sky && Chainloader.PluginInfos.ContainsKey(DungeonSplitterGuid))
        {
            Say("Sky dungeons cannot be reset while Dungeon Splitter is installed.");
            return;
        }

        if (!OwnsGenerator(target.Generator))
        {
            Say("This dungeon is not loaded on your client yet.");
            return;
        }

        Scan(target);
        var decision = LocationResetRules.Decide(target.PlayersInArea, target.NearbyActivity, rules.Confirmation);
        if (decision == ResetDecision.Refuse)
        {
            Say("A player is in the reset area.");
            return;
        }

        if (decision == ResetDecision.Confirm)
        {
            Ask(target, rules);
            return;
        }

        StartReset(target, rules);
    }

    static void Ask(DungeonCandidate target, Rules rules)
    {
        if (!UnifiedPopup.IsAvailable())
        {
            Say("The confirmation dialog is not available.");
            return;
        }

        _popup = true;
        _closing = false;
        UnifiedPopup.Push(new YesNoPopup(
            "Reset dungeon?",
            PopupBody(target),
            () => Answer(target, rules, accepted: true),
            () => Answer(target, rules, accepted: false),
            false,
            false));
        HideCover();
    }

    static void Answer(DungeonCandidate target, Rules rules, bool accepted)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        _plugin.StartCoroutine(AnswerNextFrame(target, rules, accepted));
    }

    static IEnumerator AnswerNextFrame(DungeonCandidate target, Rules rules, bool accepted)
    {
        yield return null;
        _popup = false;
        _closing = false;
        if (UnifiedPopup.IsVisible())
        {
            UnifiedPopup.Pop();
        }

        if (accepted)
        {
            Confirm(target, rules);
        }
    }

    static void HideCover()
    {
        var cover = UnifiedPopup.instance != null ? UnifiedPopup.instance.fullScreenBackgroundCover : null;
        if (cover != null)
        {
            cover.SetActive(false);
        }
    }

    static void Confirm(DungeonCandidate target, Rules rules)
    {
        _popup = false;
        if (!OwnsGenerator(target.Generator))
        {
            Say("This dungeon is not loaded on your client yet.");
            return;
        }

        if (PlayersInArea(target))
        {
            Say("A player is in the reset area.");
            return;
        }

        StartReset(target, rules);
    }

    static void StartReset(DungeonCandidate target, Rules rules)
    {
        if (ZNet.instance == null)
        {
            Say("Dungeon reset is not available yet.");
            return;
        }

        if (ZNet.instance.IsServer())
        {
            var player = Player.m_localPlayer;
            if (player == null)
            {
                return;
            }

            var decision = Ledger.TryStart(player.GetPlayerID(), DateTimeOffset.UtcNow, rules.CooldownMinutes);
            if (!decision.Allowed)
            {
                Say(RemainingText(decision.RemainingSeconds));
                return;
            }

            Finish(target);
            return;
        }

        if (ZRoutedRpc.instance == null)
        {
            Say("Dungeon reset is not available yet.");
            return;
        }

        _pending = target;
        _pendingSince = Time.time;
        ZRoutedRpc.instance.InvokeRoutedRPC(ConsumeRpc);
    }

    static void Finish(DungeonCandidate target)
    {
        if (target.Proxy == null || _tryReset == null || _instance == null)
        {
            Say("The dungeon reset failed.");
            return;
        }

        var instance = _instance.GetValue(null);
        if (instance == null)
        {
            Say("The dungeon reset failed.");
            return;
        }

        try
        {
            _tryReset.Invoke(instance, new object[] { target.Proxy, target.Hash, true });
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError(exception);
            Say("The dungeon reset failed.");
            return;
        }

        Say("Resetting " + target.Name + ".");
        Plugin.Log.LogInfo("Player reset " + target.Name + " at " + target.Entrance + ".");
    }

    static DungeonCandidate? FindNearest(Vector3 playerPosition, float radius)
    {
        DungeonCandidate? best = null;
        var bestDistance = radius;
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var proxy = roots[i].GetComponent<LocationProxy>();
            if (proxy == null)
            {
                continue;
            }

            var entrance = proxy.transform.position;
            if (!InRange(playerPosition, entrance, radius))
            {
                continue;
            }

            var distance = HorizontalDistance(playerPosition, entrance);
            if (best != null && distance >= bestDistance)
            {
                continue;
            }

            var candidate = Inspect(proxy, entrance);
            if (candidate == null)
            {
                continue;
            }

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    static DungeonCandidate? Inspect(LocationProxy proxy, Vector3 entrance)
    {
        var hash = LocationHash(proxy);
        if (hash == 0 || ZoneSystem.instance == null)
        {
            return null;
        }

        var zone = ZoneSystem.instance.GetLocation(hash);
        if (zone == null || !zone.m_prefab.IsValid)
        {
            return null;
        }

        var held = false;
        if (!zone.m_prefab.IsLoaded)
        {
            zone.m_prefab.Load();
            held = true;
        }

        try
        {
            var asset = zone.m_prefab.Asset;
            if (asset == null)
            {
                return null;
            }

            var location = asset.GetComponent<Location>();
            if (location == null)
            {
                return null;
            }

            var generator = location.m_generator;
            var sky = location.m_hasInterior || (generator != null && generator.transform.position.y > SkyHeight);
            if (!sky && generator == null)
            {
                return null;
            }

            var generatorLocal = generator != null ? generator.transform.localPosition : Vector3.zero;
            var groundRadius = zone.m_exteriorRadius;
            var skyRadius = zone.m_interiorRadius;
            var spawned = FindGenerator(entrance + generatorLocal, Mathf.Max(groundRadius, skyRadius));
            if (spawned != null)
            {
                if (sky)
                {
                    skyRadius = DungeonRadius(spawned);
                }
                else
                {
                    groundRadius += 5f;
                }
            }

            var name = string.IsNullOrEmpty(zone.m_prefabName) ? "dungeon" : zone.m_prefabName;
            return new DungeonCandidate(proxy, hash, name, sky, entrance, groundRadius, skyRadius, spawned);
        }
        finally
        {
            if (held)
            {
                zone.m_prefab.Release();
            }
        }
    }

    static int LocationHash(LocationProxy proxy)
    {
        var view = proxy.m_nview;
        if (view == null || !view.IsValid())
        {
            return 0;
        }

        var zdo = view.GetZDO();
        if (zdo == null)
        {
            return 0;
        }

        return zdo.GetInt(ZDOVars.s_location, 0);
    }

    static DungeonGenerator? FindGenerator(Vector3 center, float radius)
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var generator = roots[i].GetComponent<DungeonGenerator>();
            if (generator != null && InRange(center, roots[i].transform.position, radius))
            {
                return generator;
            }
        }

        return null;
    }

    static float DungeonRadius(DungeonGenerator generator)
    {
        var size = generator.m_zoneSize;
        var x = size.x / 2f;
        var z = size.z / 2f;
        return Mathf.Sqrt((x * x) + (z * z));
    }

    static void Scan(DungeonCandidate target)
    {
        var players = false;
        var grave = false;
        var piece = false;
        var terrain = false;
        var commander = Player.m_localPlayer;
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (var i = 0; i < roots.Length; i++)
        {
            var go = roots[i];
            var position = go.transform.position;
            var skyBand = position.y >= SkyHeight;
            var inside = skyBand
                ? target.Sky && InRange(target.Entrance, position, target.SkyRadius)
                : InRange(target.Entrance, position, target.GroundRadius);
            if (!inside)
            {
                continue;
            }

            var player = go.GetComponent<Player>();
            if (player != null)
            {
                if (skyBand || player != commander)
                {
                    players = true;
                }

                continue;
            }

            if (go.GetComponent<TombStone>() != null)
            {
                grave = true;
                continue;
            }

            var built = go.GetComponent<Piece>();
            if (built != null && built.GetCreator() != 0L)
            {
                piece = true;
            }
        }

        var modifiers = TerrainModifier.GetAllInstances();
        if (modifiers != null)
        {
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier != null && InRange(target.Entrance, modifier.transform.position, target.GroundRadius))
                {
                    terrain = true;
                }
            }
        }

        target.PlayersInArea = players;
        target.NearbyActivity = grave || piece || terrain;
        target.Grave = grave;
        target.PlayerPiece = piece;
        target.Terrain = terrain;
    }

    static bool PlayersInArea(DungeonCandidate target)
    {
        Scan(target);
        return target.PlayersInArea;
    }

    static bool OwnsGenerator(DungeonGenerator? generator)
    {
        if (generator == null)
        {
            return true;
        }

        var view = generator.m_nview;
        return view != null && view.IsValid() && view.IsOwner();
    }

    static bool InRange(Vector3 center, Vector3 position, float radius)
    {
        var dx = center.x - position.x;
        var dz = center.z - position.z;
        return (dx * dx) + (dz * dz) <= radius * radius;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        var dx = a.x - b.x;
        var dz = a.z - b.z;
        return Mathf.Sqrt((dx * dx) + (dz * dz));
    }

    static string PopupBody(DungeonCandidate target)
    {
        var body = "Reset " + target.Name + "?";
        var parts = new List<string>();
        if (target.Grave)
        {
            parts.Add("graves");
        }

        if (target.PlayerPiece)
        {
            parts.Add("player-built pieces");
        }

        if (target.Terrain)
        {
            parts.Add("terrain changes");
        }

        if (parts.Count == 0)
        {
            return body;
        }

        return body + "\n\nThis will remove " + Join(parts) + " in the reset area.";
    }

    static string Join(List<string> parts)
    {
        if (parts.Count == 1)
        {
            return parts[0];
        }

        if (parts.Count == 2)
        {
            return parts[0] + " and " + parts[1];
        }

        return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + ", and " + parts[parts.Count - 1];
    }

    static string RemainingText(int seconds)
    {
        if (seconds < 60)
        {
            return "You can reset a dungeon again in " + seconds + (seconds == 1 ? " second." : " seconds.");
        }

        var minutes = (seconds + 59) / 60;
        return "You can reset a dungeon again in " + minutes + (minutes == 1 ? " minute." : " minutes.");
    }

    static void Say(string text)
    {
        var player = Player.m_localPlayer;
        if (player != null)
        {
            player.Message(MessageHud.MessageType.Center, text, 0, null, false);
        }

        Plugin.Log.LogInfo(text);
    }

    sealed class Rules
    {
        public Rules(bool enabled, int cooldownMinutes, ConfirmationMode confirmation, float searchRadius)
        {
            Enabled = enabled;
            CooldownMinutes = cooldownMinutes;
            Confirmation = confirmation;
            SearchRadius = searchRadius;
        }

        public bool Enabled { get; }

        public int CooldownMinutes { get; }

        public ConfirmationMode Confirmation { get; }

        public float SearchRadius { get; }
    }

    sealed class DungeonCandidate
    {
        public DungeonCandidate(
            LocationProxy proxy,
            int hash,
            string name,
            bool sky,
            Vector3 entrance,
            float groundRadius,
            float skyRadius,
            DungeonGenerator? generator)
        {
            Proxy = proxy;
            Hash = hash;
            Name = name;
            Sky = sky;
            Entrance = entrance;
            GroundRadius = groundRadius;
            SkyRadius = skyRadius;
            Generator = generator;
        }

        public LocationProxy Proxy { get; }

        public int Hash { get; }

        public string Name { get; }

        public bool Sky { get; }

        public Vector3 Entrance { get; }

        public float GroundRadius { get; }

        public float SkyRadius { get; }

        public DungeonGenerator? Generator { get; }

        public bool PlayersInArea { get; set; }

        public bool NearbyActivity { get; set; }

        public bool Grave { get; set; }

        public bool PlayerPiece { get; set; }

        public bool Terrain { get; set; }
    }
}

[HarmonyPatch(typeof(Chat), nameof(Chat.InputText))]
static class LocationResetChatPatch
{
    static readonly FieldInfo InputField = typeof(Terminal).GetField("m_input", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    static bool Prefix(Chat __instance)
    {
        var input = InputField?.GetValue(__instance);
        var textProperty = input?.GetType().GetProperty("text");
        if (textProperty?.GetValue(input) is not string text || !LocationResetPlayer.TryConsume(text))
        {
            return true;
        }

        textProperty.SetValue(input, string.Empty);
        return false;
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
static class LocationResetRpcPatch
{
    static void Postfix()
    {
        LocationResetPlayer.EnsureRpc();
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_PeerInfo))]
static class LocationResetPolicyRequestPatch
{
    static void Postfix()
    {
        LocationResetPlayer.RequestPolicy();
    }
}

[HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnNewPeer))]
static class LocationResetPolicyPatch
{
    static void Postfix(long peerID)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        LocationResetPlayer.SendPolicy(peerID);
    }
}
