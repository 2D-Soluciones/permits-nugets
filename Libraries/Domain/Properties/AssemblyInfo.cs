using System.Runtime.CompilerServices;
using Vogen;

[assembly: VogenDefaults(typeof(string), debuggerAttributes: DebuggerAttributeGeneration.Basic)]
[assembly: InternalsVisibleTo("DDS.Repository")]
