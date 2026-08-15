using System.Buffers.Binary;
using System.Collections.Frozen;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace BookOfEternityClient.Services;

internal sealed record ResourceFormulaOwner(
    string Realm,
    ResourceOwnerKind OwnerKind,
    string ResourceOwnerId);

internal abstract record ResourceFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint);

internal sealed record PlayerHealthCapacityFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    int PermanentStrength,
    int PermanentConstitution)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal sealed record PlayerEnergyCapacityFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    int PermanentConstitution,
    int PermanentIntelligence,
    int PermanentWisdom,
    int PermanentFaith)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal sealed record PlayerPoiseCapacityFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    int PermanentStrength,
    int PermanentConstitution,
    int PermanentIntelligence,
    int PermanentWisdom)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal sealed record MaterializedOwnerCapacityFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    decimal AcceptedMaximum)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal sealed record SpiritFocusActionPointsFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    int SpiritFocusTier)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal sealed record ConflictSideActionPointsFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    string ConflictId,
    decimal AcceptedMaximum)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal sealed record GuardianReturnGachaFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    int Reputation,
    int AbodePower,
    int FounderExtraCharges,
    string ReturnCycleId)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal sealed record ShiningReturnGachaFormulaInput(
    ResourceFormulaOwner Owner,
    string OwnerAuthorityFingerprint,
    int RadianceTier,
    string ReturnCycleId)
    : ResourceFormulaInput(Owner, OwnerAuthorityFingerprint);

internal abstract record ResourceCapacityInput(ResourceFormulaOwner Owner);

internal sealed record DefinitionFixedCapacityInput(ResourceFormulaOwner Owner)
    : ResourceCapacityInput(Owner);

internal sealed record InstanceFixedCapacityInput(
    ResourceFormulaOwner Owner,
    decimal Maximum,
    string CapacityAuthorityFingerprint)
    : ResourceCapacityInput(Owner);

internal sealed record RegisteredFormulaCapacityInput(ResourceFormulaInput FormulaInput)
    : ResourceCapacityInput(FormulaInput.Owner);

internal abstract record ResourceInitializationInput(
    ResourceFormulaOwner Owner,
    decimal ResolvedMaximum,
    string CapacityAuthorityFingerprint);

internal sealed record SealedPolicyResourceInitializationInput(
    ResourceFormulaOwner Owner,
    decimal ResolvedMaximum,
    string CapacityAuthorityFingerprint)
    : ResourceInitializationInput(Owner, ResolvedMaximum, CapacityAuthorityFingerprint);

internal sealed record RegisteredFormulaResourceInitializationInput(
    ResourceFormulaInput FormulaInput,
    decimal ResolvedMaximum,
    string CapacityAuthorityFingerprint)
    : ResourceInitializationInput(
        FormulaInput.Owner,
        ResolvedMaximum,
        CapacityAuthorityFingerprint);

internal sealed record ResourceFormulaResult(
    decimal? Value,
    string? AuthorityFingerprint,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Value.HasValue &&
                             ResourceMaterializationContract.IsAuthorityFingerprint(
                                 AuthorityFingerprint) &&
                             Issues.Count == 0;
}

internal interface IResourceCapacityFormula
{
    string FormulaKey { get; }

    IReadOnlySet<ResourceOwnerKind> SupportedOwnerKinds { get; }

    ResourceFormulaResult Evaluate(ResourceFormulaInput input);
}

internal static class ResourceCapacityFormulaCatalog
{
    internal const string MortalHealthCapacityV1 = "mortal_health_capacity_v1";
    internal const string MortalEnergyCapacityV1 = "mortal_energy_capacity_v1";
    internal const string MortalPoiseCapacityV1 = "mortal_poise_capacity_v1";
    internal const string AfterlifeSpiritualActionPointsV1 =
        "afterlife_spiritual_action_points_v1";
    internal const string AfterlifeReturnGachaAttemptsV1 =
        "afterlife_return_gacha_attempts_v1";

    private static readonly FrozenSet<string> Realms =
        new[] { "mortal_world", "chaos_sea", "shining_abode" }
            .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, IResourceCapacityFormula> Formulas =
        new IResourceCapacityFormula[]
        {
            new MortalHealthCapacityFormula(),
            new MortalEnergyCapacityFormula(),
            new MortalPoiseCapacityFormula(),
            new AfterlifeSpiritualActionPointsFormula(),
            new AfterlifeReturnGachaAttemptsFormula()
        }.ToFrozenDictionary(
            static formula => formula.FormulaKey,
            StringComparer.Ordinal);

    internal static IReadOnlySet<string> FormulaKeys { get; } =
        Formulas.Keys.ToFrozenSet(StringComparer.Ordinal);

    internal static bool IsRegisteredFormulaKey(string formulaKey) =>
        Formulas.ContainsKey(formulaKey);

    internal static bool SupportsEveryOwnerKind(
        string formulaKey,
        IEnumerable<ResourceOwnerKind> ownerKinds) =>
        Formulas.TryGetValue(formulaKey, out var formula) &&
        ownerKinds.All(formula.SupportedOwnerKinds.Contains);

    internal static ResourceFormulaResult Evaluate(
        string formulaKey,
        ResourceFormulaInput input,
        string? expectedAuthorityFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!ResourceMaterializationContract.IsExactIdentifier(formulaKey) ||
            !Formulas.TryGetValue(formulaKey, out var formula))
        {
            return Failure(
                "resourceCapacity.formulaKey",
                "resource_capacity_formula_unknown",
                "registered exact client capacity formula key",
                formulaKey ?? "null");
        }

        var inputIssues = ValidateFormulaInputAuthority(input);
        if (inputIssues.Count != 0)
            return new ResourceFormulaResult(null, null, inputIssues);

        if (!formula.SupportedOwnerKinds.Contains(input.Owner.OwnerKind))
        {
            return Failure(
                "resourceCapacity.owner.ownerKind",
                "resource_capacity_formula_input_invalid",
                "owner kind supported by the selected formula",
                ResourceDefinitionCatalog.GetOwnerKindToken(input.Owner.OwnerKind));
        }

        var result = formula.Evaluate(input);
        if (!result.IsValid)
            return result;

        return RejectStale(
            result,
            expectedAuthorityFingerprint,
            "resource_capacity_formula_stale_input",
            "resourceCapacity.authorityFingerprint");
    }

    internal static ResourceFormulaResult ResolveCapacity(
        ResourceDefinition definition,
        ResourceCapacityInput input,
        string? expectedAuthorityFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(input);
        var ownerIssues = ValidateOwner(input.Owner);
        if (ownerIssues.Count != 0)
            return new ResourceFormulaResult(null, null, ownerIssues);
        if (!definition.AllowedOwnerKinds.Contains(input.Owner.OwnerKind))
        {
            return Failure(
                "resourceCapacity.owner.ownerKind",
                "resource_capacity_owner_kind_forbidden",
                "owner kind allowed by the sealed resource definition",
                ResourceDefinitionCatalog.GetOwnerKindToken(input.Owner.OwnerKind));
        }

        ResourceFormulaResult result;
        switch (definition.CapacityPolicy.Kind)
        {
            case ResourceCapacityKind.DefinitionFixed
                when input is DefinitionFixedCapacityInput:
                if (!definition.CapacityPolicy.Value.HasValue)
                {
                    return Failure(
                        "resourceCapacity",
                        "resource_capacity_policy_invalid",
                        "sealed definition-fixed maximum",
                        "missing");
                }
                result = Success(
                    definition.CapacityPolicy.Value.Value,
                    Fingerprint(
                        "capacity",
                        "definition_fixed",
                        DescribeDefinition(definition),
                        DescribeOwner(input.Owner),
                        Format(definition.CapacityPolicy.Value.Value)));
                break;

            case ResourceCapacityKind.InstanceFixed
                when input is InstanceFixedCapacityInput instance:
                if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                        instance.CapacityAuthorityFingerprint))
                {
                    return Failure(
                        "resourceCapacity.capacityAuthorityFingerprint",
                        "resource_capacity_invalid_authority_fingerprint",
                        "exact sha256 capacity authority fingerprint",
                        instance.CapacityAuthorityFingerprint ?? "null");
                }
                result = Success(
                    instance.Maximum,
                    Fingerprint(
                        "capacity",
                        "instance_fixed",
                        DescribeDefinition(definition),
                        DescribeOwner(input.Owner),
                        instance.CapacityAuthorityFingerprint,
                        Format(instance.Maximum)));
                break;

            case ResourceCapacityKind.RegisteredFormula
                when input is RegisteredFormulaCapacityInput registered &&
                     definition.CapacityPolicy.FormulaKey != null:
                var formulaResult = Evaluate(
                    definition.CapacityPolicy.FormulaKey,
                    registered.FormulaInput);
                if (!formulaResult.IsValid)
                    return formulaResult;
                result = Success(
                    formulaResult.Value!.Value,
                    Fingerprint(
                        "capacity",
                        "registered_formula",
                        DescribeDefinition(definition),
                        DescribeOwner(input.Owner),
                        definition.CapacityPolicy.FormulaKey,
                        formulaResult.AuthorityFingerprint!,
                        Format(formulaResult.Value.Value)));
                break;

            default:
                return Failure(
                    "resourceCapacity",
                    "resource_capacity_policy_input_mismatch",
                    $"typed {definition.CapacityPolicy.Kind} capacity input",
                    input.GetType().Name);
        }

        var value = result.Value!.Value;
        var minimum = definition.MinimumPolicy.Value;
        var requiresPositiveMaximum =
            definition.CapacityPolicy.Kind != ResourceCapacityKind.RegisteredFormula;
        var invalid = requiresPositiveMaximum
            ? value <= minimum
            : value < minimum;
        invalid |= !IsDefinitionNumber(definition, value) ||
                   !ResourceMaterializationContract.IsQuantumAligned(
                       value,
                       minimum,
                       definition.Quantum);
        if (invalid)
        {
            return Failure(
                "resourceCapacity.maximum",
                definition.CapacityPolicy.Kind == ResourceCapacityKind.InstanceFixed
                    ? "resource_capacity_invalid_instance_maximum"
                    : "resource_capacity_invalid_resolved_maximum",
                "exact capacity compatible with minimum, numeric kind, and quantum",
                Format(value));
        }

        return RejectStale(
            result,
            expectedAuthorityFingerprint,
            "resource_capacity_stale_input",
            "resourceCapacity.authorityFingerprint");
    }

    internal static ResourceFormulaResult ResolveInitialValue(
        ResourceDefinition definition,
        ResourceInitializationInput input,
        string? expectedAuthorityFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(input);
        var ownerIssues = ValidateOwner(input.Owner);
        if (ownerIssues.Count != 0)
            return new ResourceFormulaResult(null, null, ownerIssues);
        if (!definition.AllowedOwnerKinds.Contains(input.Owner.OwnerKind))
        {
            return Failure(
                "resourceInitialization.owner.ownerKind",
                "resource_initialization_owner_kind_forbidden",
                "owner kind allowed by the sealed resource definition",
                ResourceDefinitionCatalog.GetOwnerKindToken(input.Owner.OwnerKind));
        }
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                input.CapacityAuthorityFingerprint))
        {
            return Failure(
                "resourceInitialization.capacityAuthorityFingerprint",
                "resource_initialization_invalid_capacity_fingerprint",
                "exact sha256 resolved-capacity fingerprint",
                input.CapacityAuthorityFingerprint ?? "null");
        }

        var minimum = definition.MinimumPolicy.Value;
        if (input.ResolvedMaximum < minimum ||
            !IsDefinitionNumber(definition, input.ResolvedMaximum) ||
            !ResourceMaterializationContract.IsQuantumAligned(
                input.ResolvedMaximum,
                minimum,
                definition.Quantum))
        {
            return Failure(
                "resourceInitialization.resolvedMaximum",
                "resource_initialization_invalid_capacity",
                "previously resolved exact maximum compatible with the definition",
                Format(input.ResolvedMaximum));
        }

        decimal value;
        string policyAuthority;
        switch (definition.InitializationPolicy.Kind)
        {
            case ResourceInitializationKind.Minimum
                when input is SealedPolicyResourceInitializationInput:
                value = minimum;
                policyAuthority = "minimum";
                break;
            case ResourceInitializationKind.Maximum
                when input is SealedPolicyResourceInitializationInput:
                value = input.ResolvedMaximum;
                policyAuthority = "maximum";
                break;
            case ResourceInitializationKind.Fixed
                when input is SealedPolicyResourceInitializationInput &&
                     definition.InitializationPolicy.Value.HasValue:
                value = definition.InitializationPolicy.Value.Value;
                policyAuthority = "fixed:" + Format(value);
                break;
            case ResourceInitializationKind.RegisteredFormula
                when input is RegisteredFormulaResourceInitializationInput registered &&
                     definition.InitializationPolicy.FormulaKey != null:
                var formulaResult = Evaluate(
                    definition.InitializationPolicy.FormulaKey,
                    registered.FormulaInput);
                if (!formulaResult.IsValid)
                    return formulaResult;
                value = formulaResult.Value!.Value;
                policyAuthority =
                    "registered_formula:" +
                    definition.InitializationPolicy.FormulaKey + ":" +
                    formulaResult.AuthorityFingerprint;
                break;
            default:
                return Failure(
                    "resourceInitialization",
                    "resource_initialization_policy_input_mismatch",
                    $"typed {definition.InitializationPolicy.Kind} initialization input",
                    input.GetType().Name);
        }

        if (value < minimum ||
            value > input.ResolvedMaximum ||
            !IsDefinitionNumber(definition, value) ||
            !ResourceMaterializationContract.IsQuantumAligned(
                value,
                minimum,
                definition.Quantum))
        {
            return Failure(
                "resourceInitialization.value",
                "resource_initialization_invalid_resolved_value",
                "exact initial value within the resolved bounds and quantum",
                Format(value));
        }

        var result = Success(
            value,
            Fingerprint(
                "initialization",
                DescribeDefinition(definition),
                DescribeOwner(input.Owner),
                input.CapacityAuthorityFingerprint,
                Format(input.ResolvedMaximum),
                policyAuthority,
                Format(value)));
        return RejectStale(
            result,
            expectedAuthorityFingerprint,
            "resource_initialization_stale_input",
            "resourceInitialization.authorityFingerprint");
    }

    private static IReadOnlyList<ValidationIssue> ValidateFormulaInputAuthority(
        ResourceFormulaInput input)
    {
        var issues = ValidateOwner(input.Owner).ToList();
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                input.OwnerAuthorityFingerprint))
        {
            Add(
                issues,
                "resourceCapacity.ownerAuthorityFingerprint",
                "resource_capacity_formula_input_invalid",
                "exact sha256 owner-authority fingerprint",
                input.OwnerAuthorityFingerprint ?? "null");
        }
        return issues;
    }

    private static IReadOnlyList<ValidationIssue> ValidateOwner(ResourceFormulaOwner owner)
    {
        var issues = new List<ValidationIssue>();
        if (owner == null)
        {
            Add(
                issues,
                "resourceCapacity.owner",
                "resource_capacity_formula_input_invalid",
                "validated resource owner",
                "null");
            return issues;
        }
        if (owner.Realm == null || !Realms.Contains(owner.Realm))
        {
            Add(
                issues,
                "resourceCapacity.owner.realm",
                "resource_capacity_formula_input_invalid",
                "mortal_world | chaos_sea | shining_abode",
                owner.Realm ?? "null");
        }
        if (!Enum.IsDefined(typeof(ResourceOwnerKind), owner.OwnerKind))
        {
            Add(
                issues,
                "resourceCapacity.owner.ownerKind",
                "resource_capacity_formula_input_invalid",
                "registered resource owner kind",
                ((int)owner.OwnerKind).ToString(CultureInfo.InvariantCulture));
        }
        if (!ResourceMaterializationContract.IsExactIdentifier(owner.ResourceOwnerId))
        {
            Add(
                issues,
                "resourceCapacity.owner.resourceOwnerId",
                "resource_capacity_formula_input_invalid",
                "exact accepted resource owner identity",
                owner.ResourceOwnerId ?? "null");
        }
        return issues;
    }

    private abstract class MortalCoreCapacityFormula : IResourceCapacityFormula
    {
        protected MortalCoreCapacityFormula(
            string formulaKey,
            params ResourceOwnerKind[] supportedOwnerKinds)
        {
            FormulaKey = formulaKey;
            SupportedOwnerKinds = supportedOwnerKinds.ToFrozenSet();
        }

        public string FormulaKey { get; }

        public IReadOnlySet<ResourceOwnerKind> SupportedOwnerKinds { get; }

        public abstract ResourceFormulaResult Evaluate(ResourceFormulaInput input);

        protected ResourceFormulaResult ResolveMaterializedMaximum(
            MaterializedOwnerCapacityFormulaInput input,
            params ResourceOwnerKind[] supportedMaterializedKinds)
        {
            if (!string.Equals(input.Owner.Realm, "mortal_world", StringComparison.Ordinal) ||
                !supportedMaterializedKinds.Contains(input.Owner.OwnerKind) ||
                input.AcceptedMaximum <= 0m ||
                !ResourceMaterializationContract.IsIntegral(input.AcceptedMaximum))
            {
                return Invalid(input);
            }

            return Success(
                input.AcceptedMaximum,
                Fingerprint(
                    FormulaKey,
                    DescribeOwner(input.Owner),
                    input.OwnerAuthorityFingerprint,
                    "materialized_maximum",
                    Format(input.AcceptedMaximum)));
        }

        protected ResourceFormulaResult SuccessPlayer(
            ResourceFormulaInput input,
            decimal value,
            params string[] scalarInputs)
        {
            if (!IsCurrentMortalPlayer(input.Owner) || value <= 0m)
                return Invalid(input);

            return Success(
                value,
                Fingerprint(
                    new[]
                    {
                        FormulaKey,
                        DescribeOwner(input.Owner),
                        input.OwnerAuthorityFingerprint,
                        "derived_player"
                    }.Concat(scalarInputs).ToArray()));
        }

        protected ResourceFormulaResult Invalid(ResourceFormulaInput input) =>
            Failure(
                "resourceCapacity.formulaInput",
                "resource_capacity_formula_input_invalid",
                "validated typed Mortal owner capacity input",
                input.GetType().Name);
    }

    private sealed class MortalHealthCapacityFormula : MortalCoreCapacityFormula
    {
        internal MortalHealthCapacityFormula()
            : base(
                MortalHealthCapacityV1,
                ResourceOwnerKind.Player,
                ResourceOwnerKind.Npc,
                ResourceOwnerKind.Combatant,
                ResourceOwnerKind.CombatGroupMember,
                ResourceOwnerKind.Vehicle)
        {
        }

        public override ResourceFormulaResult Evaluate(ResourceFormulaInput input) => input switch
        {
            PlayerHealthCapacityFormulaInput player => SuccessPlayer(
                player,
                100m + player.PermanentConstitution * 2m + player.PermanentStrength,
                player.PermanentStrength.ToString(CultureInfo.InvariantCulture),
                player.PermanentConstitution.ToString(CultureInfo.InvariantCulture)),
            MaterializedOwnerCapacityFormulaInput materialized => ResolveMaterializedMaximum(
                materialized,
                ResourceOwnerKind.Npc,
                ResourceOwnerKind.Combatant,
                ResourceOwnerKind.CombatGroupMember,
                ResourceOwnerKind.Vehicle),
            _ => Invalid(input)
        };
    }

    private sealed class MortalEnergyCapacityFormula : MortalCoreCapacityFormula
    {
        internal MortalEnergyCapacityFormula()
            : base(
                MortalEnergyCapacityV1,
                ResourceOwnerKind.Player,
                ResourceOwnerKind.Npc,
                ResourceOwnerKind.Combatant)
        {
        }

        public override ResourceFormulaResult Evaluate(ResourceFormulaInput input) => input switch
        {
            PlayerEnergyCapacityFormulaInput player => SuccessPlayer(
                player,
                100m +
                TruncateThreeQuarters(player.PermanentConstitution) +
                TruncateThreeQuarters(player.PermanentIntelligence) +
                TruncateThreeQuarters(player.PermanentWisdom) +
                TruncateThreeQuarters(player.PermanentFaith),
                player.PermanentConstitution.ToString(CultureInfo.InvariantCulture),
                player.PermanentIntelligence.ToString(CultureInfo.InvariantCulture),
                player.PermanentWisdom.ToString(CultureInfo.InvariantCulture),
                player.PermanentFaith.ToString(CultureInfo.InvariantCulture)),
            MaterializedOwnerCapacityFormulaInput materialized => ResolveMaterializedMaximum(
                materialized,
                ResourceOwnerKind.Npc,
                ResourceOwnerKind.Combatant),
            _ => Invalid(input)
        };
    }

    private sealed class MortalPoiseCapacityFormula : MortalCoreCapacityFormula
    {
        internal MortalPoiseCapacityFormula()
            : base(
                MortalPoiseCapacityV1,
                ResourceOwnerKind.Player,
                ResourceOwnerKind.Combatant,
                ResourceOwnerKind.CombatGroupMember)
        {
        }

        public override ResourceFormulaResult Evaluate(ResourceFormulaInput input) => input switch
        {
            PlayerPoiseCapacityFormulaInput player => SuccessPlayer(
                player,
                100m +
                TruncateThreeHalves(player.PermanentStrength) +
                TruncateThreeHalves(player.PermanentConstitution) +
                TruncateThreeHalves(player.PermanentIntelligence) +
                TruncateThreeHalves(player.PermanentWisdom),
                player.PermanentStrength.ToString(CultureInfo.InvariantCulture),
                player.PermanentConstitution.ToString(CultureInfo.InvariantCulture),
                player.PermanentIntelligence.ToString(CultureInfo.InvariantCulture),
                player.PermanentWisdom.ToString(CultureInfo.InvariantCulture)),
            MaterializedOwnerCapacityFormulaInput materialized => ResolveMaterializedMaximum(
                materialized,
                ResourceOwnerKind.Combatant,
                ResourceOwnerKind.CombatGroupMember),
            _ => Invalid(input)
        };
    }

    private sealed class AfterlifeSpiritualActionPointsFormula : IResourceCapacityFormula
    {
        public string FormulaKey => AfterlifeSpiritualActionPointsV1;

        public IReadOnlySet<ResourceOwnerKind> SupportedOwnerKinds { get; } =
            new[]
            {
                ResourceOwnerKind.AfterlifeActor,
                ResourceOwnerKind.AfterlifeConflictSide
            }.ToFrozenSet();

        public ResourceFormulaResult Evaluate(ResourceFormulaInput input)
        {
            switch (input)
            {
                case SpiritFocusActionPointsFormulaInput spiritFocus
                    when spiritFocus.Owner.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                         !string.Equals(
                             spiritFocus.Owner.Realm,
                             "mortal_world",
                             StringComparison.Ordinal) &&
                         spiritFocus.SpiritFocusTier is >= 0 and <= 5:
                    return Success(
                        AfterlifeSpiritualConflictState.GetSpiritFocusMaxActionPoints(
                            spiritFocus.SpiritFocusTier),
                        Fingerprint(
                            FormulaKey,
                            DescribeOwner(spiritFocus.Owner),
                            spiritFocus.OwnerAuthorityFingerprint,
                            "spirit_focus",
                            spiritFocus.SpiritFocusTier.ToString(CultureInfo.InvariantCulture)));

                case ConflictSideActionPointsFormulaInput conflict
                    when conflict.Owner.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide &&
                         !string.Equals(
                             conflict.Owner.Realm,
                             "mortal_world",
                             StringComparison.Ordinal) &&
                         ResourceMaterializationContract.IsExactIdentifier(conflict.ConflictId) &&
                         conflict.AcceptedMaximum > 0m &&
                         ResourceMaterializationContract.IsIntegral(conflict.AcceptedMaximum):
                    return Success(
                        conflict.AcceptedMaximum,
                        Fingerprint(
                            FormulaKey,
                            DescribeOwner(conflict.Owner),
                            conflict.OwnerAuthorityFingerprint,
                            "conflict_side",
                            conflict.ConflictId,
                            Format(conflict.AcceptedMaximum)));

                default:
                    return Failure(
                        "resourceCapacity.formulaInput",
                        "resource_capacity_formula_input_invalid",
                        "validated afterlife actor spirit-focus input or conflict-side capacity input",
                        input.GetType().Name);
            }
        }
    }

    private sealed class AfterlifeReturnGachaAttemptsFormula : IResourceCapacityFormula
    {
        public string FormulaKey => AfterlifeReturnGachaAttemptsV1;

        public IReadOnlySet<ResourceOwnerKind> SupportedOwnerKinds { get; } =
            new[]
            {
                ResourceOwnerKind.AfterlifeActor,
                ResourceOwnerKind.AfterlifeScope
            }.ToFrozenSet();

        public ResourceFormulaResult Evaluate(ResourceFormulaInput input)
        {
            switch (input)
            {
                case GuardianReturnGachaFormulaInput guardian
                    when guardian.Owner.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                         string.Equals(
                             guardian.Owner.Realm,
                             "chaos_sea",
                             StringComparison.Ordinal) &&
                         guardian.Reputation is >= -100 and <= 300 &&
                         guardian.AbodePower is >= 0 and <= 100 &&
                         guardian.FounderExtraCharges >= 0 &&
                         ResourceMaterializationContract.IsExactIdentifier(
                             guardian.ReturnCycleId):
                    var guardianValue =
                        (decimal)GuardianGachaChargeRules.GetChargesPerReturnForReputation(
                            guardian.Reputation,
                            guardian.AbodePower) +
                        guardian.FounderExtraCharges;
                    return Success(
                        guardianValue,
                        Fingerprint(
                            FormulaKey,
                            DescribeOwner(guardian.Owner),
                            guardian.OwnerAuthorityFingerprint,
                            "guardian",
                            guardian.Reputation.ToString(CultureInfo.InvariantCulture),
                            guardian.AbodePower.ToString(CultureInfo.InvariantCulture),
                            guardian.FounderExtraCharges.ToString(CultureInfo.InvariantCulture),
                            guardian.ReturnCycleId));

                case ShiningReturnGachaFormulaInput shining
                    when shining.Owner.OwnerKind == ResourceOwnerKind.AfterlifeScope &&
                         string.Equals(
                             shining.Owner.Realm,
                             "shining_abode",
                             StringComparison.Ordinal) &&
                         shining.RadianceTier is >= 0 and <= 4 &&
                         ResourceMaterializationContract.IsExactIdentifier(
                             shining.ReturnCycleId):
                    return Success(
                        ShiningAbodeState.GetShiningGachaChargesPerReturn(
                            shining.RadianceTier),
                        Fingerprint(
                            FormulaKey,
                            DescribeOwner(shining.Owner),
                            shining.OwnerAuthorityFingerprint,
                            "shining_abode",
                            shining.RadianceTier.ToString(CultureInfo.InvariantCulture),
                            shining.ReturnCycleId));

                default:
                    return Failure(
                        "resourceCapacity.formulaInput",
                        "resource_capacity_formula_input_invalid",
                        "validated Guardian or Shining return-cycle input",
                        input.GetType().Name);
            }
        }
    }

    private static bool IsCurrentMortalPlayer(ResourceFormulaOwner owner) =>
        owner.OwnerKind == ResourceOwnerKind.Player &&
        string.Equals(owner.Realm, "mortal_world", StringComparison.Ordinal) &&
        string.Equals(owner.ResourceOwnerId, "player_current", StringComparison.Ordinal);

    private static decimal TruncateThreeQuarters(int value) =>
        decimal.Truncate(value * 3m / 4m);

    private static decimal TruncateThreeHalves(int value) =>
        decimal.Truncate(value * 3m / 2m);

    private static bool IsDefinitionNumber(ResourceDefinition definition, decimal value) =>
        definition.NumericKind != ResourceNumericKind.Integer ||
        ResourceMaterializationContract.IsIntegral(value);

    private static ResourceFormulaResult RejectStale(
        ResourceFormulaResult result,
        string? expectedAuthorityFingerprint,
        string code,
        string path)
    {
        if (expectedAuthorityFingerprint == null ||
            string.Equals(
                expectedAuthorityFingerprint,
                result.AuthorityFingerprint,
                StringComparison.Ordinal))
        {
            return result;
        }

        return Failure(
            path,
            code,
            "exact current authority-input fingerprint",
            expectedAuthorityFingerprint);
    }

    private static ResourceFormulaResult Success(decimal value, string fingerprint) =>
        new(value, fingerprint, Array.Empty<ValidationIssue>());

    private static ResourceFormulaResult Failure(
        string path,
        string code,
        string expected,
        string actual)
    {
        var issues = new List<ValidationIssue>();
        Add(issues, path, code, expected, actual);
        return new ResourceFormulaResult(null, null, issues);
    }

    private static string Fingerprint(params string[] values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> lengthBytes = stackalloc byte[sizeof(int)];
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteInt32LittleEndian(lengthBytes, bytes.Length);
            hash.AppendData(lengthBytes);
            hash.AppendData(bytes);
        }
        return "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string DescribeDefinition(ResourceDefinition definition) =>
        string.Join(
            "\u001f",
            definition.ResourceKey,
            definition.DefinitionVersion.ToString(CultureInfo.InvariantCulture),
            definition.Materialization.DefinitionId,
            definition.Materialization.Seal);

    private static string DescribeOwner(ResourceFormulaOwner owner) =>
        string.Join(
            "\u001f",
            owner.Realm,
            ResourceDefinitionCatalog.GetOwnerKindToken(owner.OwnerKind),
            owner.ResourceOwnerId);

    private static string Format(decimal value) =>
        value.ToString("G29", CultureInfo.InvariantCulture);

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            path,
            code,
            expected,
            actual,
            IssueCategory.ClientOwnedSurface);
}
