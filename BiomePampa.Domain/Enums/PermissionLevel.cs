namespace BiomePampa.Domain.Enums
{
    public enum PermissionLevel
    {
        None = 0,      // Usuário não vê nem sabe que existe
        Read = 1,      // Pode visualizar
        Write = 2,     // Pode visualizar e editar
        Full = 3       // Acesso total (incluindo exclusão, configurações, etc.)
    }
}
