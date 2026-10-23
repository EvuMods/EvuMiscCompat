using System;
using System.Collections.Generic;

namespace EvuMiscCompat.Core;

public enum ConfirmationMode
{
    Always,
    OnNearbyActivity,
    Never,
}

public enum ResetDecision
{
    Refuse,
    Confirm,
    Allow,
}

public readonly struct CooldownDecision
{
    public CooldownDecision(bool allowed, int remainingSeconds)
    {
        Allowed = allowed;
        RemainingSeconds = remainingSeconds;
    }

    public bool Allowed { get; }

    public int RemainingSeconds { get; }
}

public static class LocationResetRules
{
    public const string CommandName = "resetdungeon";

    public static bool IsResetDungeonCommand(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed[0] == '/')
        {
            trimmed = trimmed.Substring(1).TrimStart();
        }

        return trimmed.Equals(CommandName, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseConfirmation(string text, out ConfirmationMode mode)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            mode = default;
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Equals("Always", StringComparison.OrdinalIgnoreCase))
        {
            mode = ConfirmationMode.Always;
            return true;
        }

        if (trimmed.Equals("Never", StringComparison.OrdinalIgnoreCase))
        {
            mode = ConfirmationMode.Never;
            return true;
        }

        if (trimmed.Equals("On nearby activity", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("OnNearbyActivity", StringComparison.OrdinalIgnoreCase))
        {
            mode = ConfirmationMode.OnNearbyActivity;
            return true;
        }

        mode = default;
        return false;
    }

    public static ResetDecision Decide(bool playersInArea, bool nearbyActivity, ConfirmationMode mode)
    {
        if (playersInArea)
        {
            return ResetDecision.Refuse;
        }

        if (mode == ConfirmationMode.Always)
        {
            return ResetDecision.Confirm;
        }

        if (mode == ConfirmationMode.OnNearbyActivity && nearbyActivity)
        {
            return ResetDecision.Confirm;
        }

        return ResetDecision.Allow;
    }

    public static CooldownDecision CheckCooldown(DateTimeOffset? lastStartedUtc, DateTimeOffset now, int cooldownMinutes)
    {
        if (cooldownMinutes <= 0 || lastStartedUtc == null)
        {
            return new CooldownDecision(true, 0);
        }

        var window = TimeSpan.FromMinutes(cooldownMinutes);
        var elapsed = now - lastStartedUtc.Value;
        if (elapsed >= window)
        {
            return new CooldownDecision(true, 0);
        }

        var seconds = (int)Math.Ceiling((window - elapsed).TotalSeconds);
        if (seconds < 1)
        {
            seconds = 1;
        }

        return new CooldownDecision(false, seconds);
    }

    public static float ClampSearchRadius(float radius)
    {
        if (radius < 1f)
        {
            return 1f;
        }

        if (radius > 100f)
        {
            return 100f;
        }

        return radius;
    }
}

public sealed class CooldownLedger
{
    readonly Dictionary<long, DateTimeOffset> _started = new Dictionary<long, DateTimeOffset>();

    public CooldownDecision TryStart(long playerId, DateTimeOffset now, int cooldownMinutes)
    {
        DateTimeOffset? last = null;
        if (_started.TryGetValue(playerId, out var value))
        {
            last = value;
        }

        var decision = LocationResetRules.CheckCooldown(last, now, cooldownMinutes);
        if (decision.Allowed)
        {
            _started[playerId] = now;
        }

        return decision;
    }
}
