using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain.Fulfillments;
using static Orders.Domain.Fulfillments.FulfillmentStatus;

namespace Orders.Infrastructure.Data.EntityConfigurations;

/// <summary>
/// Stores the <see cref="FulfillmentStatus"/> union in one text column: a one-letter prefix for the variant followed
/// by its values, so a query can filter on the variant with <c>LIKE</c>.
/// </summary>
public sealed class FulfillmentConfiguration : IEntityTypeConfiguration<Fulfillment>
{
    public void Configure(EntityTypeBuilder<Fulfillment> builder)
    {
        builder.HasKey(fulfillment => fulfillment.Id);
        builder.Property(fulfillment => fulfillment.Id)
            .HasConversion(id => id.Value, value => new FulfillmentId(value))
            .ValueGeneratedNever();

        builder.Property(fulfillment => fulfillment.Status)
            .HasConversion(status => StatusToString(status), text => StringToStatus(text));

        builder.HasIndex(fulfillment => fulfillment.OrderId);
    }

    private static string StatusToString(FulfillmentStatus status) => status switch
    {
        InProgressStatus inProgress => $"I {inProgress.ProgressTime:O}",
        FailedStatus failed => $"F {failed.FailedTime:O} {failed.ErrorMessage}",
        CompletedStatus completed => $"C {completed.CompletedTime:O} {completed.ScanCount}",
        _ => throw new ArgumentException("Unknown status type", nameof(status)),
    };

    private static FulfillmentStatus StringToStatus(string text) => text.Split(' ', 3) switch
    {
        ["I", var time] => FulfillmentStatus.InProgress(DateTimeOffset.Parse(time, CultureInfo.InvariantCulture)),
        ["C", var time, var scanCount] => FulfillmentStatus.Completed(DateTimeOffset.Parse(time, CultureInfo.InvariantCulture), int.Parse(scanCount, CultureInfo.InvariantCulture)),
        ["F", var time, var errorMessage] => FulfillmentStatus.Failed(DateTimeOffset.Parse(time, CultureInfo.InvariantCulture), errorMessage),
        _ => throw new ArgumentException("Unknown status string", nameof(text)),
    };
}
