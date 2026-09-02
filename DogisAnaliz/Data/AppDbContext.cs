using DogisAnaliz.Models;
using Microsoft.EntityFrameworkCore;

namespace DogisAnaliz.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<League> Leagues => Set<League>();
    public DbSet<Season> Seasons => Set<Season>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSurprise> MatchSurprises => Set<MatchSurprise>();
    public DbSet<MatchDetail> MatchDetails => Set<MatchDetail>();
    public DbSet<DogiPattern> DogiPatterns => Set<DogiPattern>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Foreign Key Çakışma Engelleyicileri (Home & Away)
        modelBuilder.Entity<Match>()
            .HasOne(m => m.HomeTeam)
            .WithMany()
            .HasForeignKey(m => m.HomeTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.AwayTeam)
            .WithMany()
            .HasForeignKey(m => m.AwayTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        // 1:1 İlişki Tanımlamaları
        modelBuilder.Entity<MatchSurprise>()
            .HasKey(ms => ms.MatchId);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.Surprise)
            .WithOne(s => s.Match)
            .HasForeignKey<MatchSurprise>(s => s.MatchId);

        modelBuilder.Entity<MatchDetail>()
            .HasKey(md => md.MatchId);

        modelBuilder.Entity<Match>()
            .HasOne(m => m.Detail)
            .WithOne(d => d.Match)
            .HasForeignKey<MatchDetail>(d => d.MatchId);

        // PostgreSQL JSONB Kolon Tipi
        modelBuilder.Entity<MatchDetail>()
            .Property(md => md.AdditionalDataJson)
            .HasColumnType("jsonb");

        // 🚀 SÜRPRİZ İNDEKLERİ (Performans İndeksleri)
        modelBuilder.Entity<MatchSurprise>()
            .HasIndex(ms => ms.IsTurnaround);

        modelBuilder.Entity<MatchSurprise>()
            .HasIndex(ms => ms.IsHighGoal);

        modelBuilder.Entity<MatchSurprise>()
            .HasIndex(ms => ms.SurpriseType);

        // DogiPattern FK konfigürasyonları (cascade çakışmasını önlemek için)
        modelBuilder.Entity<DogiPattern>()
            .HasOne(d => d.TriggerMatch1)
            .WithMany()
            .HasForeignKey(d => d.TriggerMatch1Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DogiPattern>()
            .HasOne(d => d.TriggerMatch2)
            .WithMany()
            .HasForeignKey(d => d.TriggerMatch2Id)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DogiPattern>()
            .HasOne(d => d.AlertMatch)
            .WithMany()
            .HasForeignKey(d => d.AlertMatchId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DogiPattern>()
            .HasOne(d => d.Team)
            .WithMany()
            .HasForeignKey(d => d.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DogiPattern>()
            .HasOne(d => d.League)
            .WithMany()
            .HasForeignKey(d => d.LeagueId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DogiPattern>()
            .HasOne(d => d.Season)
            .WithMany()
            .HasForeignKey(d => d.SeasonId)
            .OnDelete(DeleteBehavior.Restrict);

        // Aynı takım için aynı iki tetikleyici maç birden fazla kaydedilmesin
        modelBuilder.Entity<DogiPattern>()
            .HasIndex(d => new { d.TriggerMatch1Id, d.TriggerMatch2Id, d.TeamId })
            .IsUnique();

        modelBuilder.Entity<DogiPattern>()
            .HasIndex(d => d.AlertResult);
    }
}