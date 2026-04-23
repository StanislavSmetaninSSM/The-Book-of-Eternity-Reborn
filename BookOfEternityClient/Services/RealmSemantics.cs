namespace BookOfEternityClient.Services;

internal static class RealmSemantics
{
    public static bool HasResolvedRealm(string? realm) => !string.IsNullOrWhiteSpace(realm);

    public static bool IsAfterlifeRealm(string? realm) =>
        string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    public static bool IsShiningRealm(string? realm) =>
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    public static bool IsMortalRealm(string? realm) =>
        HasResolvedRealm(realm) && !IsAfterlifeRealm(realm);
}
