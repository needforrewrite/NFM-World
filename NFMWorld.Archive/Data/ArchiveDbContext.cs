using Microsoft.EntityFrameworkCore;

namespace NFMWorld.Archive.Data;

/// <summary>
/// The archive's database. SQLite in WAL mode; uploaded files live on disk and this only
/// indexes them, so a lost database can be rebuilt by rescanning the storage root.
/// </summary>
public sealed class ArchiveDbContext(DbContextOptions<ArchiveDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<OauthPending> OauthPending => Set<OauthPending>();
    public DbSet<AuthAudit> AuthAudit => Set<AuthAudit>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Clan> Clans => Set<Clan>();
    public DbSet<ClanMember> ClanMembers => Set<ClanMember>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchParticipant> MatchParticipants => Set<MatchParticipant>();
    public DbSet<TimeTrial> TimeTrials => Set<TimeTrial>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<User>(e =>
        {
            // Case-insensitive uniqueness is enforced with a normalised column, because SQLite's
            // NOCASE only folds ASCII and usernames may not be.
            e.HasIndex(u => u.UsernameNormalized).IsUnique();
            e.Property(u => u.Username).HasMaxLength(32);
            e.Property(u => u.UsernameNormalized).HasMaxLength(32);
        });

        model.Entity<Session>(e =>
        {
            e.HasIndex(s => s.TokenHash).IsUnique();
            e.HasOne(s => s.User).WithMany(u => u.Sessions).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<ExternalLogin>(e =>
        {
            e.HasIndex(l => new { l.Provider, l.ProviderUserId }).IsUnique();
            e.HasOne(l => l.User).WithMany(u => u.ExternalLogins).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<AuthAudit>(e =>
        {
            e.HasIndex(a => a.Timestamp);
        });

        model.Entity<Asset>(e =>
        {
            // Content-addressed: one row per distinct file, however many names point at it.
            e.HasIndex(a => a.ContentHash);
            e.HasIndex(a => new { a.Type, a.OwnerId });
            e.HasOne(a => a.Owner).WithMany().OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<Clan>(e =>
        {
            e.HasIndex(c => c.NameNormalized).IsUnique();
            e.Property(c => c.Name).HasMaxLength(32);
            e.Property(c => c.NameNormalized).HasMaxLength(32);
            e.Property(c => c.Tag).HasMaxLength(6);
        });

        model.Entity<ClanMember>(e =>
        {
            // One clan per user for now; relaxing this is a schema change, not a behaviour flag,
            // because membership is what rosters and clan ratings are keyed on.
            e.HasIndex(m => m.UserId).IsUnique();
            e.HasOne(m => m.Clan).WithMany(c => c.Members).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User).WithMany().OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Rating>(e =>
        {
            // One rating row per user per gamemode: rank is per gamemode, not global.
            e.HasIndex(r => new { r.UserId, r.Gamemode }).IsUnique();
            e.HasOne(r => r.User).WithMany(u => u.Ratings).OnDelete(DeleteBehavior.Cascade);
        });

        model.Entity<Match>(e =>
        {
            // The lobby's MatchKey is the idempotency key: a retried report must not rate twice.
            e.HasIndex(m => m.MatchKey).IsUnique();
            e.HasIndex(m => m.ResultServerId);
        });

        model.Entity<MatchParticipant>(e =>
        {
            e.HasIndex(p => new { p.MatchId, p.UserId });
            e.HasOne(p => p.Match).WithMany(m => m.Participants).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User).WithMany().OnDelete(DeleteBehavior.SetNull);
        });

        model.Entity<TimeTrial>(e =>
        {
            e.HasIndex(t => new { t.UserId, t.StageHash, t.CarHash }).IsUnique();
            e.HasIndex(t => new { t.StageHash, t.CarHash, t.TotalTicks });
            e.HasOne(t => t.User).WithMany(u => u.TimeTrials).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
