namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private static string FormatLocationTypeForPlayer(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "indoor" => "помещение",
            "outdoor" => "открытая местность",
            "city" => "городская локация",
            "gate" => "ворота",
            "market" => "рынок",
            "district" => "квартал",
            "building" => "здание",
            "dungeon" => "подземелье",
            "cave" or "cavesystem" or "cave_system" => "пещерная система",
            "vehicle" => "транспорт",
            "uniqueindoor" or "unique_indoor" => "особое помещение",
            _ => StructuredBonusDisplay.FormatScalar(value)
        };

    private static string FormatLocationBiomeForPlayer(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "urban" => "город",
            "forest" => "лес",
            "mountain" => "горы",
            "swamp" => "болото",
            "desert" => "пустошь",
            "coast" => "побережье",
            _ => StructuredBonusDisplay.FormatScalar(value)
        };
}
