using BookShelf.Api.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Api.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Book>()
            .Property(book => book.Title)
            .HasMaxLength(200);

        builder.Entity<Book>()
            .Property(book => book.Author)
            .HasMaxLength(150);

        builder.Entity<Book>()
            .HasOne(book => book.UploadedBy)
            .WithMany()
            .HasForeignKey(book => book.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
