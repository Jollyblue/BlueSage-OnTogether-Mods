using BlueSage.QoLTweaks.Contracts;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class DiagnosticsProviderDiscoveryContractTests
{
    [Test]
    public void Generic_diagnostics_contract_is_read_only_bounded_and_has_no_private_names()
    {
        Type provider = typeof(IBlueSageDiagnosticsProviderV1);
        Type snapshot = typeof(BlueSageDiagnosticsSnapshotV1);

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetMethods(), Has.Length.EqualTo(1));
            Assert.That(provider.GetMethods()[0].Name, Is.EqualTo("GetSnapshot"));
            Assert.That(provider.GetProperties(), Is.Empty);
            Assert.That(snapshot.GetProperties().Any(property => property.CanWrite), Is.False);
            Assert.That(snapshot.GetProperties().Any(property => property.PropertyType == typeof(string)), Is.False);
            Assert.That(string.Join("|", provider.GetMembers().Select(member => member.Name)),
                Does.Not.Contain("BlueExclusive").And.Not.Contain("Creator"));
        });
    }

    [Test]
    public void Generic_diagnostics_control_exposes_only_one_bounded_visibility_action()
    {
        Type control = typeof(IBlueSageDiagnosticsControlV2);

        Assert.Multiple(() =>
        {
            Assert.That(control.GetMethods(), Has.Length.EqualTo(1));
            Assert.That(control.GetMethods()[0].Name, Is.EqualTo("TrySetOverlayVisible"));
            Assert.That(control.GetMethods()[0].ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(control.GetMethods()[0].GetParameters().Select(parameter => parameter.ParameterType),
                Is.EqualTo(new[] { typeof(bool) }));
            Assert.That(string.Join("|", control.GetMembers().Select(member => member.Name)),
                Does.Not.Contain("Inspector").And.Not.Contain("Private"));
        });
    }

    [TestCase(-1, 100)]
    [TestCase(0, 100)]
    [TestCase(49, 100)]
    [TestCase(100, 100)]
    [TestCase(1_000, 1_000)]
    [TestCase(10_000, 5_000)]
    public void Diagnostic_interval_is_clamped(int requested, int expected)
    {
        Assert.That(BlueSageDiagnosticsPolicyV1.ClampIntervalMilliseconds(requested), Is.EqualTo(expected));
    }
}
