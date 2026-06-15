using BolaoCopa2026.Models;
using Microsoft.EntityFrameworkCore;

namespace BolaoCopa2026.Data;

public sealed class BolaoDbContext : DbContext
{
    public BolaoDbContext(DbContextOptions<BolaoDbContext> options)
        : base(options)
    {
    }

    public DbSet<PredictionRound> Rounds => Set<PredictionRound>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
    public DbSet<RoundSubmission> RoundSubmissions => Set<RoundSubmission>();
    public DbSet<SpecialPrediction> SpecialPredictions => Set<SpecialPrediction>();
    public DbSet<ResultAudit> ResultAudits => Set<ResultAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PredictionRound>(entity =>
        {
            entity.HasKey(round => round.Id);
            entity.Property(round => round.Name).HasMaxLength(120);
        });

        modelBuilder.Entity<Match>(entity =>
        {
            entity.HasKey(match => match.Id);
            entity.HasIndex(match => match.OfficialNumber).IsUnique();
            entity.Property(match => match.GroupName).HasMaxLength(40);
            entity.Property(match => match.Venue).HasMaxLength(120);

            entity.OwnsOne(match => match.HomeTeam, team =>
            {
                team.Property(item => item.Code).HasColumnName("HomeTeamCode").HasMaxLength(20);
                team.Property(item => item.Name).HasColumnName("HomeTeamName").HasMaxLength(80);
                team.Property(item => item.IsBrazil).HasColumnName("HomeTeamIsBrazil");
            });

            entity.OwnsOne(match => match.AwayTeam, team =>
            {
                team.Property(item => item.Code).HasColumnName("AwayTeamCode").HasMaxLength(20);
                team.Property(item => item.Name).HasColumnName("AwayTeamName").HasMaxLength(80);
                team.Property(item => item.IsBrazil).HasColumnName("AwayTeamIsBrazil");
            });

            entity.OwnsOne(match => match.Result, result =>
            {
                result.Property(item => item.QualifiedTeamCode).HasColumnName("ResultQualifiedTeamCode").HasMaxLength(20);
                result.Property(item => item.RegisteredBy).HasColumnName("ResultRegisteredBy").HasMaxLength(80);
            });
        });

        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasKey(participant => participant.Id);
            entity.HasIndex(participant => participant.Login).IsUnique();
            entity.HasIndex(participant => participant.Email).IsUnique();
            entity.Property(participant => participant.Name).HasMaxLength(100);
            entity.Property(participant => participant.Email).HasMaxLength(180);
            entity.Property(participant => participant.Login).HasMaxLength(80);
            entity.Property(participant => participant.PasswordHash).HasMaxLength(500);
            entity.Property(participant => participant.AvatarKey).HasMaxLength(40);
        });

        modelBuilder.Entity<Prediction>(entity =>
        {
            entity.HasKey(prediction => new { prediction.ParticipantId, prediction.MatchId });
            entity.HasIndex(prediction => prediction.MatchId);
            entity.Property(prediction => prediction.QualifiedTeamCode).HasMaxLength(20);
            entity.Ignore(prediction => prediction.IsFinal);
        });

        modelBuilder.Entity<RoundSubmission>(entity =>
        {
            entity.HasKey(submission => submission.Id);
            entity.HasIndex(submission => new { submission.ParticipantId, submission.RoundId }).IsUnique();
            entity.Property(submission => submission.AuditProofHash).HasMaxLength(128);
        });

        modelBuilder.Entity<SpecialPrediction>(entity =>
        {
            entity.HasKey(prediction => prediction.ParticipantId);
            entity.Property(prediction => prediction.Champion).HasMaxLength(80);
            entity.Property(prediction => prediction.RunnerUp).HasMaxLength(80);
            entity.Property(prediction => prediction.TopScorer).HasMaxLength(100);
            entity.Property(prediction => prediction.GoldenBall).HasMaxLength(100);
            entity.Property(prediction => prediction.AuditProofHash).HasMaxLength(128);
            entity.Ignore(prediction => prediction.IsFinal);
        });

        modelBuilder.Entity<ResultAudit>(entity =>
        {
            entity.HasKey(audit => audit.Id);
            entity.Property(audit => audit.RegisteredBy).HasMaxLength(80);
            entity.Property(audit => audit.Summary).HasMaxLength(240);
        });
    }
}
