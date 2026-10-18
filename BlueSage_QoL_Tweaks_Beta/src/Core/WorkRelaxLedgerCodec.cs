#nullable enable

using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace BlueSage.QoLTweaks.Core;

internal static class WorkRelaxLedgerCodec
{
    public static string Serialize(WorkRelaxLedger ledger)
    {
        Validate(ledger);
        using var stream = new MemoryStream();
        CreateSerializer().WriteObject(stream, ledger);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static WorkRelaxLedger Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new SerializationException("The Work / Relax ledger is empty.");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var ledger = CreateSerializer().ReadObject(stream) as WorkRelaxLedger
            ?? throw new SerializationException("The Work / Relax ledger has no root object.");
        ledger.NormalizeAfterDeserialize();
        Validate(ledger);
        return ledger;
    }

    private static DataContractJsonSerializer CreateSerializer()
    {
        return new DataContractJsonSerializer(typeof(WorkRelaxLedger));
    }

    private static void Validate(WorkRelaxLedger ledger)
    {
        if (ledger == null)
        {
            throw new ArgumentNullException(nameof(ledger));
        }

        if (ledger.SchemaVersion != 1)
        {
            throw new SerializationException("Unsupported Work / Relax ledger schema.");
        }

        if (string.IsNullOrWhiteSpace(ledger.ProfileKey))
        {
            throw new SerializationException("The Work / Relax ledger profile key is missing.");
        }
    }
}
