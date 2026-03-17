using Microsoft.EntityFrameworkCore;
using SecretShareApi.Entities;
using System;
using System.Collections.Generic;

public class SebasDbContext : DbContext
{
    public SebasDbContext(DbContextOptions<SebasDbContext> options) : base(options) { }

    public SebasDbContext() { }


    public DbSet<SharedSecret> SharedSecrets { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SebasDbContext).Assembly);
    }

}