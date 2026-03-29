namespace BookOfEternityClient.Services;

internal static class RealmSemantics
{
    public static bool IsAfterlifeRealm(string? realm) =>
        string.Equals(realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(realm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    public static bool IsMortalRealm(string? realm) =>
        !string.IsNullOrWhiteSpace(realm) && !IsAfterlifeRealm(realm);
}
