using System.Text.Json;
using System.Text.Json.Serialization;
using SPTushonka.LFS.Models.LFS;

namespace SPTushonka.LFS.Converters;

/// <summary>
/// Maps unrecognised operations to <see cref="LFSOperation.Unknown"/> so the batch handler can
/// answer with a 422 and an LFS error body, rather than model binding failing with a bare 400.
/// </summary>
public sealed class LFSOperationConverter : JsonConverter<LFSOperation>
{
    public override LFSOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetString() switch
        {
            "download" => LFSOperation.Download,
            "upload" => LFSOperation.Upload,
            _ => LFSOperation.Unknown,
        };
    }

    public override void Write(Utf8JsonWriter writer, LFSOperation value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
    }
}
