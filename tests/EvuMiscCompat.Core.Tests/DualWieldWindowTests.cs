using System.Collections.Generic;
using EvuMiscCompat.Core;
using Xunit;

namespace EvuMiscCompat.Core.Tests;

public sealed class DualWieldWindowTests
{
    [Fact]
    public void VanillaWindow_WithOffhandCall_IsUnchanged()
    {
        var code = new List<IlOp>
        {
            new IlOp(IlOpKind.Ldarg1),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.LdcI4_3),
            new IlOp(IlOpKind.BneUn, "skip"),
            new IlOp(IlOpKind.Ldarg0),
            new IlOp(IlOpKind.Ldarg1),
            new IlOp(IlOpKind.Ldarg2),
            new IlOp(IlOpKind.Call, DualWieldWindow.OffhandMethod),
            new IlOp(IlOpKind.Brtrue, "skip"),
            new IlOp(IlOpKind.Other),
        };

        Assert.Equal(code, DualWieldWindow.Repair(code));
    }

    [Fact]
    public void VanillaWindow_WithoutOffhandCall_GainsOne()
    {
        var code = new List<IlOp>
        {
            new IlOp(IlOpKind.Ldarg1),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.LdcI4_3),
            new IlOp(IlOpKind.BneUn, "skip"),
            new IlOp(IlOpKind.Other),
        };

        var repaired = DualWieldWindow.Repair(code);

        Assert.Equal(new IlOp(IlOpKind.Call, DualWieldWindow.OffhandMethod), repaired[8]);
        Assert.Equal(new IlOp(IlOpKind.Brtrue, "skip"), repaired[9]);
        Assert.Equal(new IlOp(IlOpKind.Other), repaired[10]);
    }

    [Fact]
    public void TrinketsInsertion_GainsOffhandCall_OnTheSameBranch()
    {
        var code = new List<IlOp>
        {
            new IlOp(IlOpKind.Ldarg1),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.Ldarg0),
            new IlOp(IlOpKind.Ldarg1),
            new IlOp(IlOpKind.Call, "GetAssignmentType"),
            new IlOp(IlOpKind.LdcI4_3),
            new IlOp(IlOpKind.BneUn, "skip"),
            new IlOp(IlOpKind.Other),
        };

        var repaired = DualWieldWindow.Repair(code);

        Assert.Equal(
            new List<IlOp>
            {
                new IlOp(IlOpKind.Ldarg1),
                new IlOp(IlOpKind.Other),
                new IlOp(IlOpKind.Other),
                new IlOp(IlOpKind.Ldarg0),
                new IlOp(IlOpKind.Ldarg1),
                new IlOp(IlOpKind.Call, "GetAssignmentType"),
                new IlOp(IlOpKind.LdcI4_3),
                new IlOp(IlOpKind.BneUn, "skip"),
                new IlOp(IlOpKind.Ldarg0),
                new IlOp(IlOpKind.Ldarg1),
                new IlOp(IlOpKind.Ldarg2),
                new IlOp(IlOpKind.Call, DualWieldWindow.OffhandMethod),
                new IlOp(IlOpKind.Brtrue, "skip"),
                new IlOp(IlOpKind.Other),
            },
            repaired);
    }

    [Fact]
    public void OtherOneHandedCompares_StayUntouched()
    {
        var shortBranch = new List<IlOp>
        {
            new IlOp(IlOpKind.Ldarg1),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.LdcI4_3),
            new IlOp(IlOpKind.Other),
        };
        var currentItem = new List<IlOp>
        {
            new IlOp(IlOpKind.Ldarg0),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.Other),
            new IlOp(IlOpKind.LdcI4_3),
            new IlOp(IlOpKind.BneUn, "skip"),
        };

        Assert.Equal(shortBranch, DualWieldWindow.Repair(shortBranch));
        Assert.Equal(currentItem, DualWieldWindow.Repair(currentItem));
    }
}
