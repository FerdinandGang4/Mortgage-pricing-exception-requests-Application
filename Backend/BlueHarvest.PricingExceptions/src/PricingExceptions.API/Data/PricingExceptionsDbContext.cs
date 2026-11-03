using Microsoft.EntityFrameworkCore;
using PricingExceptions.API.Models.Entities;
using PricingExceptions.API.Exceptions;

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

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectHistoricalRecords();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ProtectHistoricalRecords();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectHistoricalRecords()
    {
        if (ChangeTracker.Entries().Any(entry =>
            entry.State is EntityState.Modified or EntityState.Deleted &&
            entry.Entity is RequestRevision or ReviewDecision or HistoryEntry or IdempotencyRecord))
            throw new BusinessRuleException("Submitted revisions, decisions, history and completed retry records cannot be changed or deleted.");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RequestRevision>()
            .HasKey(x => new { x.RequestId, x.RevisionNumber });

        modelBuilder.Entity<PricingExceptionRequest>(request =>
        {
            request.Property(x => x.Version).IsConcurrencyToken();
            request.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId);
            request.HasOne<User>().WithMany().HasForeignKey(x => x.AssignedReviewerId);
            request.Property(x => x.RequesterName).HasMaxLength(200);
            request.Property(x => x.AssignedReviewerName).HasMaxLength(200);
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
