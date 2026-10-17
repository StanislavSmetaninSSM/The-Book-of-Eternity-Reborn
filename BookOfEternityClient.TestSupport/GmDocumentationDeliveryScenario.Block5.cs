namespace BookOfEternityClient.Tests;

internal static partial class GmDocumentationDeliveryScenario
{
    // A memory-fixture inventory stays36; real current publication requires this explicit union.
    internal static readonly Doc[] Block5Expected =
    [
        new("Rules/Block_5.txt", "block5_rule_navigator"),
        new("Examples/E_Block_5.txt", "block5_example_navigator"),
        new("Rules/Block_5_Units_Ids.txt", "block5_rules_units_ids"),
        new("Examples/E_Block_5_Units_Ids.txt", "block5_examples_units_ids"),
        new("Rules/Block_5_Characteristics_Progression.txt", "block5_rules_characteristics_progression"),
        new("Examples/E_Block_5_Characteristics_Progression.txt", "block5_examples_characteristics_progression"),
        new("Rules/Block_5_Combat_Foundations.txt", "block5_rules_combat_foundations"),
        new("Examples/E_Block_5_Combat_Foundations.txt", "block5_examples_combat_foundations"),
        new("Rules/Block_5_Items_Resources.txt", "block5_rules_items_resources"),
        new("Examples/E_Block_5_Items_Resources.txt", "block5_examples_items_resources"),
        new("Rules/Block_5_Narrative_Social.txt", "block5_rules_narrative_social"),
        new("Examples/E_Block_5_Narrative_Social.txt", "block5_examples_narrative_social"),
        new("Rules/Block_5_Wounds.txt", "block5_rules_wounds"),
        new("Examples/E_Block_5_Wounds.txt", "block5_examples_wounds"),
        new("Rules/Block_5_World_Chronology.txt", "block5_rules_world_chronology"),
        new("Examples/E_Block_5_World_Chronology.txt", "block5_examples_world_chronology"),
    ];
    internal static Doc[] CurrentExpected => [.. Expected, .. Block5Expected];
}
