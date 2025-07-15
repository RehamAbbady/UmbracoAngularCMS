using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using UmbracoAngularCMS.Models;

namespace UmbracoAngularCMS.Services
{
    public interface IApprovalWorkflowService
    {
        ApprovalWorkflow CreateWorkflowForContent(IContent content);
        List<IUser> GetPotentialApproversForRole(string role);
        bool CanUserApproveStep(IUser user, ApprovalStep step);
    }

    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly IUserService _userService;

        public ApprovalWorkflowService(IUserService userService)
        {
            _userService = userService;
        }

        public ApprovalWorkflow CreateWorkflowForContent(IContent content)
        {
            var category = content.GetValue<string>("category");
            var priority = content.GetValue<string>("priority");
            var creator = _userService.GetByProviderKey(content.CreatorId);

            var workflow = new ApprovalWorkflow
            {
                Name = DetermineWorkflowName(category, priority),
                RequiresAllApprovals = true,
                Steps = DetermineWorkflowSteps(category, priority),
                CreatedBy = creator?.Name ?? "System",
                CreatedDate = DateTime.Now
            };

            return workflow;
        }

        public List<IUser> GetPotentialApproversForRole(string role)
        {
            // Get all users who could approve for a given role
            var approvers = new List<IUser>();

            switch (role.ToLower())
            {
                case "editors":
                    approvers.AddRange(GetUsersInGroup("editor"));
                    break;
                case "manager":
                    approvers.AddRange(GetUsersInGroup("manager"));
                    approvers.AddRange(GetUsersInGroup("director")); // Directors can also approve manager steps
                    break;
                case "director":
                    approvers.AddRange(GetUsersInGroup("director"));
                    break;
            }

            // Administrators can always approve
            approvers.AddRange(GetUsersInGroup("administrators"));

            // Remove duplicates
            return approvers.GroupBy(u => u.Id).Select(g => g.First()).ToList();
        }

        public bool CanUserApproveStep(IUser user, ApprovalStep step)
        {
            var userGroups = user.Groups.Select(g => g.Alias).ToList();
            return step.RequiredApproverGroups.Any(g => userGroups.Contains(g));
        }

        private List<IUser> GetUsersInGroup(string groupAlias)
        {
            var group = _userService.GetUserGroupByAlias(groupAlias);
            if (group == null) return new List<IUser>();

            return _userService.GetAllInGroup(group.Id).ToList();
        }

        private string DetermineWorkflowName(string category, string priority)
        {
            // Determine workflow based on business rules
            if (IsQuickApproval(category, priority))
                return "Quick Approval (Single Step)";

            if (IsExecutiveApproval(category, priority))
                return "Executive Approval (3 Steps)";

            return "Standard Approval (2 Steps)";
        }

        private List<ApprovalStep> DetermineWorkflowSteps(string category, string priority)
        {
            var steps = new List<ApprovalStep>();

            // Always need at least editor approval
            steps.Add(CreateApprovalStep(1, "editor", new[] { "editor", "administrators" }));

            // Add manager step for medium+ priority
            if (!IsQuickApproval(category, priority))
            {
                steps.Add(CreateApprovalStep(2, "manager", new[] { "manager", "director", "administrators" }));
            }

            // Add director step for high priority
            if (IsExecutiveApproval(category, priority))
            {
                steps.Add(CreateApprovalStep(3, "director", new[] { "director", "administrators" }));
            }

            return steps;
        }

        private ApprovalStep CreateApprovalStep(int order, string role, string[] requiredGroups)
        {
            return new ApprovalStep
            {
                Order = order,
                ApproverRole = role,
                RequiredApproverGroups = requiredGroups.ToList(),
                IsApproved = false
            };
        }

        private bool IsQuickApproval(string category, string priority)
        {
            return priority == "low" || category == "blog";
        }

        private bool IsExecutiveApproval(string category, string priority)
        {
            return priority == "high" || priority == "urgent";
        }
    }
}