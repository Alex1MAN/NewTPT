using JWTAuthTemplate.Application.Models;
using JWTAuthTemplate.Models.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JWTAuthTemplate.Infrastructure.Database
{
    public class Context : IdentityDbContext<ApplicationUser, ApplicationRole, string, ApplicationUserClaim, ApplicationUserRole, ApplicationUserLogin, ApplicationRoleClaim, ApplicationUserToken>
    {
        public Context(DbContextOptions<Context> options) : base(options) { }

        // Старая таблица — пока оставляем для миграции данных
        public DbSet<UserReferencesInMinio> UserReferencesInMinio { get; set; }

        // Новые таблицы для системы версионности
        public DbSet<Document> Documents { get; set; }
        public DbSet<DocumentVersion> DocumentVersions { get; set; }

        public DbSet<UserSessionStatus> UserSessionStatuses { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // === Существующие настройки (оставляем без изменений) ===
            modelBuilder.Entity<ApplicationUser>(e =>
            {
                e.HasMany(r => r.Roles)
                    .WithOne(u => u.User)
                    .HasForeignKey(u => u.UserId)
                    .IsRequired();
            });

            modelBuilder.Entity<ApplicationRole>(e =>
            {
                e.HasMany(u => u.Users)
                    .WithOne(u => u.Role)
                    .HasForeignKey(u => u.RoleId)
                    .IsRequired();
            });

            modelBuilder.Entity<ApplicationUser>().Navigation(e => e.Roles).AutoInclude();
            modelBuilder.Entity<ApplicationUserRole>().Navigation(e => e.Role).AutoInclude();

            // === Новые настройки для системы версионности ===

            // Document -> ApplicationUser (многие к одному)
            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasOne(d => d.User)
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.CurrentVersion)
                    .WithMany()
                    .HasForeignKey(d => d.CurrentVersionId)
                    .OnDelete(DeleteBehavior.SetNull);

                // Индекс для быстрого поиска документов пользователя
                entity.HasIndex(d => d.UserId);

                // Индекс для быстрого поиска по имени файла
                entity.HasIndex(d => d.OriginalName);
            });

            // DocumentVersion -> Document (многие к одному)
            modelBuilder.Entity<DocumentVersion>(entity =>
            {
                entity.HasOne(v => v.Document)
                    .WithMany(d => d.Versions)
                    .HasForeignKey(v => v.DocumentId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Уникальный индекс: нельзя создать две версии с одинаковым номером для одного документа
                entity.HasIndex(v => new { v.DocumentId, v.VersionNumber })
                    .IsUnique();

                // Индекс для быстрого поиска по пути в MinIO
                entity.HasIndex(v => v.MinioObjectName);
            });
        }
    }
}