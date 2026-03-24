using Microsoft.AspNetCore.Http;

namespace BookShelf.Api.Dtos;

public class BookUpsertRequest
{
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateOnly? PublishedOn { get; set; }
    public IFormFile? PdfFile { get; set; }
    public IFormFile? AudioFile { get; set; }
    public bool RemovePdfFile { get; set; }
    public bool RemoveAudioFile { get; set; }
}

public record BookDto(
    Guid Id,
    string Title,
    string Author,
    string Description,
    string Category,
    DateOnly? PublishedOn,
    string? PdfFileUrl,
    string? AudioFileUrl,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string UploadedById,
    string UploadedByName);
