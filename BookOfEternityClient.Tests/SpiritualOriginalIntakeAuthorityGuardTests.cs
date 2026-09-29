using System.Reflection;
using System.Runtime.CompilerServices;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies original intake preflight preserves unrelated accepted-turn claims without cache mutation.
/// </summary>
public sealed class SpiritualOriginalIntakeAuthorityGuardTests
{
    /// <summary>
    /// Rejects an open treatment receipt even when common validation has already been consumed.
    /// </summary>
    [Fact]
    public void OriginalIntakeGuard_RejectsOpenReceiptWithNoCommonPlan()
    {
        var state = CreateBoundState();
        var receiptField = StateField(state, "_openTreatmentPublicationReceipt");
        var receipt = RuntimeHelpers.GetUninitializedObject(receiptField.FieldType);
        receiptField.SetValue(state, receipt);
        Assert.False(CanBegin(state, new MortalItemIdentityFactory(), checkPlansAndItems: false));
        Assert.Same(receipt, receiptField.GetValue(state));
    }

    /// <summary>
    /// Rejects a foreign validated item factory before rotating its fence, while allowing this exact factory.
    /// </summary>
    [Fact]
    public void OriginalIntakeGuard_RejectsForeignItemRegistrationWithoutMutation()
    {
        var state = CreateBoundState();
        var cache = StateField(state, "_mortalItems").GetValue(state);
        Assert.NotNull(cache);
        var cacheType = cache.GetType();
        var factoryField = cacheType.GetField("_identityFactory", BindingFlags.Instance | BindingFlags.NonPublic);
        var validatedField = cacheType.GetField("_validated", BindingFlags.Instance | BindingFlags.NonPublic);
        var fenceField = cacheType.GetField("_validatedFence", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(factoryField);
        Assert.NotNull(validatedField);
        Assert.NotNull(fenceField);
        var foreignFactory = new MortalItemIdentityFactory();
        factoryField.SetValue(cache, foreignFactory);
        validatedField.SetValue(cache, true);
        var fence = fenceField.GetValue(cache);

        Assert.False(CanBegin(state, new MortalItemIdentityFactory(), checkPlansAndItems: true));
        Assert.True(CanBegin(state, foreignFactory, checkPlansAndItems: true));
        Assert.Same(fence, fenceField.GetValue(cache));
        Assert.True(Assert.IsType<bool>(validatedField.GetValue(cache)));
    }

    /// <summary>
    /// Rejects stale physical generations and revisions without rebinding the existing authority state.
    /// </summary>
    [Fact]
    public void OriginalIntakeGuard_RejectsStaleGenerationWithoutRebinding()
    {
        var state = CreateBoundState();
        var factory = new MortalItemIdentityFactory();
        var guard = state.GetType().GetMethod("CanBeginSpiritualOriginalIntake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(guard);

        Assert.False(Assert.IsType<bool>(guard.Invoke(state,
            ["generation-b", 1L, factory, false])));
        Assert.False(Assert.IsType<bool>(guard.Invoke(state,
            ["generation-a", 2L, factory, false])));
        Assert.True(CanBegin(state, factory, checkPlansAndItems: false));
        Assert.Equal("generation-a", StateField(state, "_sessionGeneration").GetValue(state));
        Assert.Equal(1L, StateField(state, "_sessionGenerationRevision").GetValue(state));
    }

    /// <summary>
    /// Excludes a validated common plan from staged intake while leaving treatment-only preflight read-only.
    /// </summary>
    [Fact]
    public void OriginalIntakeGuard_RejectsCommonPlanWithoutConsumingIt()
    {
        var state = CreateBoundState();
        var common = StateField(state, "_commonPlan").GetValue(state);
        Assert.NotNull(common);
        var validated = StateField(common, "_validatedResult");
        var plan = RuntimeHelpers.GetUninitializedObject(validated.FieldType);
        validated.SetValue(common, plan);

        Assert.False(CanBegin(state, new MortalItemIdentityFactory(), checkPlansAndItems: true));
        Assert.True(CanBegin(state, new MortalItemIdentityFactory(), checkPlansAndItems: false));
        Assert.Same(plan, validated.GetValue(common));
    }

    /// <summary>
    /// Creates an authority state bound to a stable test generation.
    /// </summary>
    /// <returns>
    /// Bound private registry state for read-only probe assertions.
    /// </returns>
    private static object CreateBoundState()
    {
        var stateType = typeof(AcceptedTurnAuthorityRegistry).GetNestedType(
            "AcceptedTurnAuthorityState", BindingFlags.NonPublic);
        Assert.NotNull(stateType);
        var state = Activator.CreateInstance(stateType, BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [null, null, null, null], culture: null);
        Assert.NotNull(state);
        stateType.GetMethod("BindRootGeneration", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(state, ["generation-a", 1L]);
        return state;
    }

    /// <summary>
    /// Locates a private claim field without changing the authority state.
    /// </summary>
    /// <param name="state">
    /// Bound registry state under test.
    /// </param>
    /// <param name="name">
    /// Exact private field name.
    /// </param>
    /// <returns>
    /// Reflected field for the assertion or test fixture setup.
    /// </returns>
    private static FieldInfo StateField(object state, string name)
    {
        var field = state.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field;
    }

    /// <summary>
    /// Invokes the bound state's intake probe with its original test generation.
    /// </summary>
    /// <param name="state">
    /// Bound registry state under test.
    /// </param>
    /// <param name="factory">
    /// Exact item factory offered to the probe.
    /// </param>
    /// <param name="checkPlansAndItems">
    /// Includes common and item claims when <see langword="true"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when that state permits intake; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool CanBegin(object state, MortalItemIdentityFactory factory, bool checkPlansAndItems)
    {
        var guard = state.GetType().GetMethod("CanBeginSpiritualOriginalIntake",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(guard);
        return Assert.IsType<bool>(guard.Invoke(state,
            ["generation-a", 1L, factory, checkPlansAndItems]));
    }
}
