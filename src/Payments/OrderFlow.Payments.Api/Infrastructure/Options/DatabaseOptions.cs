using System.ComponentModel.DataAnnotations;
using Npgsql;

namespace OrderFlow.Payments.Infrastructure.Options;

public sealed class DatabaseOptions : IValidatableObject
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        Exception? validationException = null;
        try
        {
            _ = new NpgsqlConnectionStringBuilder(ConnectionString);
        }
        catch (Exception exception)
        {
            validationException = exception;
        }

        return validationException is null
            ? Array.Empty<ValidationResult>()
            : new[] { new ValidationResult($"Database connection string is invalid: {validationException.Message}", new[] { nameof(ConnectionString) }) };
    }
}
