using System.Linq;
using System.Text.Json;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using UmbracoAngularCMS.Models;

namespace UmbracoAngularCMS.NotificationHandlers
{
    public class ContentPublishingHandler : INotificationHandler<ContentPublishingNotification>
    {
        public void Handle(ContentPublishingNotification notification)
        {
            foreach (var content in notification.PublishedEntities)
            {
                if (content.ContentType.Alias == "contentItem")
                {
                    var approvalStatus = content.GetValue<string>("approvalStatus");
                    var approvalHistoryJson = content.GetValue<string>("approvalHistory");

                    // If there's no approval history, allow publishing (for backward compatibility)
                    if (string.IsNullOrEmpty(approvalHistoryJson))
                    {
                        continue;
                    }

                    if (approvalStatus != "Approved")
                    {
                        try
                        {
                            // Check if all required approvals are complete
                            var workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);

                            if (workflow != null && workflow.RequiresAllApprovals)
                            {
                                var allApproved = workflow.Steps.All(s => s.IsApproved);
                                if (!allApproved)
                                {
                                    // Get more specific message based on workflow
                                    var pendingSteps = workflow.Steps.Where(s => !s.IsApproved).ToList();
                                    var nextApprover = pendingSteps.FirstOrDefault()?.ApproverRole ?? "Unknown";
                                    var message = $"This content requires approval from: {nextApprover}. Total approvals needed: {workflow.Steps.Count}, completed: {workflow.Steps.Count(s => s.IsApproved)}";

                                    notification.CancelOperation(new EventMessage(
                                        "Approval Required",
                                        message,
                                        EventMessageType.Error));
                                }
                                else
                                {
                                    // All approved, update status
                                    content.SetValue("approvalStatus", "Approved");
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // If JSON parsing fails, prevent publishing for safety
                            notification.CancelOperation(new EventMessage(
                                "Approval Error",
                                "Unable to verify approval status. Please contact administrator.",
                                EventMessageType.Error));
                        }
                    }
                }
            }
        }
    }
}