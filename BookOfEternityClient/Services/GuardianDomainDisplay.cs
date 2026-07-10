namespace BookOfEternityClient.Services;

public static class GuardianDomainDisplay
{
    public static string ForPlayer(string? domainTag)
    {
        if (string.IsNullOrWhiteSpace(domainTag))
            return "Сфера не указана";

        var trimmed = domainTag.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "combat" => "Боевой домен",
            "magic" => "Магический домен",
            "social" => "Социальный домен",
            "crafting" => "Ремесленный домен",
            "survival" => "Домен выживания",
            "knowledge" => "Домен знания",
            "trade" => "Торговый домен",
            "runes" => "Руны",
            "stardust" => "Звёздная пыль",
            "ward" or "wards" => "Обереги",
            "mirror" or "mirrors" => "Зеркала",
            "focus" => "Средоточие",
            _ => trimmed
        };
    }
}
