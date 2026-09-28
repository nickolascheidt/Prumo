namespace Prumo.Tests.Architecture
{
    /// <summary>
    /// A cleanup pass removed 68 IgnoreQueryFilters calls that had become redundant or had
    /// never done anything. What is left is legitimate and documented. This test exists so
    /// the next one does not get in without a justification — without it, the cleanup
    /// undoes itself.
    ///
    /// The justification has to say *why* the read is cross-tenant, not just that it is.
    /// The two reasons that survived: startup code that runs without a TenantContext
    /// (DbInitializer, seeders) and token issuing, which happens before the tenant_id claim
    /// exists (TenantRoleService).
    /// </summary>
    public class DataAccessHygieneTests
    {
        [Fact]
        public void Every_IgnoreQueryFilters_call_is_justified_by_a_comment()
        {
            var root = FindRepoRoot();
            var offenders = new List<string>();

            var files = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                         && !f.Contains("Prumo.Tests"));

            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].Contains("IgnoreQueryFilters")) continue;

                    // Justification = a comment in the 6 lines above containing "cross-tenant".
                    // The comparison ignores case: the comment usually opens the sentence with
                    // "Cross-tenant on purpose:", and a case-sensitive check would accept or
                    // reject on wording, not on content.
                    var start = Math.Max(0, i - 6);
                    var justified = lines[start..i].Any(l =>
                        l.TrimStart().StartsWith("//")
                        && l.Contains("cross-tenant", StringComparison.OrdinalIgnoreCase));

                    if (!justified)
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }

            Assert.True(offenders.Count == 0,
                "IgnoreQueryFilters without a justification. Either the global filter already covers "
                + "this case and the call should go, or the read is cross-tenant on purpose and needs "
                + "a comment above containing 'cross-tenant' explaining why.\n  "
                + string.Join("\n  ", offenders));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Prumo.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Prumo.slnx not found.");
        }
    }
}
