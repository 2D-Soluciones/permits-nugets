using System.Text.Json.Serialization;
using DDS.General.Logging.Javascript;
using DDS.General.Logging.Javascript.SourceMaps.Model;
using JetBrains.Annotations;

namespace DDS.General.Json;

[JsonSerializable(typeof(IDictionary<string, object>))]
[JsonSerializable(typeof(SourceMapFile))]
[JsonSerializable(typeof(SourceMapSection))]
[UsedImplicitly]
internal sealed partial  class GeneralJsonSerializerContext : JsonSerializerContext;
