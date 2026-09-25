using JWTAuthTemplate.Models.Identity;  // Протестить
using System;

namespace JWTAuthTemplate.Application.Models;

public class Document
{
    public int Id { get; set; }
    
    /// <summary>
    /// ID пользователя-владельца документа (FK на ApplicationUser)
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    
    /// <summary>
    /// Оригинальное имя файла, которое видит пользователь (например, "report.xlsx")
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;
    
    /// <summary>
    /// Расширение файла без точки (например, "xlsx", "spc")
    /// </summary>
    public string FileExtension { get; set; } = string.Empty;
    
    /// <summary>
    /// ID текущей активной версии (последней загруженной)
    /// </summary>
    public int? CurrentVersionId { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Навигационные свойства
    public virtual ApplicationUser? User { get; set; }
    public virtual ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public virtual DocumentVersion? CurrentVersion { get; set; }
}
