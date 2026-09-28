namespace Prumo.Domain.Enums
{
    public enum PermissionLevel
    {
        None = 0,      // The user does not see it or know it exists
        Read = 1,      // Can view
        Write = 2,     // Can view and edit
        Full = 3       // Full access (including deleting)
    }
}
