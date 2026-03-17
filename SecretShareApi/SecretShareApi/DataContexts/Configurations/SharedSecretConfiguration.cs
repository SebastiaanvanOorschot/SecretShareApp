using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SecretShareApi.Entities;

namespace SecretShareApi.DataContexts.Configurations
{
    public class SharedSecretConfiguration : IEntityTypeConfiguration<SharedSecret>
    {
        public void Configure(EntityTypeBuilder<SharedSecret> builder)
        {
            builder.ToTable("SharedSecret");

            builder.HasKey(e => e.Id);

            // Columns
            builder.Property(e => e.Id)
                .HasColumnName("id");

            builder.Property(e => e.Secret)
                .HasColumnName("secret");

            builder.Property(e => e.Expire)
                .HasColumnName("expire");

            builder.Property(e => e.Token)
                .HasColumnName("token");

            builder.Property(e => e.Urlextension)
                .HasColumnName("urlExtension");

            builder.Property(e => e.Tries)
                .HasColumnName("tries");
        }
    }
}
