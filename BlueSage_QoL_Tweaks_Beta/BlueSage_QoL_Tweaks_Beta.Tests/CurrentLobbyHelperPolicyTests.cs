using System.Reflection;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

public sealed class CurrentLobbyHelperPolicyTests
{
    [Test]
    public void Lobby_transition_excludes_stale_published_helpers_from_the_active_authority_set()
    {
        Type? policy = Assembly.GetExecutingAssembly().GetType(
            "BlueSage.QoLTweaks.Core.CurrentLobbyHelperPolicy");
        Assert.That(policy, Is.Not.Null,
            "The active-lobby helper filter must exist before stale authority can be excluded.");

        MethodInfo? filter = policy!.GetMethod(
            "FilterCurrent",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(filter, Is.Not.Null);

        var result = (string[])filter!.Invoke(
            null,
            new object[]
            {
                new[] { "76561198000000003", "76561198000000002" },
                new[] { "76561198000000001", "76561198000000002" },
            })!;

        Assert.That(result, Is.EqualTo(new[] { "76561198000000002" }));
    }
}
