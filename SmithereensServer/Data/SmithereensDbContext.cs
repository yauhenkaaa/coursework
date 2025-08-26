using Microsoft.EntityFrameworkCore;
using SmithereensServer.Models;

namespace SmithereensServer.Data
{
    public class SmithereensDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<ConversationParticipants> ConversationParticipants { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Models.File> Files { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                "Server=NUCLEARGHANDI;Database=SmithereensDB;Integrated Security=True;Encrypt=true;TrustServerCertificate=true;"
            );
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConversationParticipants>()
                .HasKey(cp => new { cp.ConversationID, cp.UserID });
        }
    }
}
