namespace UmbracoAngularCMS.Models
{
    public class ContentVersion
    {
        public int Id { get; set; }
        public int ContentId { get; set; }
        public int VersionNumber { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public string Priority { get; set; }
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public string ChangeNotes { get; set; }
    }
}
