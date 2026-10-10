using System;
using System.Collections.Generic;

namespace EvuMiscCompat.Core;

public enum IlOpKind
{
    Other,
    Ldarg0,
    Ldarg1,
    Ldarg2,
    LdcI4_3,
    BneUn,
    Call,
    Brtrue,
}

public readonly struct IlOp : IEquatable<IlOp>
{
    public IlOp(IlOpKind kind, string? name = null)
    {
        Kind = kind;
        Name = name;
    }

    public IlOpKind Kind { get; }

    public string? Name { get; }

    public bool Equals(IlOp other)
    {
        return Kind == other.Kind && Name == other.Name;
    }

    public override bool Equals(object? obj)
    {
        return obj is IlOp other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return ((int)Kind * 397) ^ (Name?.GetHashCode() ?? 0);
        }
    }
}

/// <summary>
/// Puts Dual Wield's offhand call back on the one-handed compare inside Humanoid.EquipItem.
/// Trinkets Unbound inserts three instructions before that compare, so Dual Wield's
/// fixed four-instruction window no longer matches.
/// </summary>
public static class DualWieldWindow
{
    public const string OffhandMethod = "CheckDualOneHandedWeaponEquip";

    public static List<IlOp> Repair(IReadOnlyList<IlOp> code)
    {
        var result = new List<IlOp>(code.Count + 5);
        for (var i = 0; i < code.Count; i++)
        {
            result.Add(code[i]);
            if (!IsOneHandedBranch(code, i) || HasOffhandFallthrough(code, i))
            {
                continue;
            }

            var label = code[i].Name;
            result.Add(new IlOp(IlOpKind.Ldarg0));
            result.Add(new IlOp(IlOpKind.Ldarg1));
            result.Add(new IlOp(IlOpKind.Ldarg2));
            result.Add(new IlOp(IlOpKind.Call, OffhandMethod));
            result.Add(new IlOp(IlOpKind.Brtrue, label));
        }

        return result;
    }

    static bool IsOneHandedBranch(IReadOnlyList<IlOp> code, int index)
    {
        if (code[index].Kind != IlOpKind.BneUn)
        {
            return false;
        }

        if (index < 1 || code[index - 1].Kind != IlOpKind.LdcI4_3)
        {
            return false;
        }

        return IsVanillaWindow(code, index) || IsTrinketsWindow(code, index);
    }

    static bool IsVanillaWindow(IReadOnlyList<IlOp> code, int branch)
    {
        return branch >= 4
            && code[branch - 4].Kind == IlOpKind.Ldarg1
            && code[branch - 3].Kind == IlOpKind.Other
            && code[branch - 2].Kind == IlOpKind.Other;
    }

    static bool IsTrinketsWindow(IReadOnlyList<IlOp> code, int branch)
    {
        return branch >= 7
            && code[branch - 7].Kind == IlOpKind.Ldarg1
            && code[branch - 6].Kind == IlOpKind.Other
            && code[branch - 5].Kind == IlOpKind.Other
            && code[branch - 4].Kind == IlOpKind.Ldarg0
            && code[branch - 3].Kind == IlOpKind.Ldarg1
            && code[branch - 2].Kind == IlOpKind.Call;
    }

    static bool HasOffhandFallthrough(IReadOnlyList<IlOp> code, int branch)
    {
        if (branch + 5 >= code.Count)
        {
            return false;
        }

        return code[branch + 1].Kind == IlOpKind.Ldarg0
            && code[branch + 2].Kind == IlOpKind.Ldarg1
            && code[branch + 3].Kind == IlOpKind.Ldarg2
            && code[branch + 4].Kind == IlOpKind.Call
            && code[branch + 4].Name == OffhandMethod
            && code[branch + 5].Kind == IlOpKind.Brtrue
            && code[branch + 5].Name == code[branch].Name;
    }
}
