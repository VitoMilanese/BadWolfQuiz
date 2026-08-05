using BadWolfQuiz.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace BadWolfQuiz.Web.Migrations.Archive;

[DbContext(typeof(ArchiveDbContext))]
public partial class ArchiveDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.29");
        modelBuilder.Entity("BadWolfQuiz.Web.Models.ArchivedQuizMedia", b =>
        {
            b.Property<long>("Id").ValueGeneratedOnAdd().HasColumnType("INTEGER");
            b.Property<DateTime>("ArchivedAtUtc").HasColumnType("TEXT");
            b.Property<string>("ContentType").HasMaxLength(256).HasColumnType("TEXT");
            b.Property<byte[]>("Data").IsRequired().HasColumnType("BLOB");
            b.Property<int>("EntityId").HasColumnType("INTEGER");
            b.Property<string>("HostId").IsRequired().HasMaxLength(36).HasColumnType("TEXT");
            b.Property<long>("Length").HasColumnType("INTEGER");
            b.Property<Guid>("OperationId").HasColumnType("TEXT");
            b.Property<string>("OriginalFileName").HasMaxLength(512).HasColumnType("TEXT");
            b.Property<int>("QuizId").HasColumnType("INTEGER");
            b.Property<int>("Role").HasColumnType("INTEGER");
            b.Property<string>("Sha256").IsRequired().HasMaxLength(64).HasColumnType("TEXT");
            b.HasKey("Id"); b.HasIndex("HostId"); b.HasIndex("OperationId"); b.HasIndex("QuizId"); b.HasIndex("QuizId", "HostId");
            b.HasIndex("OperationId", "EntityId", "Role").IsUnique(); b.ToTable("ArchivedQuizMedia");
        });
        modelBuilder.Entity("BadWolfQuiz.Web.Models.ArchiveOperation", b =>
        {
            b.Property<Guid>("Id").HasColumnType("TEXT"); b.Property<DateTime?>("CompletedAtUtc").HasColumnType("TEXT");
            b.Property<DateTime>("CreatedAtUtc").HasColumnType("TEXT"); b.Property<string>("FailureReason").HasMaxLength(1000).HasColumnType("TEXT");
            b.Property<string>("HostId").IsRequired().HasMaxLength(36).HasColumnType("TEXT"); b.Property<long>("MediaBytes").HasColumnType("INTEGER");
            b.Property<int>("MediaCount").HasColumnType("INTEGER"); b.Property<DateTime?>("OrphanedAtUtc").HasColumnType("TEXT");
            b.Property<int>("QuizId").HasColumnType("INTEGER"); b.Property<DateTime?>("RestoredAtUtc").HasColumnType("TEXT"); b.Property<int>("State").HasColumnType("INTEGER");
            b.HasKey("Id"); b.HasIndex("QuizId", "HostId"); b.ToTable("ArchiveOperations");
        });
    }
}
