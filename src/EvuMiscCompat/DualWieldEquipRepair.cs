using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using EvuMiscCompat.Core;
using HarmonyLib;

namespace EvuMiscCompat;

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
static class DualWieldEquipRepair
{
    [HarmonyTranspiler]
    [HarmonyPriority(Priority.Last)]
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var offhand = FindOffhand();
        var list = new List<CodeInstruction>(instructions);
        if (offhand == null)
        {
            return list;
        }

        var model = new List<IlOp>(list.Count);
        for (var i = 0; i < list.Count; i++)
        {
            model.Add(Map(list[i]));
        }

        var repaired = DualWieldWindow.Repair(model);
        if (repaired.Count == model.Count)
        {
            return list;
        }

        var output = new List<CodeInstruction>(repaired.Count);
        object? branch = null;
        var source = 0;
        for (var i = 0; i < repaired.Count; i++)
        {
            if (source < list.Count && Map(list[source]).Equals(repaired[i]))
            {
                output.Add(list[source]);
                if (list[source].opcode == OpCodes.Bne_Un)
                {
                    branch = list[source].operand;
                }

                source++;
                continue;
            }

            output.Add(Emit(repaired[i], branch, offhand));
        }

        if (source != list.Count)
        {
            Plugin.Log.LogWarning("Dual Wield offhand repair did not consume Humanoid.EquipItem. Left the method unchanged.");
            return list;
        }

        Plugin.Log.LogInfo("Restored Dual Wield offhand equip on Humanoid.EquipItem.");
        return output;
    }

    static MethodInfo? FindOffhand()
    {
        var outer = AccessTools.TypeByName("DualWield.DualWield");
        if (outer == null)
        {
            return null;
        }

        var inner = AccessTools.Inner(outer, "Patch_Humanoid_EquipItem");
        if (inner == null)
        {
            return null;
        }

        return AccessTools.DeclaredMethod(inner, DualWieldWindow.OffhandMethod);
    }

    static IlOp Map(CodeInstruction instruction)
    {
        if (instruction.opcode == OpCodes.Ldarg_0)
        {
            return new IlOp(IlOpKind.Ldarg0);
        }

        if (instruction.opcode == OpCodes.Ldarg_1)
        {
            return new IlOp(IlOpKind.Ldarg1);
        }

        if (instruction.opcode == OpCodes.Ldarg_2)
        {
            return new IlOp(IlOpKind.Ldarg2);
        }

        if (instruction.opcode == OpCodes.Ldc_I4_3)
        {
            return new IlOp(IlOpKind.LdcI4_3);
        }

        if (instruction.opcode == OpCodes.Bne_Un)
        {
            return new IlOp(IlOpKind.BneUn, OperandName(instruction.operand));
        }

        if (instruction.opcode == OpCodes.Brtrue)
        {
            return new IlOp(IlOpKind.Brtrue, OperandName(instruction.operand));
        }

        if (instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo method)
        {
            return new IlOp(IlOpKind.Call, method.Name);
        }

        return new IlOp(IlOpKind.Other);
    }

    static CodeInstruction Emit(IlOp op, object? branch, MethodInfo offhand)
    {
        switch (op.Kind)
        {
            case IlOpKind.Ldarg0:
                return new CodeInstruction(OpCodes.Ldarg_0);
            case IlOpKind.Ldarg1:
                return new CodeInstruction(OpCodes.Ldarg_1);
            case IlOpKind.Ldarg2:
                return new CodeInstruction(OpCodes.Ldarg_2);
            case IlOpKind.Call:
                return new CodeInstruction(OpCodes.Call, offhand);
            case IlOpKind.Brtrue:
                return new CodeInstruction(OpCodes.Brtrue, branch);
            default:
                return new CodeInstruction(OpCodes.Nop);
        }
    }

    static string? OperandName(object? operand)
    {
        return operand?.ToString();
    }
}
