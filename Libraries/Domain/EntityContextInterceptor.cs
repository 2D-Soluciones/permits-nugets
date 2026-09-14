using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DDS.Domain;

/// <summary>
///     Registra que <see cref="DbContext" /> materializo cada entidad, para que <c>ForceLoadCollection</c> pueda
///     volver a encontrar ese contexto teniendo nada mas que la entidad.
/// </summary>
/// <remarks>
///     Un scope ambiente no puede contestar esto. Con <c>AddDbContextPool</c> el constructor del contexto corre una
///     sola vez, asi que un scope abierto ahi queda muerto desde el segundo prestamo y el force-load degrada
///     calladito justo al comportamiento del change tracker que existe para evitar. Una unica ranura ambiente
///     tampoco puede tener dos tipos de DbContext vivos en el mismo request, que es como se termina preguntandole al
///     contexto equivocado por una entidad de la que nunca oyo hablar. La materializacion, en cambio, pasa una vez
///     por entidad y justo en el contexto que la posee.
///     <para>
///         <see cref="Instance" /> se comparte a proposito: EF cachea su service provider interno usando como clave
///         los interceptores de materializacion que trae el set de opciones, asi que registrar una instancia nueva por
///         contexto armaria un provider nuevo por cada contexto y dispararia <c>ManyServiceProvidersCreatedWarning</c>.
///     </para>
/// </remarks>
[PublicAPI]
public sealed class EntityContextInterceptor : IMaterializationInterceptor
{
	private static readonly ConditionalWeakTable<object, Owner> _entityOwners = new();
	private static readonly ConditionalWeakTable<DbContext, Owner> _contextOwners = new();

	/// <summary>
	///     La instancia que hay que pasarle a <c>AddInterceptors</c>.
	/// </summary>
	public static readonly EntityContextInterceptor Instance = new();

	private EntityContextInterceptor() { }

	/// <inheritdoc />
	public object InitializedInstance(MaterializationInterceptionData materializationData, object instance)
	{
		var context = materializationData.Context;
		var owner = _contextOwners.GetValue(context, static _ => new());

		// Se rearma en cada materializacion, que es lo que permite que esto sobreviva al reset de un contexto pooleado.
		owner.Context = context;
		_entityOwners.AddOrUpdate(instance, owner);
		return instance;
	}

	/// <summary>
	///     Se olvida de <paramref name="dbContext" />, para que un contexto ya disposeado o devuelto al pool nunca
	///     vuelva a entregarse para alguna de las entidades que materializo.
	/// </summary>
	/// <param name="dbContext"></param>
	public static void Release(DbContext dbContext)
	{
		if (_contextOwners.TryGetValue(dbContext, out var owner))
		{
			owner.Context = null;
		}
	}

	internal static DbContext? FindContext(object entity)
	{
		return _entityOwners.TryGetValue(entity, out var owner) ? owner.Context : null;
	}

	private sealed class Owner
	{
		public DbContext? Context;
	}
}
