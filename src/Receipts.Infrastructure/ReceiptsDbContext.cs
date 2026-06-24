using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Receipts.Core;

namespace Receipts.Infrastructure;

public class ReceiptsDbContext(DbContextOptions<ReceiptsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.HasKey(u => u.Id);
        user.HasIndex(u => u.Email).IsUnique();
        user.Property(u => u.Email).IsRequired();
        user.Property(u => u.PasswordHash).IsRequired();

        // Group's collections are stored as JSON text columns (provider-agnostic).
        var stringList = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new());

        var stringListComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v == null ? 0 : v.Aggregate(0, (acc, s) => HashCode.Combine(acc, s.GetHashCode())),
            v => v.ToList());

        var expenseList = new ValueConverter<List<Expense>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => JsonSerializer.Deserialize<List<Expense>>(v, (JsonSerializerOptions?)null) ?? new());

        var expenseListComparer = new ValueComparer<List<Expense>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            v => v == null ? 0 : v.Aggregate(0, (acc, e) => HashCode.Combine(acc, e.GetHashCode())),
            v => v.ToList());

        var group = modelBuilder.Entity<Group>();
        group.HasKey(g => g.Id);
        group.Property(g => g.Name).IsRequired();
        group.Property(g => g.MemberEmails).HasConversion(stringList, stringListComparer);
        group.Property(g => g.Expenses).HasConversion(expenseList, expenseListComparer);
    }
}
