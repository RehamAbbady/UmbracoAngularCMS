using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoAngularCMS.Models;
using Microsoft.Extensions.Logging;

namespace UmbracoAngularCMS.NotificationHandlers
{
    public class ContentApprovalHandler : INotificationHandler<ContentSavingNotification>
    {
        private readonly IContentService _contentService;
        private readonly IUserService _userService;
        private readonly ILogger<ContentApprovalHandler> _logger;

        public ContentApprovalHandler(
            IContentService contentService,
            IUserService userService,
            ILogger<ContentApprovalHandler> logger)
        {
            _contentService = contentService;
            _userService = userService;
            _logger = logger;
        }

        public void Handle(ContentSavingNotification notification)
        {
            foreach (var content in notification.SavedEntities)
            {
                if (content.ContentType.Alias == "contentItem")
                {
                    var currentStatus = content.GetValue<string>("approvalStatus");

                    // Initialize workflow for new content or resubmitted content
                    if (string.IsNullOrEmpty(currentStatus) ||
                        currentStatus == "Draft" ||
                        currentStatus == "Rejected")
                    {
                        var creator = _userService.GetByProviderKey(content.CreatorId);
                        var workflow = CreateWorkflowForContent(content, creator?.Name);

                        if (workflow != null)
                        {
                            content.SetValue("approvalStatus", "Pending Approval");
                            content.SetValue("approvalHistory", JsonSerializer.Serialize(workflow));
                            content.SetValue("workflowStartDate", DateTime.Now.ToString("O"));
                            content.SetValue("rejectionReason", string.Empty);
                        }
                    }
                }
            }
        }

        private ApprovalWorkflow CreateWorkflowForContent(IContent content, string creatorName)
        {
            var workflow = new ApprovalWorkflow
            {
                Name = "Content Approval",
                CreatedBy = creatorName ?? "System",
                CreatedDate = DateTime.Now,
                Requirements = new List<ApprovalRequirement>(),
                MinimumApprovals = 1,
                RequiresAllApprovals = false // Any assigned person can approve
            };

            // Get assigned users
            var assignedUserIds = content.GetValue<string>("assignedUsers");
            if (!string.IsNullOrEmpty(assignedUserIds))
            {
                // Parse user IDs (could be comma-separated or JSON array)
                var userIds = ParseUserIds(assignedUserIds);

                if (userIds.Any())
                {
                    workflow.Requirements.Add(new ApprovalRequirement
                    {
                        Type = ApprovalType.AnyFromGroup,
                        RequirementName = "User Approval",
                        AssignedTo = userIds
                    });
                }
            }

            // Get assigned groups
            var assignedGroups = content.GetValue<string[]>("assignedGroups") ??
                                content.GetValue<string>("assignedGroups")?.Split(',');

            if (assignedGroups != null && assignedGroups.Any())
            {
                workflow.Requirements.Add(new ApprovalRequirement
                {
                    Type = ApprovalType.AnyFromGroup,
                    RequirementName = "Group Approval",
                    AssignedTo = assignedGroups.Where(g => !string.IsNullOrWhiteSpace(g)).ToList()
                });
            }

            // If no specific assignment, fall back to priority-based rules
            if (!workflow.Requirements.Any())
            {
                var priority = content.GetValue<string>("priority");
                workflow.Requirements.Add(new ApprovalRequirement
                {
                    Type = ApprovalType.AnyFromGroup,
                    RequirementName = "Default Approval",
                    AssignedTo = new List<string> { "editor", "manager", "Administrators" }
                });
            }

            return workflow;
        }

        private List<string> ParseUserIds(string userIdString)
        {
            var userIds = new List<string>();

            try
            {
                // Try parsing as JSON array first
                if (userIdString.StartsWith("["))
                {
                    var ids = JsonSerializer.Deserialize<List<string>>(userIdString);
                    userIds.AddRange(ids);
                }
                // Try parsing as comma-separated
                else if (userIdString.Contains(","))
                {
                    userIds.AddRange(userIdString.Split(',').Select(id => id.Trim()));
                }
                // Single ID
                else
                {
                    userIds.Add(userIdString.Trim());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing user IDs: {UserIds}", userIdString);
            }

            return userIds;
        }
    }
}