namespace UmbracoAngularCMS.Models
{
    public class ApprovalWorkflow
    {
        public string Name { get; set; }
        public List<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();
        public bool RequiresAllApprovals { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; }
    }

    public class ApprovalStep
    {
        public int Order { get; set; }
        public string ApproverRole { get; set; }
        public List<string> RequiredApproverGroups { get; set; } = new List<string>();
        public bool IsApproved { get; set; }
        public string ApprovedBy { get; set; }
        public string ApproverEmail { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string Comments { get; set; }
        public string Action { get; set; } // "Approved" or "Rejected"
    }

    public class ContentItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string ImageUrl { get; set; }
        public string Category { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime UpdateDate { get; set; }
    }

    public class DropdownOptionDto
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Category { get; set; }
        public int SortOrder1 { get; set; }
    }
}