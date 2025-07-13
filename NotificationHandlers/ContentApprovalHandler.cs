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
                            // Initialize approval workflow based on content
                            var workflow = GetWorkflowForContent(content);
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

        private ApprovalWorkflow GetWorkflowForContent(IContent content)
        {
            var category = content.GetValue<string>("category");
            var priority = content.GetValue<string>("priority");

            if (priority == "low" || category == "blog" )
            {
                return new ApprovalWorkflow
                {
                    Name = "Single Approval",
                    RequiresAllApprovals = true,
                    Steps = new List<ApprovalStep>
                    {
                        new ApprovalStep
                        {
                            Order = 1,
                            ApproverRole = "Editor",
                            ApproverEmail = "editor@company.com",
                            IsApproved = false
                        }
                    }
                };
            }

            // MULTIPLE APPROVALS for medium priority
            if (priority == "medium" || category == "announcement")
            {
                return new ApprovalWorkflow
                {
                    Name = "Standard Approval",
                    RequiresAllApprovals = true,
                    Steps = new List<ApprovalStep>
                    {
                        new ApprovalStep
                        {
                            Order = 1,
                            ApproverRole = "Editor",
                            ApproverEmail = "editor@company.com",
                            IsApproved = false
                        },
                        new ApprovalStep
                        {
                            Order = 2,
                            ApproverRole = "Manager",
                            ApproverEmail = "manager@company.com",
                            IsApproved = false
                        }
                    }
                };
            }

            // THREE APPROVALS for high/urgent priority
            if (priority == "high" || priority == "urgent")
            {
                return new ApprovalWorkflow
                {
                    Name = "Executive Approval",
                    RequiresAllApprovals = true,
                    Steps = new List<ApprovalStep>
                    {
                        new ApprovalStep
                        {
                            Order = 1,
                            ApproverRole = "Editor",
                            ApproverEmail = "editor@company.com",
                            IsApproved = false
                        },
                        new ApprovalStep
                        {
                            Order = 2,
                            ApproverRole = "Manager",
                            ApproverEmail = "manager@company.com",
                            IsApproved = false
                        },
                        new ApprovalStep
                        {
                            Order = 3,
                            ApproverRole = "Director",
                            ApproverEmail = "director@company.com",
                            IsApproved = false
                        }
                    }
                };
            }

            // Default to single approval
            return new ApprovalWorkflow
            {
                Name = "Single Approval",
                RequiresAllApprovals = true,
                Steps = new List<ApprovalStep>
                {
                    new ApprovalStep
                    {
                        Order = 1,
                        ApproverRole = "Editor",
                        ApproverEmail = "editor@company.com",
                        IsApproved = false
                    }
                }
            };
        }
    }
}