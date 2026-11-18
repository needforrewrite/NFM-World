namespace NFMWorld.Archive;

/// <summary>Small SQL/LINQ helpers shared by the endpoints and the pages.</summary>
public static class Sql
{
    /// <summary>Escapes a LIKE pattern's wildcards so a search box cannot become a wildcard query.</summary>
    public static string LikeEscape(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
