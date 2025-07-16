using System;
using System.Collections.Generic;
using System.Linq;

namespace UmbracoAngularCMS.Models
{
    // Keep the old models for backward compatibility
    public class ApprovalWorkflow
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Old property for backward compatibility
        public List<ApprovalStep> Steps { get; set; } = new List<ApprovalStep>();

        // New flexible requirements system
        public List<ApprovalRequirement> Requirements { get; set; } = new List<ApprovalRequirement>();

        public int MinimumApprovals { get; set; } = 1;
        public bool RequiresAllApprovals { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; }

        // Helper property to check if using new or old system
        public bool IsUsingRequirements => Requirements != null && Requirements.Any();
    }

    // Keep old ApprovalStep for backward compatibility
    public class ApprovalStep
    {
        public int Order { get; set; }
        public string ApproverRole { get; set; }
        public string ApproverEmail { get; set; }
        public List<string> RequiredApproverGroups { get; set; } = new List<string>();
        public bool IsApproved { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public string Comments { get; set; }
        public string Action { get; set; }
    }

    // New flexible approval requirement
    public class ApprovalRequirement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RequirementName { get; set; } // This was missing!
        public ApprovalType Type { get; set; }
        public List<string> AssignedTo { get; set; } = new List<string>();
        public List<ApprovalAction> Actions { get; set; } = new List<ApprovalAction>();

        // Helper property
        public bool IsCompleted => Actions.Any(a => a.Action == "Approved");
    }

    public enum ApprovalType
    {
        SpecificPerson,
        AnyFromGroup,
        AllFromGroup
    }

    public class ApprovalAction
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string Action { get; set; } // Approved/Rejected
        public DateTime ActionDate { get; set; }
        public string Comments { get; set; }
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

    public class ApprovalRequest
    {
        public string ApprovedBy { get; set; }
        public int ApprovedById { get; set; }
        public string ApprovedByEmail { get; set; }
        public string RejectedBy { get; set; }
        public int RejectedById { get; set; }
        public string RejectedByEmail { get; set; }
        public string Comments { get; set; }
    }
}