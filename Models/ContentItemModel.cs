namespace UmbracoAngularCMS.Models
{
    public class ContentItemModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ParagraphContent { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public DateTime CreateDate { get; set; }
    }

    public class CategoryModel
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}