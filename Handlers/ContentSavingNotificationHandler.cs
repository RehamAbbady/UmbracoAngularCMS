using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using UmbracoAngularCMS.Services;

namespace UmbracoAngularCMS.Handlers
{
    public class ContentSavingNotificationHandler : INotificationHandler<ContentSavingNotification>
    {
        private readonly ApprovalWorkflowService _approvalService;
        private readonly IUserService _userService;

        public ContentSavingNotificationHandler(ApprovalWorkflowService approvalService, IUserService userService)
        {
            _approvalService = approvalService;
            _userService = userService;
        }

        public void Handle(ContentSavingNotification notification)
        {
            foreach (var content in notification.SavedEntities)
            {
                if (content.ContentType.Alias == "contentItem")
                {
                    var currentStatus = content.GetValue<string>("status");

                    // If content is being saved for the first time
                    if (string.IsNullOrEmpty(currentStatus))
                    {
                        content.SetValue("status", "Draft");
                        content.SetValue("createdBy", GetCurrentUserEmail());
                        content.SetValue("approvalLevel", "Level 1");
                    }

                    // If content is being submitted for approval
                    if (currentStatus == "Draft" && content.GetValue<bool>("submitForApproval"))
                    {
                        content.SetValue("status", "Pending Approval");
                        content.SetValue("approvalLevel", "Level 1");
                        content.SetValue("currentApprover", GetLevel1Approver(content));
                        content.SetValue("submitForApproval", false);
                    }
                }
            }
        }

        private string GetCurrentUserEmail()
        {
            // Get current user email - implement based on your auth system
            return "editor@company.com";
        }

        private string GetLevel1Approver(IContent content)
        {
            // Return appropriate Level 1 approver
            return "supervisor@company.com";
        }
    }
}