using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mostawsef.Domain.Entities;

namespace Mostawsef.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Number).HasMaxLength(Order.NumberMaxLength).IsRequired();
        builder.Property(o => o.PatientName).HasMaxLength(Patient.FullNameMaxLength).IsRequired();
        builder.Property(o => o.PatientSearchName).HasMaxLength(Patient.FullNameMaxLength).IsRequired();
        builder.Property(o => o.PatientPhone).HasMaxLength(Patient.PhoneMaxLength).IsRequired();
        builder.Property(o => o.SearchText).HasMaxLength(Order.SearchTextMaxLength).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.DiscountType).HasConversion<string>().HasMaxLength(10);
        builder.Property(o => o.DiscountValue).IsRequired();
        builder.Property(o => o.Total).IsRequired();
        builder.Property(o => o.TestsCount).IsRequired();
        builder.Property(o => o.Paid).IsRequired();
        builder.Property(o => o.Currency).HasMaxLength(Order.CurrencyMaxLength).IsRequired();
        builder.Property(o => o.Notes).HasMaxLength(Order.NotesMaxLength);

        // Only the id: the snapshot columns cover display, and a deleted patient's orders stay readable.
        builder.HasOne<Patient>().WithMany().HasForeignKey(o => o.PatientId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(o => o.Payments).WithOne().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(o => o.Items).HasField("_items");
        builder.Navigation(o => o.Payments).HasField("_payments");

        builder.HasIndex(o => o.Number).IsUnique();
        builder.HasIndex(o => new { o.PatientId, o.Status });
        builder.HasIndex(o => new { o.Status, o.CreatedAt });
        builder.HasIndex(o => o.PatientSearchName);
        builder.HasIndex(o => new { o.CreatedAt, o.Id });
        // Index-only PostgreSQL exception (SQLite ignores it): substring search stays plain Contains/LIKE.
        builder.HasIndex(o => o.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops");
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.LabTestCode).HasMaxLength(LabTest.CodeMaxLength).IsRequired();
        builder.Property(i => i.LabTestName).HasMaxLength(LabTest.NameMaxLength).IsRequired();
        builder.Property(i => i.LabPrice).IsRequired();

        builder.HasOne(i => i.LabTest).WithMany().HasForeignKey(i => i.LabTestId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.LabTestId);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Currency).HasMaxLength(Order.CurrencyMaxLength).IsRequired();
        builder.Property(p => p.Amount).IsRequired();
        builder.Property(p => p.ExchangeRate).HasPrecision(18, 6).IsRequired();
        builder.Property(p => p.PaidAt).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(Payment.NotesMaxLength);

        builder.HasIndex(p => p.OrderId);
        builder.HasIndex(p => p.PaidAt);
    }
}
