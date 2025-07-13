using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoAngularCMS.Services
{
    public class ApprovalWorkflowService
    {
        private readonly IContentService _contentService;
        private readonly IUserService _userService;

        public ApprovalWorkflowService(IContentService contentService, IUserService userService)
        {
            _contentService = contentService;
            _userService = userService;
        }

        public bool ProcessApproval(IContent content, string approverEmail)
        {
            var currentStatus = content.GetValue<string>("status");
            var approvalLevel = content.GetValue<string>("approvalLevel");
            var approvalHistory = content.GetValue<string>("approvalHistory") ?? "";

            var historyEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm} - {approverEmail} approved at {approvalLevel}\n";
            content.SetValue("approvalHistory", approvalHistory + historyEntry);

            switch (approvalLevel)
            {
                case "Level 1":
                    return ProcessLevel1Approval(content, approverEmail);
                case "Level 2":
                    return ProcessLevel2Approval(content, approverEmail);
                case "Level 3":
                    return ProcessLevel3Approval(content, approverEmail);
                default:
                    return false;
            }
        }

        private bool ProcessLevel1Approval(IContent content, string approverEmail)
        {
            if (RequiresLevel2Approval(content))
            {
                content.SetValue("status", "Pending Approval");
                content.SetValue("approvalLevel", "Level 2");
                content.SetValue("currentApprover", GetLevel2Approver(content));
                _contentService.Save(content);
                return false; 
            }
            else
            {
                content.SetValue("status", "Approved");
                content.SetValue("approvedBy", approverEmail);
                content.SetValue("approvalDate", DateTime.Now);
                _contentService.SaveAndPublish(content);
                return true;
            }
        }

        private bool ProcessLevel2Approval(IContent content, string approverEmail)
        {
            if (RequiresLevel3Approval(content))
            {
                content.SetValue("status", "Pending Approval");
                content.SetValue("approvalLevel", "Level 3");
                content.SetValue("currentApprover", GetLevel3Approver(content));
                _contentService.Save(content);
                return false; 
            }
            else
            {
                content.SetValue("status", "Approved");
                content.SetValue("approvedBy", approverEmail);
                content.SetValue("approvalDate", DateTime.Now);
                _contentService.SaveAndPublish(content);
                return true;
            }
        }

        private bool ProcessLevel3Approval(IContent content, string approverEmail)
        {
            content.SetValue("status", "Approved");
            content.SetValue("approvedBy", approverEmail);
            content.SetValue("approvalDate", DateTime.Now);
            _contentService.SaveAndPublish(content);
            return true;
        }

        private bool RequiresLevel2Approval(IContent content)
        { 
            var category = content.GetValue<string>("category");
            return category == "Important" || category == "Critical";
        }

        private bool RequiresLevel3Approval(IContent content)
        {
            // Add your business logic here
            var category = content.GetValue<string>("category");
            return category == "Critical";
        }

        private string GetLevel2Approver(IContent content)
        {
            return "manager@company.com";
        }

        private string GetLevel3Approver(IContent content)
        {
            // Return appropriate Level 3 approver based on content
            return "director@company.com";
        }
    }
}