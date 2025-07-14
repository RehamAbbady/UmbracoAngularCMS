using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoAngularCMS.Models;

namespace UmbracoAngularCMS.NotificationHandlers
{
    public class ContentApprovalHandler : INotificationHandler<ContentSavingNotification>
    {
        private readonly IContentService _contentService;
        private readonly IUserService _userService;

        public ContentApprovalHandler(IContentService contentService, IUserService userService)
        {
            _contentService = contentService;
            _userService = userService;
        }

        public void Handle(ContentSavingNotification notification)
        {
            foreach (var content in notification.SavedEntities)
            {
                if (content.ContentType.Alias == "contentItem")
                {
                    var isDirty = content.IsDirty();

                    if (isDirty && !content.Published)
                    {
                        var currentStatus = content.GetValue<string>("approvalStatus");

                        if (string.IsNullOrEmpty(currentStatus) || currentStatus == "Draft")
                        {
                            // Get the creator's information
                            var creator = _userService.GetByProviderKey(content.CreatorId);

                            // Initialize approval workflow
                            var workflow = GetWorkflowForContent(content, creator?.Name);
                            if (workflow != null)
                            {
                                var approvalHistory = JsonSerializer.Serialize(workflow);
                                content.SetValue("approvalStatus", "Pending Approval");
                                content.SetValue("approvalHistory", approvalHistory);
                            }
                        }
                    }
                }
            }
        }

        private ApprovalWorkflow GetWorkflowForContent(IContent content, string creatorName)
        {
            var category = content.GetValue<string>("category");
            var priority = content.GetValue<string>("priority");

            // Create workflow name based on requirements
            var workflowName = DetermineWorkflowName(category, priority);
            var steps = DetermineWorkflowSteps(category, priority);

            return new ApprovalWorkflow
            {
                Name = workflowName,
                RequiresAllApprovals = true,
                Steps = steps,
                CreatedBy = creatorName ?? "System"
            };
        }

        private string DetermineWorkflowName(string category, string priority)
        {
            if (priority == "low")
                return "Quick Approval (Single Step)";

            if (priority == "high")
                return "Executive Approval (3 Steps)";

            return "Standard Approval (2 Steps)";
        }

        private List<ApprovalStep> DetermineWorkflowSteps(string category, string priority)
        {
            var steps = new List<ApprovalStep>();

            // First step - Editor approval
            steps.Add(new ApprovalStep
            {
                Order = 1,
                ApproverRole = "editor",
                RequiredApproverGroups = new List<string> { "editor", "administrators" },
                IsApproved = false
            });

            // Medium priority or announcements need manager approval
            if (priority == "medium" || category == "announcement" ||
                priority == "high" || priority == "urgent")
            {
                steps.Add(new ApprovalStep
                {
                    Order = 2,
                    ApproverRole = "manager",
                    RequiredApproverGroups = new List<string> { "manager", "director", "administrators" },
                    IsApproved = false
                });
            }

            // High/urgent priority needs director approval
            if (priority == "high" || priority == "urgent")
            {
                steps.Add(new ApprovalStep
                {
                    Order = 3,
                    ApproverRole = "director",
                    RequiredApproverGroups = new List<string> { "director", "administrators" },
                    IsApproved = false
                });
            }

            return steps;
        }
    }
}