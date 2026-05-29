using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tourbooking.Models;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tour> Tours { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<TourReview> TourReviews { get; set; }
    

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Tour>()
            .Property(t => t.Price)
            .HasPrecision(18, 2);
        builder.Entity<Booking>()
            .Property(b => b.TotalPrice)
            .HasPrecision(18, 2);
        builder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(18, 2);
        builder.Entity<Payment>()
            .HasOne(p => p.Booking)
            .WithMany(b => b.Payments)
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<TourReview>()
            .HasIndex(r => r.BookingId)
            .IsUnique();

        builder.Entity<TourReview>()
            .HasOne(r => r.Booking)
            .WithOne()
            .HasForeignKey<TourReview>(r => r.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TourReview>()
            .HasOne(r => r.Tour)
            .WithMany()
            .HasForeignKey(r => r.TourId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.Entity<TourReview>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        
    }
}