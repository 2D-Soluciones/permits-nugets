using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace DDS.General.Validations;

/// <summary>
///     Clase de validacion simple.
/// </summary>
[PublicAPI]
public sealed class Validator
{
	/// <summary>
	///     Delegado que corre al llamar a <see cref="Validator.Execute(System.Threading.CancellationToken)" />.
	/// </summary>
	public delegate Task<Result> AsyncValidation(CancellationToken cancellationToken);

	/// <summary>
	///     Delegado que corre al llamar a <see cref="Validator.Execute(System.Threading.CancellationToken)" />.
	/// </summary>
	public delegate Result SyncValidation();

	private readonly List<RuleType> _rulesOrder = new(2);
	private List<AsyncValidation>? _asyncRules;
	private List<SyncValidation>? _rules;

	private Validator() { }

	/// <summary>
	///     Le agrega una regla nueva al validador.
	/// </summary>
	/// <param name="validationRule"></param>
	public Validator AddRule(SyncValidation validationRule)
	{
		_rulesOrder.Add(RuleType.Sync);
		Rules.Add(validationRule);
		return this;
	}

	/// <summary>
	///     Le agrega una regla nueva al validador.
	/// </summary>
	/// <param name="asyncValidationRule"></param>
	public Validator AddRule(AsyncValidation asyncValidationRule)
	{
		_rulesOrder.Add(RuleType.Async);
		AsyncRules.Add(asyncValidationRule);
		return this;
	}

	/// <summary>
	///     Valida una por una las reglas agregadas a este validador.
	/// </summary>
	/// <param name="cancellationToken"></param>
	/// <returns></returns>
	public ValueTask<Result> Execute(CancellationToken cancellationToken)
	{
		if (_asyncRules is not null)
		{
			return ValidateSlow(cancellationToken);
		}

		if (_rules is null)
		{
			return ValueTask.FromResult(Result.Success);
		}

		foreach (var rule in _rules)
		{
			var result = rule();

			if (result.IsFaulted)
			{
				return ValueTask.FromResult(result);
			}
		}

		return ValueTask.FromResult(Result.Success);
	}

	/// <summary>
	///     Valida una por una las reglas agregadas a este validador.
	/// </summary>
	/// <returns></returns>
	/// <exception cref="InvalidOperationException">Si el validador tiene alguna regla asincronica.</exception>
	public Result Execute()
	{
		if (_asyncRules is not null)
		{
			Throw();
		}

		if (_rules is null)
		{
			return Result.Success;
		}

		foreach (var rule in _rules)
		{
			var result = rule();

			if (result.IsFaulted)
			{
				return result;
			}
		}

		return Result.Success;
	}



	/// <summary>
	///     Crea un <see cref="Validator" /> nuevo.
	/// </summary>
	/// <param name="configure">Configuration options.</param>
	/// <returns></returns>
	public static Validator New(Action<Validator> configure)
	{
		var validator = new Validator();
		configure(validator);
		return validator;
	}

	private List<AsyncValidation> AsyncRules
	{
		get { return _asyncRules ??= new(2); }
	}

	private List<SyncValidation> Rules
	{
		get { return _rules ??= new(2); }
	}

	private async ValueTask<Result> ValidateSlow(CancellationToken cancellationToken)
	{
		var asyncIndex = 0;
		var syncIndex = 0;

		foreach (var rule in _rulesOrder)
		{
			if (rule == RuleType.Sync)
			{
				var syncResult = _rules![syncIndex++]();

				if (syncResult.IsFaulted)
				{
					return syncResult;
				}

				continue;
			}

			var asyncResult = await _asyncRules![asyncIndex++](cancellationToken);

			if (asyncResult.IsFaulted)
			{
				return asyncResult;
			}
		}

		return new Success();
	}

	[DoesNotReturn]
	[StackTraceHidden]
	private static void Throw()
	{
		throw new InvalidOperationException("This validator has async rules. Please use the async version of this method (Validator.Execute(System.Threading.CancellationToken)).");
	}

	private enum RuleType
	{
		Sync,
		Async
	}
}
