using System.Text.Json;
using System.Text.RegularExpressions;

namespace JobPlatform.AccountIdentity.ArchitectureTests;

/// <summary>
/// Story-to-test traceability gate (foundation section 14.3). Every acceptance criterion of the 13 stories owned by BC-03 needs at least
/// one test tagged [Trait("Story","US-...")] and [Trait("AC","AC-nn")] on the same test method. The same rule is available as
/// scripts/check-ac-coverage.ps1 for CI.
/// </summary>
public class TraceabilityTests
{
    private static readonly string[] OwnedStories =
    {
        "US-3.1.1-01", "US-3.1.1-02", "US-3.1.2-01", "US-3.1.2-02", "US-3.1.3-01", "US-3.1.3-02", "US-3.1.4-03",
        "US-3.1.5-01", "US-3.1.5-02", "US-3.1.5-03", "US-3.1.5-04", "US-3.4.3-04", "US-4.1-04"
    };

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JobPlatform.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("JobPlatform.sln not found");
    }

    private static Dictionary<string, string[]> Snapshot()
    {
        var json = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "owned-story-acs.json"));
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(json)!;
    }

    private static string? StoriesDirectory()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("STORIES_DIR");
        var candidate = fromEnvironment ?? Path.GetFullPath(Path.Combine(RepoRoot(), "..", "Pipeline-Output", "stories"));
        return Directory.Exists(candidate) ? candidate : null;
    }

    /// <summary>(story, ac) pairs found on the same test method: consecutive attribute lines above a public test method.</summary>
    private static HashSet<(string Story, string Ac)> TaggedTests()
    {
        var pairs = new HashSet<(string, string)>();
        // Scoped to this BC's own test tree: other bounded contexts tag their own tests with their own (unrelated) story/AC
        // codes, which is not this test's business - scanning the whole repo's tests/ would flag those as "unknown" too.
        foreach (var file in Directory.EnumerateFiles(Path.Combine(RepoRoot(), "tests", "AccountIdentity"), "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var stories = new List<string>();
            var acs = new List<string>();
            foreach (var line in File.ReadLines(file))
            {
                var trimmed = line.Trim();
                var story = Regex.Match(trimmed, @"^\[Trait\(""Story"",\s*""([^""]+)""\)\]");
                var ac = Regex.Match(trimmed, @"^\[Trait\(""AC"",\s*""([^""]+)""\)\]");
                if (story.Success)
                {
                    stories.Add(story.Groups[1].Value);
                }
                else if (ac.Success)
                {
                    acs.Add(ac.Groups[1].Value);
                }
                else if (trimmed.StartsWith('['))
                {
                    continue;
                }
                else if (Regex.IsMatch(trimmed, @"^public\s+(async\s+)?(Task|void)\s+\w+\("))
                {
                    foreach (var s in stories)
                    {
                        foreach (var a in acs)
                        {
                            pairs.Add((s, a));
                        }
                    }

                    stories.Clear();
                    acs.Clear();
                }
                else if (trimmed.Length > 0)
                {
                    stories.Clear();
                    acs.Clear();
                }
            }
        }

        return pairs;
    }

    [Fact]
    public void EveryAcceptanceCriterionOfTheThirteenOwnedStories_HasAtLeastOneTaggedTest()
    {
        var required = Snapshot();
        required.Keys.Should().BeEquivalentTo(OwnedStories);
        var tagged = TaggedTests();

        var missing = OwnedStories.SelectMany(s => required[s].Select(ac => (Story: s, Ac: ac))).Where(x => !tagged.Contains(x)).Select(x => $"{x.Story} {x.Ac}").ToList();

        required.Values.Sum(v => v.Length).Should().Be(58, "the 13 stories define 58 acceptance criteria");
        missing.Should().BeEmpty("every AC needs a test tagged [Trait(\"Story\", ...)] + [Trait(\"AC\", ...)]; missing: " + string.Join(", ", missing));
    }

    [Fact]
    public void NoTestIsTaggedWithAnAcceptanceCriterionThatDoesNotExist()
    {
        var required = Snapshot();

        var unknown = TaggedTests().Where(t => OwnedStories.Contains(t.Story) && !required[t.Story].Contains(t.Ac)).ToList();

        unknown.Should().BeEmpty("a typo in a tag silently removes coverage");
        TaggedTests().Where(t => !OwnedStories.Contains(t.Story)).Should().BeEmpty("only the 13 owned stories are tagged in this BC");
    }

    [Fact]
    public void TheSnapshotMatchesTheStoryFiles_WhenTheyAreAvailable()
    {
        var directory = StoriesDirectory();
        if (directory is null)
        {
            // The stories live outside this repository; the checked-in snapshot is authoritative when they are absent.
            return;
        }

        var snapshot = Snapshot();
        foreach (var story in OwnedStories)
        {
            var file = Directory.EnumerateFiles(directory, $"{story}-*.md").Single();
            var acs = Regex.Matches(File.ReadAllText(file), @"^\*\*(AC-\d+)", RegexOptions.Multiline).Select(m => m.Groups[1].Value).Distinct().OrderBy(a => a).ToArray();

            snapshot[story].Should().Equal(acs, $"scripts/owned-story-acs.json is stale for {story}; regenerate it from the story file");
        }
    }
}
