using NetworkProfiles.Core.AutoSwitch;

namespace NetworkProfiles.Core.Tests;

public class AutoSwitchTests
{
    private static readonly AutoSwitchRule Game = new() { Program = @"C:\Games\Game.EXE", ProfileId = Guid.NewGuid() };
    private static readonly AutoSwitchRule Vpn = new() { Program = "vpnclient", ProfileId = Guid.NewGuid() };

    [Theory]
    [InlineData("game.exe", "game")]
    [InlineData(@"C:\Program Files\App\app.exe", "app")]
    [InlineData("\"C:/x/Tool.Exe\"", "Tool")]
    [InlineData(" steam ", "steam")]
    [InlineData("my.app", "my.app")]
    public void NormalizeProgram(string input, string expected) =>
        Assert.Equal(expected, AutoSwitchRule.NormalizeProgram(input));

    [Fact]
    public void ProgramStart_Activates_AndExit_Restores()
    {
        var engine = new AutoSwitchEngine();

        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([Game], ["explorer"]).Action);

        var start = engine.Evaluate([Game], ["explorer", "game"]);
        Assert.Equal(AutoSwitchAction.Activate, start.Action);
        Assert.Same(Game, start.Rule);

        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([Game], ["GAME"]).Action);

        var exit = engine.Evaluate([Game], ["explorer"]);
        Assert.Equal(AutoSwitchAction.Restore, exit.Action);
        Assert.Null(engine.ActiveRuleId);
    }

    [Fact]
    public void OnlyOneRuleIsActive_FirstInListWins()
    {
        var engine = new AutoSwitchEngine();

        Assert.Same(Game, engine.Evaluate([Game, Vpn], ["game", "vpnclient"]).Rule);
        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([Game, Vpn], ["game", "vpnclient"]).Action);

        // Game exits: restore first, then the waiting rule takes over on the next check.
        Assert.Equal(AutoSwitchAction.Restore, engine.Evaluate([Game, Vpn], ["vpnclient"]).Action);
        var next = engine.Evaluate([Game, Vpn], ["vpnclient"]);
        Assert.Equal(AutoSwitchAction.Activate, next.Action);
        Assert.Same(Vpn, next.Rule);
    }

    [Fact]
    public void ManualChange_MeansNothingIsRestoredLater()
    {
        var engine = new AutoSwitchEngine();
        engine.Evaluate([Game], ["game"]);
        engine.UserChangedSettings();

        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([Game], ["game"]).Action);
        Assert.Equal(AutoSwitchAction.Release, engine.Evaluate([Game], []).Action);
        Assert.False(engine.Overridden);
    }

    [Fact]
    public void ManualChange_WithoutActiveRule_IsIgnored()
    {
        var engine = new AutoSwitchEngine();
        engine.UserChangedSettings();
        Assert.Equal(AutoSwitchAction.Activate, engine.Evaluate([Game], ["game"]).Action);
        Assert.False(engine.Overridden);
    }

    [Fact]
    public void FailedActivation_DoesNotRetriggerOrRestore()
    {
        var engine = new AutoSwitchEngine();
        engine.Evaluate([Game], ["game"]);
        engine.ActivationFailed(Game.Id);

        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([Game], ["game"]).Action);
        Assert.Equal(AutoSwitchAction.Release, engine.Evaluate([Game], []).Action);
        Assert.Equal(AutoSwitchAction.Activate, engine.Evaluate([Game], ["game"]).Action);
    }

    [Fact]
    public void DisablingOrDeletingActiveRule_Restores()
    {
        var engine = new AutoSwitchEngine();
        var rule = new AutoSwitchRule { Program = "game", ProfileId = Guid.NewGuid() };
        engine.Evaluate([rule], ["game"]);

        rule.Enabled = false;
        Assert.Equal(AutoSwitchAction.Restore, engine.Evaluate([rule], ["game"]).Action);
        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([rule], ["game"]).Action);

        rule.Enabled = true;
        engine.Evaluate([rule], ["game"]);
        var deleted = engine.Evaluate([], ["game"]);
        Assert.Equal(AutoSwitchAction.Restore, deleted.Action);
        Assert.Null(deleted.Rule);
    }

    [Fact]
    public void EmptyProgram_NeverMatches()
    {
        var engine = new AutoSwitchEngine();
        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([new AutoSwitchRule { Program = " " }], [""]).Action);
    }

    [Fact]
    public void Deactivate_RetriesOnNextCheck_AndNeverRestores()
    {
        // Rule's adapter is unplugged: the app deactivates and tries again next time.
        var engine = new AutoSwitchEngine();
        Assert.Equal(AutoSwitchAction.Activate, engine.Evaluate([Game], ["game"]).Action);
        engine.Deactivate(Game.Id);
        Assert.Null(engine.ActiveRuleId);

        Assert.Equal(AutoSwitchAction.Activate, engine.Evaluate([Game], ["game"]).Action);
        engine.Deactivate(Game.Id);

        // Program exits while still waiting: nothing to restore.
        Assert.Equal(AutoSwitchAction.None, engine.Evaluate([Game], []).Action);
    }

    [Fact]
    public void Deactivate_OtherRule_IsIgnored()
    {
        var engine = new AutoSwitchEngine();
        engine.Evaluate([Game], ["game"]);
        engine.Deactivate(Vpn.Id);
        Assert.Equal(Game.Id, engine.ActiveRuleId);
    }
}
