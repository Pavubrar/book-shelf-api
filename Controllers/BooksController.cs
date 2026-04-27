using System.Security.Claims;
using BookShelf.Api.Data;
using BookShelf.Api.Dtos;
using BookShelf.Api.Models;
using BookShelf.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookShelf.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BooksController(
    ApplicationDbContext dbContext,
    UserManager<AppUser> userManager,
    FileStorageService fileStorageService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BookDto>>> GetBooks()
    {
        var books = await dbContext.Books
            .Include(book => book.UploadedBy)
            .OrderByDescending(book => book.CreatedAtUtc)
            .ToListAsync();

        return Ok(books.Select(MapBook));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookDto>> GetBook(Guid id)
    {
        var book = await dbContext.Books
            .Include(item => item.UploadedBy)
            .FirstOrDefaultAsync(item => item.Id == id);

        return book is null ? NotFound() : Ok(MapBook(book));
    }

    [HttpPost]
    public async Task<ActionResult<BookDto>> Create([FromForm] BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        if (request.PdfFile is null)
        {
            return BadRequest("A PDF file is required when creating a book.");
        }

        var book = new Book
        {
            Title = request.Title,
            Author = request.Author,
            Description = request.Description,
            Category = request.Category,
            PublishedOn = request.PublishedOn,
            UploadedById = user.Id,
            PdfFilePath = await fileStorageService.SavePdfAsync(request.PdfFile, cancellationToken)
        };

        if (request.AudioFile is not null)
        {
            book.AudioFilePath = await fileStorageService.SaveAudioAsync(request.AudioFile, cancellationToken);
        }

        dbContext.Books.Add(book);
        await dbContext.SaveChangesAsync(cancellationToken);

        book.UploadedBy = user;
        return CreatedAtAction(nameof(GetBook), new { id = book.Id }, MapBook(book));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BookDto>> Update(Guid id, [FromForm] BookUpsertRequest request, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books
            .Include(item => item.UploadedBy)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (book is null)
        {
            return NotFound();
        }

        if (!await CanManageBookAsync(book))
        {
            return Forbid();
        }

        book.Title = request.Title;
        book.Author = request.Author;
        book.Description = request.Description;
        book.Category = request.Category;
        book.PublishedOn = request.PublishedOn;
        book.UpdatedAtUtc = DateTime.UtcNow;

        if (request.RemovePdfFile)
        {
            await fileStorageService.DeleteAsync(book.PdfFilePath, cancellationToken);
            book.PdfFilePath = null;
        }

        if (request.RemoveAudioFile)
        {
            await fileStorageService.DeleteAsync(book.AudioFilePath, cancellationToken);
            book.AudioFilePath = null;
        }

        if (request.PdfFile is not null)
        {
            await fileStorageService.DeleteAsync(book.PdfFilePath, cancellationToken);
            book.PdfFilePath = await fileStorageService.SavePdfAsync(request.PdfFile, cancellationToken);
        }

        if (request.AudioFile is not null)
        {
            await fileStorageService.DeleteAsync(book.AudioFilePath, cancellationToken);
            book.AudioFilePath = await fileStorageService.SaveAudioAsync(request.AudioFile, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(MapBook(book));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var book = await dbContext.Books.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (book is null)
        {
            return NotFound();
        }

        if (!await CanManageBookAsync(book))
        {
            return Forbid();
        }

        await fileStorageService.DeleteAsync(book.PdfFilePath, cancellationToken);
        await fileStorageService.DeleteAsync(book.AudioFilePath, cancellationToken);
        dbContext.Books.Remove(book);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? null : await userManager.FindByIdAsync(userId);
    }

    private async Task<bool> CanManageBookAsync(Book book)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return false;
        }

        return book.UploadedById == user.Id || await userManager.IsInRoleAsync(user, "Admin");
    }

    private static BookDto MapBook(Book book) => new(
        book.Id,
        book.Title,
        book.Author,
        book.Description,
        book.Category,
        book.PublishedOn,
        book.PdfFilePath,
        book.AudioFilePath,
        book.CreatedAtUtc,
        book.UpdatedAtUtc,
        book.UploadedById,
        book.UploadedBy?.DisplayName ?? "Unknown User");
}
