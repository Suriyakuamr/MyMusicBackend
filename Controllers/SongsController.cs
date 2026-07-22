using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MusicPlatform.API.Data;
using MusicPlatform.API.Models;
using MusicPlatform.API.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MusicPlatform.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SongsController : ControllerBase
    {
        private readonly MusicDbContext _context;
        private readonly IR2StorageService _storageService;

        public SongsController(MusicDbContext context, IR2StorageService storageService)
        {
            _context = context;
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var songs = await _context.Songs
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
            return Ok(songs);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
            {
                return NotFound();
            }
            return Ok(song);
        }

        [HttpPost]
        [DisableRequestSizeLimit] // Allow large files
        public async Task<IActionResult> Upload([FromForm] string title, [FromForm] string artist, [FromForm] string? album, [FromForm] double? duration, [FromForm] string? artworkUri, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No audio file provided.");
            }

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(artist))
            {
                return BadRequest("Title and Artist are required fields.");
            }

            try
            {
                // Upload file to R2 storage
                using var stream = file.OpenReadStream();
                var fileKey = await _storageService.UploadFileAsync(stream, file.FileName, file.ContentType);

                // Create Song database record
                var song = new Song
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    Artist = artist,
                    Album = album ?? string.Empty,
                    FileKey = fileKey,
                    ContentType = file.ContentType,
                    Duration = duration ?? 0,
                    FileSize = file.Length,
                    ArtworkUri = artworkUri,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Songs.Add(song);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetById), new { id = song.Id }, song);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        [HttpGet("{id}/stream")]
        public async Task<IActionResult> Stream(Guid id)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
            {
                return NotFound();
            }

            try
            {
                var (stream, contentType, contentLength) = await _storageService.GetFileStreamAsync(song.FileKey);
                
                // Copy S3 network stream to MemoryStream to make it seekable.
                // ASP.NET Core's FileStreamResult requires a seekable stream to support HTTP Range requests (seeking).
                var memoryStream = new MemoryStream();
                using (stream)
                {
                    await stream.CopyToAsync(memoryStream);
                }
                memoryStream.Position = 0;

                return File(memoryStream, contentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error streaming audio: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var song = await _context.Songs.FindAsync(id);
            if (song == null)
            {
                return NotFound();
            }

            try
            {
                await _storageService.DeleteFileAsync(song.FileKey);
                _context.Songs.Remove(song);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting song: {ex.Message}");
            }
        }
    }
}
