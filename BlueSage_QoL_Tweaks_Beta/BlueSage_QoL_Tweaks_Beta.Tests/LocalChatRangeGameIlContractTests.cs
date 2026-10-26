using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using BlueSage.QoLTweaks.Core;
using NUnit.Framework;

namespace BlueSage.QoLTweaks.Tests;

/// <summary>
/// Reads the installed game's own code (metadata only, nothing is loaded or run) and feeds the real instruction
/// stream of TextChannelManager.OnChannelMessageReceived to the same matcher the Harmony transpiler uses. If a game
/// update moves or duplicates the 5 m distance check, this fails at the commit gate, not silently on a player's PC.
/// Skipped when the game is not installed on the machine running the tests.
/// </summary>
public sealed class LocalChatRangeGameIlContractTests
{
    private const string GameAssemblyPath =
        @"C:\Program Files (x86)\Steam\steamapps\common\On-Together\On-Together\OnTogether_Data\Managed\Assembly-CSharp.dll";

    [Test]
    public void The_games_receive_method_has_exactly_one_matching_distance_check_with_a_branch_after_it()
    {
        if (!File.Exists(GameAssemblyPath))
        {
            Assert.Ignore("The game is not installed on this machine, so its code cannot be read.");
        }

        List<string> opcodes = new List<string>();
        List<string> operands = new List<string>();
        ReadMethod(GameAssemblyPath, "TextChannelManager", "OnChannelMessageReceived", opcodes, operands);

        int[] hits = LocalChatRangePolicy.FindGateConstantIndexes(opcodes, operands);

        Assert.Multiple(() =>
        {
            Assert.That(opcodes, Is.Not.Empty, "The method body was not found.");
            Assert.That(hits, Has.Length.EqualTo(1), "Exactly one 'SqrMagnitude then 25' check, or the patch stands down.");
            Assert.That(opcodes[hits[0] + 1], Does.StartWith("b"), "The constant must feed a branch (the distance test).");
        });
    }

    [Test]
    public void The_games_speech_bubble_method_has_exactly_one_matching_distance_check()
    {
        if (!File.Exists(GameAssemblyPath))
        {
            Assert.Ignore("The game is not installed on this machine, so its code cannot be read.");
        }

        List<string> opcodes = new List<string>();
        List<string> operands = new List<string>();
        ReadMethod(GameAssemblyPath, "TextBubbleController", "ShowTextBubble_Original_1", opcodes, operands);

        Assert.That(LocalChatRangePolicy.FindGateConstantIndexes(opcodes, operands), Has.Length.EqualTo(1));
    }

    private static void ReadMethod(string path, string typeName, string methodName, List<string> opcodes, List<string> operands)
    {
        Dictionary<int, OpCode> oneByte = new Dictionary<int, OpCode>();
        Dictionary<int, OpCode> twoByte = new Dictionary<int, OpCode>();
        foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            OpCode code = (OpCode)field.GetValue(null)!;
            int low = (ushort)code.Value & 0xFF;
            if (code.Size == 1)
            {
                oneByte[low] = code;
            }
            else
            {
                twoByte[low] = code;
            }
        }

        using FileStream stream = File.OpenRead(path);
        using PEReader pe = new PEReader(stream);
        MetadataReader md = pe.GetMetadataReader();

        foreach (TypeDefinitionHandle typeHandle in md.TypeDefinitions)
        {
            TypeDefinition type = md.GetTypeDefinition(typeHandle);
            if (md.GetString(type.Name) != typeName || !type.GetDeclaringType().IsNil)
            {
                continue;
            }

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = md.GetMethodDefinition(methodHandle);
                if (md.GetString(method.Name) != methodName || method.RelativeVirtualAddress == 0)
                {
                    continue;
                }

                byte[] il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
                Decode(il, md, oneByte, twoByte, opcodes, operands);
                return;
            }
        }
    }

    private static void Decode(byte[] il, MetadataReader md, Dictionary<int, OpCode> oneByte, Dictionary<int, OpCode> twoByte,
        List<string> opcodes, List<string> operands)
    {
        int i = 0;
        while (i < il.Length)
        {
            OpCode op = il[i] == 0xFE ? twoByte[il[i + 1]] : oneByte[il[i]];
            i += op.Size;
            string operand = string.Empty;
            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.ShortInlineR:
                    operand = BitConverter.ToSingle(il, i).ToString("R", CultureInfo.InvariantCulture);
                    i += 4;
                    break;
                case OperandType.InlineMethod:
                    operand = NameOf(md, BitConverter.ToInt32(il, i));
                    i += 4;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    int count = BitConverter.ToInt32(il, i);
                    i += 4 + 4 * count;
                    break;
                default:
                    i += 4;
                    break;
            }

            opcodes.Add(op.Name!);
            operands.Add(operand);
        }
    }

    private static string NameOf(MetadataReader md, int token)
    {
        EntityHandle handle = MetadataTokens.EntityHandle(token);
        switch (handle.Kind)
        {
            case HandleKind.MemberReference:
                return md.GetString(md.GetMemberReference((MemberReferenceHandle)handle).Name);
            case HandleKind.MethodDefinition:
                return md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)handle).Name);
            default:
                return string.Empty;
        }
    }
}
