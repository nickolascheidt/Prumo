namespace Prumo.Tests.Architecture
{
    /// <summary>
    /// A fase 2 removeu 68 IgnoreQueryFilters que tinham virado redundantes ou que nunca
    /// fizeram nada. O que sobra é legítimo e está documentado. Este teste existe para que
    /// o próximo não entre sem justificativa — sem ele, a limpeza se desfaz sozinha.
    ///
    /// A justificativa tem que dizer *por que* a leitura é cross-tenant, não só que é.
    /// Os dois motivos que sobreviveram à fase 2: código de startup que roda sem
    /// TenantContext (DbInitializer, seeders) e emissão de token, que acontece antes de o
    /// claim tenant_id existir (TenantRoleService, PermissionService).
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

                    // Justificativa = comentário nas 6 linhas acima contendo "cross-tenant".
                    // A comparação ignora maiúsculas: o comentário costuma abrir a frase
                    // com "Cross-tenant de propósito:", e uma checagem sensível a caixa
                    // passaria a aceitar/rejeitar por detalhe de redação, não por conteúdo.
                    var start = Math.Max(0, i - 6);
                    var justified = lines[start..i].Any(l =>
                        l.TrimStart().StartsWith("//")
                        && l.Contains("cross-tenant", StringComparison.OrdinalIgnoreCase));

                    if (!justified)
                        offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}");
                }
            }

            Assert.True(offenders.Count == 0,
                "IgnoreQueryFilters sem justificativa. Ou o filtro global já cobre este caso "
                + "e a chamada deve sair, ou a leitura é cross-tenant de propósito e precisa "
                + "de um comentário acima contendo 'cross-tenant' explicando por quê.\n  "
                + string.Join("\n  ", offenders));
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Prumo.slnx")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Prumo.slnx não encontrado.");
        }
    }
}
