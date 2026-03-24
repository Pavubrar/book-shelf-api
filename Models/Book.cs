namespace BookShelf.Api.Models;

public class Book
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public string? PdfFilePath { get; set; }
    public string? AudioFilePath { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UploadedById { get; set; } = string.Empty;
    public AppUser? UploadedBy { get; set; }
}
