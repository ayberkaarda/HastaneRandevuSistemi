using HastaneRandevu.Domain.Entities;

namespace HastaneRandevu.Domain.Repositories;

public interface IPatientRepository
{
    Task<bool> NationalIdExistsAsync(string nationalId, CancellationToken cancellationToken = default);

    /// <summary>Stores a new patient and returns its id.</summary>
    /// <exception cref="DuplicateNationalIdException">A patient with the same national id exists.</exception>
    Task<int> AddAsync(Patient patient, CancellationToken cancellationToken = default);
}

/// <summary>Raised when a national id is already registered.</summary>
public sealed class DuplicateNationalIdException : Exception
{
    public DuplicateNationalIdException()
        : base("The national id is already registered.")
    {
    }

    public DuplicateNationalIdException(string message)
        : base(message)
    {
    }

    public DuplicateNationalIdException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
