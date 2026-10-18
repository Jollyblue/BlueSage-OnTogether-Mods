using System.Reflection;

namespace BlueSage.QoLTweaks.Core;

// Reads a public instance property or, failing that, a public instance field. The game's
// PurrLobby.Lobby is a struct of plain fields (Members, MaxPlayers, Name), which GetProperty alone
// never finds, so Host Health showed "lobby=?" (UAT 2026-10-18).
internal static class ReflectedMember
{
    private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;

    internal static bool TryRead(object target, string name, out object value)
    {
        value = null;
        if (target == null || string.IsNullOrEmpty(name))
        {
            return false;
        }

        System.Type type = target.GetType();
        PropertyInfo property = type.GetProperty(name, Public);
        if (property != null && property.GetIndexParameters().Length == 0)
        {
            value = property.GetValue(target, null);
            return true;
        }

        FieldInfo field = type.GetField(name, Public);
        if (field != null)
        {
            value = field.GetValue(target);
            return true;
        }

        return false;
    }
}
