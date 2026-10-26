namespace PCLCS;

/// <summary>
/// An mcmod.cn (MC百科) entry.
/// <para/>Use <see cref="WikiEntry.All"/> to get all entries.
/// </summary>
public class WikiEntry {

    /// <summary>
    /// The corresponding ID on mcmod.cn.
    /// </summary>
    public int Id;
    /// <summary>
    /// Chinese name.
    /// <c>null</c> means there is no translation.
    /// </summary>
    public string? ChineseName;
    /// <summary>
    /// Slug on each mod platform (e.g. advanced-solar-panels).
    /// A missing key means it is not on that platform.
    /// </summary>
    public Dictionary<ResourcePlatforms, string> Slugs = [];
    /// <summary>
    /// Reverse ranking by mcmod.cn page views; 1 means the fewest views.
    /// </summary>
    public int Popularity;

    /// <summary>
    /// All mcmod.cn entries in the built-in database.
    /// </summary>
    public static readonly Lazy<List<WikiEntry>> All = new(() => {
        var dataLines = FileUtils.ReadAsLines(@"Resource\WikiEntries.txt", type: typeof(WikiEntry)).ToList();

        // Read the page views from the last line
        Queue<int> popularities = [];
        for (int i = 0; i < dataLines[^1].Length; i += 3) { // Split every 3 characters into one element
            popularities.Enqueue(int.Parse(dataLines[^1].Substring(i, 3).ConvertRadix(86, 10)));
        }
        dataLines.RemoveAt(dataLines.Count - 1);

        // Parse each line
        List<WikiEntry> results = [];
        var lineNumber = 0;
        foreach (var lineData in dataLines) {
            lineNumber++;
            if (lineData == "") continue;

            var popularity = popularities.Dequeue();
            foreach (var entryData in lineData.Split('¨')) {
                var entry = new WikiEntry();
                var parts = entryData.Split('|');
                var slugs = parts[0];
                if (slugs.StartsWithF("@")) {
                    entry.Slugs.Add(ResourcePlatforms.Modrinth, slugs.Replace("@", ""));
                } else if (slugs.EndsWithF("@")) {
                    entry.Slugs.Add(ResourcePlatforms.CurseForge, slugs.TrimEnd('@'));
                    entry.Slugs.Add(ResourcePlatforms.Modrinth, entry.Slugs[ResourcePlatforms.CurseForge]);
                } else if (slugs.Contains("@")) {
                    var splitSlugs = slugs.Split('@');
                    entry.Slugs.Add(ResourcePlatforms.CurseForge, splitSlugs[0]);
                    entry.Slugs.Add(ResourcePlatforms.Modrinth, splitSlugs[1]);
                } else {
                    entry.Slugs.Add(ResourcePlatforms.CurseForge, slugs);
                }

                entry.Id = lineNumber;
                entry.Popularity = popularity;
                if (parts.Length >= 2) {
                    entry.ChineseName = parts[^1]; // Last item
                    if (entry.ChineseName.Contains("*")) { // Handle *
                        var englishName = entry.Slugs.Values.First().Replace("-", " ").Capitalize();
                        entry.ChineseName = entry.ChineseName.Replace("*", $" ({englishName})");
                    }
                }
                results.Add(entry);
            }
        }
        return results;
    });

    public override string ToString()
        => $"{Slugs.GetOrDefault(ResourcePlatforms.CurseForge)}&{Slugs.GetOrDefault(ResourcePlatforms.Modrinth)}|{Id}|{ChineseName}，浏览量 {Popularity}";
}