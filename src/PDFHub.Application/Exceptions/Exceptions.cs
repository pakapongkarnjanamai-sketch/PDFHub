namespace PDFHub.Application.Exceptions;

/// <summary>Input that fails validation. Keys are PascalCase DTO property names; returned as a 400 ValidationProblemDetails.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationException For(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}

/// <summary>A business rule the request breaks; the message is shown to the user (400, ProblemDetails.detail).</summary>
public sealed class BusinessRuleException(string message) : Exception(message);

/// <summary>Collects field errors and throws them together, so the user sees every problem at once.</summary>
internal sealed class ValidationErrors
{
    private readonly Dictionary<string, List<string>> _errors = [];

    public bool Any => _errors.Count > 0;

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var list))
            _errors[field] = list = [];
        list.Add(message);
    }

    public void ThrowIfAny()
    {
        if (Any)
            throw new ValidationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
    }
}
