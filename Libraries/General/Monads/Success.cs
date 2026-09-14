using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace DDS.General;

/// <summary>
///     Marcador vacio que indica un <see cref="Result" /> terminado bien, sin ningun valor en particular.
/// </summary>
[PublicAPI]
[StructLayout(LayoutKind.Sequential, Size = 1)]
public readonly record struct Success;
