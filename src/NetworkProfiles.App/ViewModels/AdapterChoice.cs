namespace NetworkProfiles.App.ViewModels;

/// <summary>
/// An adapter a rule can point to. Kept separate from the live adapter list,
/// which is rebuilt on every refresh: combo boxes bound to a list that is
/// cleared would reset each rule's choice.
/// </summary>
public sealed record AdapterChoice(string Id, string Name);
