using HabitSystem.Common;
using HabitSystem.Domain;
using HabitSystem.Domain.Enums;

namespace HabitSystem.Features.Habits;

/// <summary>
/// Validated and normalized habit fields (shared by create/update)
/// </summary>
public record NormalizedHabitInput(
    string Name,
    string? Description,
    short Weight,
    short PartialWeight,
    FrequencyType FrequencyType,
    int[]? FrequencyDays,
    short? FrequencyTimes
);

/// <summary>
/// Validation rules shared by CreateHabit and UpdateHabit.
/// Days of week use the JS getDay() convention: 0=Sunday ... 6=Saturday (7 is accepted as Sunday).
/// </summary>
public static class HabitValidation
{
    public static Result<NormalizedHabitInput> Validate(
        string? name,
        string? description,
        short weight,
        short partialWeight,
        FrequencyType frequencyType,
        int[]? frequencyDays,
        short? frequencyTimes)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
            return Result<NormalizedHabitInput>.Failure("Habit name is required");

        if (trimmedName.Length > 100)
            return Result<NormalizedHabitInput>.Failure("Habit name must be at most 100 characters");

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (trimmedDescription != null && trimmedDescription.Length > Constants.MaxNoteLength)
            return Result<NormalizedHabitInput>.Failure($"Description must be at most {Constants.MaxNoteLength} characters");

        if (weight < 1 || weight > 10)
            return Result<NormalizedHabitInput>.Failure("Weight must be between 1 and 10");

        if (partialWeight < 0 || partialWeight > 10)
            return Result<NormalizedHabitInput>.Failure("Partial weight must be between 0 and 10");

        if (partialWeight > weight)
            return Result<NormalizedHabitInput>.Failure("Partial weight cannot be greater than weight");

        if (!Enum.IsDefined(frequencyType))
            return Result<NormalizedHabitInput>.Failure("Invalid frequency type");

        int[]? days = null;
        short? times = null;

        switch (frequencyType)
        {
            case FrequencyType.SpecificDays:
                if (frequencyDays == null || frequencyDays.Length == 0)
                    return Result<NormalizedHabitInput>.Failure("FrequencyDays is required for SpecificDays frequency type");

                days = NormalizeDays(frequencyDays);
                if (days.Any(d => d < 0 || d > 6))
                    return Result<NormalizedHabitInput>.Failure("FrequencyDays values must be between 0 (Sunday) and 6 (Saturday)");
                break;

            case FrequencyType.XTimesWeek:
                if (frequencyTimes == null || frequencyTimes < 1 || frequencyTimes > 7)
                    return Result<NormalizedHabitInput>.Failure("FrequencyTimes must be between 1 and 7 for XTimesWeek frequency type");

                times = frequencyTimes;
                break;
        }

        return Result<NormalizedHabitInput>.Success(new NormalizedHabitInput(
            trimmedName, trimmedDescription, weight, partialWeight, frequencyType, days, times));
    }

    /// <summary>
    /// Maps 7 (legacy Sunday) to 0, removes duplicates and sorts
    /// </summary>
    public static int[] NormalizeDays(IEnumerable<int> days)
        => days.Select(d => d == 7 ? 0 : d).Distinct().OrderBy(d => d).ToArray();

    public static HabitDto ToDto(this Habit habit) => new(
        habit.Id,
        habit.Name,
        habit.Description,
        habit.Weight,
        habit.PartialWeight,
        habit.FrequencyType,
        habit.FrequencyDays,
        habit.FrequencyTimes,
        habit.IsActive,
        habit.CreatedAt,
        habit.ArchivedAt
    );
}
