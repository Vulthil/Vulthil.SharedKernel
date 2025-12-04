using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WebApi.Domain.SideEffects;
using static WebApi.Domain.SideEffects.Status;

namespace WebApi.Infrastructure.Data.EntityConfigurations;

public sealed class SideEffectEntityConfiguration : IEntityTypeConfiguration<SideEffect>
{
    public void Configure(EntityTypeBuilder<SideEffect> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status)
            .HasConversion(
                v => StatusToString(v),
                v => StringToStatus(v));

        builder.Property(e => e.Id)
            .HasConversion(
                v => v.Value,
                v => new SideEffectId(v));
    }

    private static string StatusToString(Status v) => v switch
    {
        InProgressStatus inProgress => $"I {inProgress.ProgressTime:O}",
        FailedStatus failed => $"F {failed.FailedTime:O} {failed.ErrorMessage}",
        CompletedStatus completed => $"C {completed.CompletedTime:O} {completed.Value}",
        _ => throw new ArgumentException("Unknown status type", nameof(v))
    };

    private static Status StringToStatus(string v) => v.Split(" ") switch
    {
        [var type, var time] when type == "I" => Status.InProgress(DateTimeOffset.Parse(time, CultureInfo.InvariantCulture)),
        [var type, var time, var value] when type == "C" => Status.Completed(DateTimeOffset.Parse(time, CultureInfo.InvariantCulture), int.Parse(value)),
        [var type, var time, var errorMessage] when type == "F" => Status.Failed(DateTimeOffset.Parse(time, CultureInfo.InvariantCulture), errorMessage),
        _ => throw new ArgumentException("Unknown status string", nameof(v))
    };
}


