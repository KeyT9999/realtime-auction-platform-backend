namespace RealtimeAuction.Api.Dtos
{
    public class CreateCategoryDto
    {
        public string Name { get; set; } = null!;
        public string? ParentId { get; set; }
        public List<CategoryAttributeDto>? Attributes { get; set; }
    }

    public class CategoryAttributeDto
    {
        public string Name { get; set; } = null!;
        public string Type { get; set; } = "text";
        public List<string>? Options { get; set; }
        public bool IsRequired { get; set; }
    }

    public class CategoryDto : CreateCategoryDto
    {
        public string Id { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public List<CategoryDto> Children { get; set; } = new();
    }
}
