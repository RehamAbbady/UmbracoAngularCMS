using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using UmbracoAngularCMS.NotificationHandlers;

namespace UmbracoAngularCMS.Composers
{
    public class ApprovalWorkflowComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.AddNotificationHandler<ContentSavingNotification, ContentApprovalHandler>();
            builder.AddNotificationHandler<ContentPublishingNotification, ContentPublishingHandler>();
        }
    }
}