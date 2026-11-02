namespace NetworkProfiles.App.ViewModels;

/// <summary>A value with a display label, for combo boxes bound to enums.</summary>
public sealed record Option<T>(T Value, string Label);
