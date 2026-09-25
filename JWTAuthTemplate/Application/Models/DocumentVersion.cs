namespace JWTAuthTemplate.Application.Models
{
    public class DocumentVersion
    {
        public int Id { get; set; }

        /// <summary>
        /// ID родительского документа
        /// </summary>
        public int DocumentId { get; set; }

        /// <summary>
        /// Номер версии (1, 2, 3...) — инкрементируется для каждого документа
        /// </summary>
        public int VersionNumber { get; set; }

        /// <summary>
        /// Путь к объекту в MinIO (например, "{userId}/{documentId}/v1_report.xlsx")
        /// </summary>
        public string MinioObjectName { get; set; } = string.Empty;

        /// <summary>
        /// ETag из MinIO — используется для проверки целостности
        /// </summary>
        public string ETag { get; set; } = string.Empty;

        /// <summary>
        /// Размер файла в байтах
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Оригинальное имя файла на момент загрузки этой версии
        /// </summary>
        public string OriginalFileName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Навигационные свойства
        public virtual Document Document { get; set; } = null!;
    }
}
