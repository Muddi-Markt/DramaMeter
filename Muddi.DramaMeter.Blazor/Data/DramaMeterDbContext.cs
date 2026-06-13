using Microsoft.EntityFrameworkCore;
using Muddi.DramaMeter.Blazor.Models;

namespace Muddi.DramaMeter.Blazor.Data;

public class DramaMeterDbContext(DbContextOptions<DramaMeterDbContext> options) : DbContext(options)
{
	public DbSet<User> Users => Set<User>();
	public DbSet<Vote> Votes => Set<Vote>();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		modelBuilder.Entity<User>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.Id);
			entity.Property(e => e.CreatedAt).HasColumnType("timestamptz");
		});

		modelBuilder.Entity<Vote>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasIndex(e => e.CreatedAt);
		});
	}
}