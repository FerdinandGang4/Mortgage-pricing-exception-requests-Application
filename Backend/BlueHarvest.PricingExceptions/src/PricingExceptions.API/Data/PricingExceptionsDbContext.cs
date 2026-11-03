using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Models.Entities;

namespace PricingExceptions.API.Data;

public sealed class PricingExceptionsDbContext(
    DbContextOptions<PricingExceptionsDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PricingExceptionRequest> PricingExceptionRequests => Set<PricingExceptionRequest>();
    public DbSet<RequestRevision> RequestRevisions => Set<RequestRevision>();
    public DbSet<ReviewDecision> ReviewDecisions => Set<ReviewDecision>();
    public DbSet<HistoryEntry> HistoryEntries => Set<HistoryEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RequestRevision>()
            .HasKey(x => new { x.RequestId, x.RevisionNumber });

        modelBuilder.Entity<PricingExceptionRequest>(request =>
        {
            request.Property(x => x.Version).IsConcurrencyToken();
            request.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId);
            request.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.RequestId);
            request.HasMany(x => x.History).WithOne().HasForeignKey(x => x.RequestId);
        });

        modelBuilder.Entity<RequestRevision>()
            .HasOne<User>().WithMany().HasForeignKey(x => x.SubmittedByUserId);

        modelBuilder.Entity<ReviewDecision>(decision =>
        {
            decision.HasOne<RequestRevision>().WithOne()
                .HasForeignKey<ReviewDecision>(x => new { x.RequestId, x.RevisionNumber });
            decision.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId);
        });

        modelBuilder.Entity<HistoryEntry>(history =>
        {
            history.HasOne<RequestRevision>().WithMany()
                .HasForeignKey(x => new { x.RequestId, x.RevisionNumber });
            history.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId);
        });

        modelBuilder.Entity<IdempotencyRecord>(record =>
        {
            record.HasOne<User>().WithMany().HasForeignKey(x => x.ActorUserId);
            record.HasOne<PricingExceptionRequest>().WithMany().HasForeignKey(x => x.RequestId);
        });
    }
}
