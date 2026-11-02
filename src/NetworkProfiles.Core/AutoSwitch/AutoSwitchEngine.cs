namespace NetworkProfiles.Core.AutoSwitch;

public enum AutoSwitchAction
{
    None,

    /// <summary>Apply <see cref="AutoSwitchDecision.Rule"/>'s profile, remembering the current settings.</summary>
    Activate,

    /// <summary>The active rule's program has exited: restore the remembered settings.</summary>
    Restore,

    /// <summary>The active rule's program has exited, but the user changed settings meanwhile: just forget it.</summary>
    Release,
}

public sealed record AutoSwitchDecision(AutoSwitchAction Action, AutoSwitchRule? Rule)
{
    public static readonly AutoSwitchDecision Nothing = new(AutoSwitchAction.None, null);
}

/// <summary>
/// Decides when to switch profiles based on which programs are running.
/// Only one rule is active at a time: the first enabled rule, in list order,
/// whose program is running. Later rules wait until it ends.
/// Pure logic; the caller supplies the running process names and carries out
/// the decision.
/// </summary>
public sealed class AutoSwitchEngine
{
    /// <summary>The rule whose profile is currently applied, if any.</summary>
    public Guid? ActiveRuleId { get; private set; }

    /// <summary>True when the user applied something by hand while a rule was active.</summary>
    public bool Overridden { get; private set; }

    /// <param name="runningPrograms">Process names (as <c>Process.ProcessName</c> reports them), compared ignoring case.</param>
    public AutoSwitchDecision Evaluate(IEnumerable<AutoSwitchRule> rules, IEnumerable<string> runningPrograms)
    {
        var ruleList = rules.ToList();
        var running = new HashSet<string>(runningPrograms, StringComparer.OrdinalIgnoreCase);

        if (ActiveRuleId is { } activeId)
        {
            var active = ruleList.FirstOrDefault(r => r.Id == activeId);
            if (active is not null && active.Enabled && IsRunning(active, running))
                return AutoSwitchDecision.Nothing;

            // Program exited, or the rule was disabled or deleted.
            var action = Overridden ? AutoSwitchAction.Release : AutoSwitchAction.Restore;
            ActiveRuleId = null;
            Overridden = false;
            return new AutoSwitchDecision(action, active);
        }

        var next = ruleList.FirstOrDefault(r => r.Enabled && IsRunning(r, running));
        if (next is null)
            return AutoSwitchDecision.Nothing;

        ActiveRuleId = next.Id;
        return new AutoSwitchDecision(AutoSwitchAction.Activate, next);
    }

    /// <summary>
    /// Call when applying a rule's profile failed. The rule stays marked active
    /// so it does not retrigger every tick, but nothing is restored when its
    /// program exits. It is tried again the next time the program starts.
    /// </summary>
    public void ActivationFailed(Guid ruleId)
    {
        if (ActiveRuleId == ruleId)
            Overridden = true;
    }

    /// <summary>
    /// Call when a rule could not be applied yet (for example its adapter is
    /// unplugged). Nothing counts as active, so the rule is tried again on the
    /// next check and applies as soon as it can.
    /// </summary>
    public void Deactivate(Guid ruleId)
    {
        if (ActiveRuleId == ruleId)
        {
            ActiveRuleId = null;
            Overridden = false;
        }
    }

    /// <summary>The user applied or reverted settings by hand; don't undo that later.</summary>
    public void UserChangedSettings()
    {
        if (ActiveRuleId is not null)
            Overridden = true;
    }

    private static bool IsRunning(AutoSwitchRule rule, HashSet<string> runningPrograms)
    {
        var name = AutoSwitchRule.NormalizeProgram(rule.Program);
        return name.Length > 0 && runningPrograms.Contains(name);
    }
}
