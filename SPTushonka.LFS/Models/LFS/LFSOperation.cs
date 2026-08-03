using System.Text.Json;
using System.Text.Json.Serialization;

namespace SPTushonka.LFS.Models.LFS;

public enum LFSOperation
{
    Download,
    Upload,
    Unknown,
}
