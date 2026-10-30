using JWTAuthTemplate.Application.Interfaces;
using JWTAuthTemplate.Infrastructure.Database;
using JWTAuthTemplate.Models.Identity;
using Microsoft.AspNetCore.Mvc;
using JWTAuthTemplate.Shared.Dtos;
using JWTAuthTemplate.DTO.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JWTAuthTemplate.Application.Services
{
    public class SessionService: ISessionService
    {
        private readonly Context _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SessionService(Context context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpContextAccessor = httpContextAccessor;
        }


        public async Task<int> SaveStatusAsync(Dictionary<string, object> statusParams)
        {
            if (statusParams == null || !statusParams.Any())
                throw new ArgumentException("Status parameters cannot be empty", nameof(statusParams));

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            try
            {
                var record = new UserSessionStatus
                {
                    UserId = userId,
                    ActualAt = DateTime.UtcNow,
                    StatusParamsDict = statusParams,
                    FileName = statusParams.GetValueOrDefault("fileName")?.ToString() ?? "",
                    FileExtension = statusParams.GetValueOrDefault("fileExtension")?.ToString() ?? ""
                };

                _context.UserSessionStatuses.Add(record);
                await _context.SaveChangesAsync();

                return record.Id;
            }
            catch (DbUpdateException dbEx)
            {
                throw new InvalidOperationException("Failed to save session status to database", dbEx);
            }
        }


        public async Task<UserSessionStatusDTO?> GetLatestUserSessionStatusAsync()
        {
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return await _context.UserSessionStatuses
                .Where(u => u.UserId == userId)
                .OrderByDescending(u => u.ActualAt)
                .Select(u => new UserSessionStatusDTO
                {
                    Id = u.Id,
                    UserId = u.UserId,
                    ActualAt = u.ActualAt,
                    StatusParamsDict = u.StatusParamsDict
                })
                .FirstOrDefaultAsync();
        }


        public async Task<IEnumerable<UserSessionStatusDTO>> GetAllStatusesByFileNameAsync(string fileName, string fileExtension)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or whitespace", nameof(fileName));
            
            if (string.IsNullOrWhiteSpace(fileExtension))
                throw new ArgumentException("File extension cannot be null or whitespace", nameof(fileExtension));

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var statuses = await _context.UserSessionStatuses
                .Where(u => u.UserId == userId)
                .OrderByDescending(u => u.ActualAt)
                .Select(u => new UserSessionStatusDTO
                {
                    Id = u.Id,
                    UserId = u.UserId,
                    ActualAt = u.ActualAt,
                    StatusParamsDict = u.StatusParamsDict
                })
                .ToListAsync();

            return statuses
                .Where(u => u.StatusParamsDict != null &&
                            u.StatusParamsDict.TryGetValue("fileName", out var fileNameValue) &&
                            fileNameValue?.ToString() == fileName &&
u.StatusParamsDict.TryGetValue("fileExtension", out var fileExtensionValue) &&
                            fileExtensionValue?.ToString() == fileExtension);
            
        }


        public async Task<UserSessionStatusDTO?> GetLatestStatusByFileNameAsync(string fileName, string fileExtension)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or whitespace", nameof(fileName));
            
            if (string.IsNullOrWhiteSpace(fileExtension))
                throw new ArgumentException("File extension cannot be null or whitespace", nameof(fileExtension));

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var allStatuses = await _context.UserSessionStatuses
                .Where(u => u.UserId == userId)
                .OrderByDescending(u => u.ActualAt)
                .Select(u => new UserSessionStatusDTO
                {
                    Id = u.Id,
                    UserId = u.UserId,
                    ActualAt = u.ActualAt,
                    StatusParamsDict = u.StatusParamsDict
                })
                .ToListAsync();

            return allStatuses
                .FirstOrDefault(u => u.StatusParamsDict != null &&
                                     u.StatusParamsDict.TryGetValue("fileName", out var fileNameValue) &&
                                     fileNameValue?.ToString() == fileName &&
         u.StatusParamsDict.TryGetValue("fileExtension", out var fileExtensionValue) &&
                            fileExtensionValue?.ToString() == fileExtension);
        }


        public async Task<UserSessionStatusDTO?> GetLatestStatusByFileNameAndTimeAsync(
            string fileName, 
            string fileExtension,
            DateTime asOfTime)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or whitespace", nameof(fileName));
            
            if (string.IsNullOrWhiteSpace(fileExtension))
                throw new ArgumentException("File extension cannot be null or whitespace", nameof(fileExtension));

            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            var allStatuses = await _context.UserSessionStatuses
                .Where(u => u.UserId == userId)
                .Where(u => u.ActualAt <= asOfTime)
                .OrderByDescending(u => u.ActualAt)
                .Select(u => new UserSessionStatusDTO
                {
                    Id = u.Id,
                    UserId = u.UserId,
                    ActualAt = u.ActualAt,
                    StatusParamsDict = u.StatusParamsDict
                })
                .ToListAsync();

            return allStatuses
                .FirstOrDefault(u => u.StatusParamsDict != null &&
                                     u.StatusParamsDict.TryGetValue("fileName", out var fileNameValue) &&
                                     fileNameValue?.ToString() == fileName &&
         u.StatusParamsDict.TryGetValue("fileExtension", out var fileExtensionValue) &&
                            fileExtensionValue?.ToString() == fileExtension);
        }
    }
}
