using System.Collections.Generic;

namespace WindowsStorageCleaner.Models;

public class CleanupProfile
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ProfileLevel Level { get; set; }
    public List<string> EnabledItemIds { get; set; } = new();
}

public enum ProfileLevel
{
    None,
    Safe,
    Standard,
    Thorough,
    Maximum,
    All
}
