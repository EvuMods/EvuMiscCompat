using System;
using EvuMiscCompat.Core;
using Xunit;

namespace EvuMiscCompat.Core.Tests;

public sealed class LocationResetRulesTests
{
    static readonly DateTimeOffset Start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("resetdungeon")]
    [InlineData("ResetDungeon")]
    [InlineData("/resetdungeon")]
    [InlineData("/ResetDungeon")]
    [InlineData("  /resetdungeon  ")]
    public void Command_AcceptsSlashAndCase(string text)
    {
        Assert.True(LocationResetRules.IsResetDungeonCommand(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("resetdungeons")]
    [InlineData("resetdungeon please")]
    [InlineData("/resetdungeon now")]
    [InlineData("hello")]
    public void Command_RejectsAnythingElse(string text)
    {
        Assert.False(LocationResetRules.IsResetDungeonCommand(text));
    }

    [Theory]
    [InlineData("Always", ConfirmationMode.Always)]
    [InlineData("always", ConfirmationMode.Always)]
    [InlineData("On nearby activity", ConfirmationMode.OnNearbyActivity)]
    [InlineData("onnearbyactivity", ConfirmationMode.OnNearbyActivity)]
    [InlineData("Never", ConfirmationMode.Never)]
    public void Confirmation_ParsesTheThreeModes(string text, ConfirmationMode mode)
    {
        Assert.True(LocationResetRules.TryParseConfirmation(text, out var parsed));
        Assert.Equal(mode, parsed);
    }

    [Fact]
    public void PlayersInArea_RefuseUnderEveryMode()
    {
        Assert.Equal(ResetDecision.Refuse, LocationResetRules.Decide(true, false, ConfirmationMode.Always));
        Assert.Equal(ResetDecision.Refuse, LocationResetRules.Decide(true, true, ConfirmationMode.OnNearbyActivity));
        Assert.Equal(ResetDecision.Refuse, LocationResetRules.Decide(true, true, ConfirmationMode.Never));
    }

    [Fact]
    public void Always_ConfirmsEvenWhenTheAreaIsClear()
    {
        Assert.Equal(ResetDecision.Confirm, LocationResetRules.Decide(false, false, ConfirmationMode.Always));
    }

    [Fact]
    public void OnNearbyActivity_ConfirmsOnlyWhenAHazardIsPresent()
    {
        Assert.Equal(ResetDecision.Allow, LocationResetRules.Decide(false, false, ConfirmationMode.OnNearbyActivity));
        Assert.Equal(ResetDecision.Confirm, LocationResetRules.Decide(false, true, ConfirmationMode.OnNearbyActivity));
    }

    [Fact]
    public void Never_AllowsWithoutADialog()
    {
        Assert.Equal(ResetDecision.Allow, LocationResetRules.Decide(false, false, ConfirmationMode.Never));
        Assert.Equal(ResetDecision.Allow, LocationResetRules.Decide(false, true, ConfirmationMode.Never));
    }

    [Fact]
    public void Cooldown_ReportsTheRemainingSeconds()
    {
        var decision = LocationResetRules.CheckCooldown(Start, Start.AddMinutes(1), 10);

        Assert.False(decision.Allowed);
        Assert.Equal(540, decision.RemainingSeconds);
    }

    [Fact]
    public void Cooldown_AllowsOnceTheWindowHasElapsed()
    {
        var decision = LocationResetRules.CheckCooldown(Start, Start.AddMinutes(10), 10);

        Assert.True(decision.Allowed);
        Assert.Equal(0, decision.RemainingSeconds);
    }

    [Fact]
    public void Cooldown_ZeroDisablesTheLimit()
    {
        var decision = LocationResetRules.CheckCooldown(Start, Start.AddMinutes(1), 0);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Cooldown_FirstResetIsAllowed()
    {
        var decision = LocationResetRules.CheckCooldown(null, Start, 10);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Ledger_RefusedAttemptDoesNotMoveTheTimestamp()
    {
        var ledger = new CooldownLedger();

        Assert.True(ledger.TryStart(7, Start, 10).Allowed);

        var denied = ledger.TryStart(7, Start.AddMinutes(1), 10);
        Assert.False(denied.Allowed);
        Assert.Equal(540, denied.RemainingSeconds);

        Assert.True(ledger.TryStart(7, Start.AddMinutes(10), 10).Allowed);
    }

    [Fact]
    public void Ledger_TracksPlayersSeparately()
    {
        var ledger = new CooldownLedger();

        Assert.True(ledger.TryStart(1, Start, 10).Allowed);
        Assert.True(ledger.TryStart(2, Start.AddMinutes(1), 10).Allowed);
        Assert.False(ledger.TryStart(1, Start.AddMinutes(1), 10).Allowed);
    }

    [Fact]
    public void SearchRadius_ClampsToTheManualResetCap()
    {
        Assert.Equal(1f, LocationResetRules.ClampSearchRadius(0f));
        Assert.Equal(20f, LocationResetRules.ClampSearchRadius(20f));
        Assert.Equal(100f, LocationResetRules.ClampSearchRadius(250f));
    }
}
