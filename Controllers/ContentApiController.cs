using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoAngularCMS.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Microsoft.AspNetCore.Cors;
using Umbraco.Cms.Core;
using System.Text.Json;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Extensions;

namespace UmbracoAngularCMS.Controllers
{
    [EnableCors("AllowAngularApp")]
    [ApiController]
    [Route("api/[controller]")]
    public class ContentApiController : UmbracoApiController
    {
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPublishedContentQuery _publishedContentQuery;
        private readonly IContentService _contentService;
        private readonly IUserService _userService;

        public ContentApiController(IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
            IHttpContextAccessor httpContextAccessor,
            IPublishedContentQuery publishedContentQuery,
            IContentService contentService,
            IUserService userService)
        {
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
            _httpContextAccessor = httpContextAccessor;
            _publishedContentQuery = publishedContentQuery;
            _contentService = contentService;
            _userService = userService;
        }

        [HttpGet("published-content")]
        public IActionResult GetPublishedContent()
        {
            var contentItems = _publishedContentQuery
                .ContentAtRoot()
                .FirstOrDefault()?
                .DescendantsOfType("contentItem")
                .Where(x => x.IsPublished())
                .Select(MapToContentItemDto)
                .OrderByDescending(x => x.UpdateDate)
                .ToList();

            return Ok(contentItems ?? new List<ContentItemDto>());
        }

        [HttpGet("dropdown-options")]
        public IActionResult GetDropdownOptions([FromQuery] string category = null)
        {
            var options = _publishedContentQuery
                .ContentAtRoot()
                .FirstOrDefault()?
                .DescendantsOfType("dropdownOption");

            var tt = _publishedContentQuery
           .ContentAtRoot().FirstOrDefault();

            if (!string.IsNullOrEmpty(category))
            {
                options = options?.Where(x => x.Value<string>("category") == category);
            }

            var result = options?
                .Select(x => new DropdownOptionDto
                {
                    Key = x.Value<string>("ddKey"),
                    Value = x.Value<string>("value"),
                    Category = x.Value<string>("category"),
                    SortOrder1 = x.Value<int>("sortOrder1")
                })
                .OrderBy(x => x.SortOrder1)
                .ToList();

            return Ok(result ?? new List<DropdownOptionDto>());
        }

        private ContentItemDto MapToContentItemDto(IPublishedContent content)
        {
            var imageUrl = content.Value<IPublishedContent>("featuredImage")?.Url() ?? string.Empty;

            // Convert relative URL to absolute URL
            if (!string.IsNullOrEmpty(imageUrl) && imageUrl.StartsWith("/"))
            {
                var request = _httpContextAccessor.HttpContext?.Request;
                if (request != null)
                {
                    imageUrl = $"{request.Scheme}://{request.Host}{imageUrl}";
                }
            }

            return new ContentItemDto
            {
                Id = content.Id,
                Title = content.Value<string>("title") ?? string.Empty,
                Content = content.Value<string>("content") ?? string.Empty,
                ImageUrl = imageUrl,
                Category = content.Value<string>("category") ?? string.Empty,
                Priority = content.Value<string>("priority") ?? string.Empty,
                Status = content.Value<string>("approvalStatus") ?? "Published",
                CreateDate = content.CreateDate,
                UpdateDate = content.UpdateDate
            };
        }

        [HttpGet("pending-approvals")]
        public IActionResult GetPendingApprovals()
        {
            try
            {
                IUser currentUser = null;

                // Try to get user from custom headers (sent by JavaScript)
                if (Request.Headers.TryGetValue("X-Current-User-Id", out var userIdHeader))
                {
                    if (int.TryParse(userIdHeader, out var userId))
                    {
                        currentUser = _userService.GetUserById(userId);
                    }
                }

                // Fallback to email if provided
                if (currentUser == null && Request.Headers.TryGetValue("X-Current-User-Email", out var emailHeader))
                {
                    currentUser = _userService.GetByEmail(emailHeader);
                }

                // Final fallback to backoffice security
                if (currentUser == null)
                {
                    currentUser = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
                }

                var userGroups = currentUser?.Groups.Select(g => g.Alias).ToList() ?? new List<string>();
                var currentUserEmail = currentUser?.Email;
                var pendingItems = new List<object>();

                var rootContent = _contentService.GetRootContent().ToList();

                foreach (var root in rootContent)
                {
                    var descendants = _contentService.GetPagedDescendants(root.Id, 0, 1000, out _);

                    var contentItems = descendants
                        .Where(x => x.ContentType.Alias == "contentItem" &&
                                   x.GetValue<string>("approvalStatus") == "Pending Approval")
                        .ToList();

                    foreach (var content in contentItems)
                    {
                        var approvalHistoryJson = content.GetValue<string>("approvalHistory");
                        ApprovalWorkflow workflow = null;

                        if (!string.IsNullOrEmpty(approvalHistoryJson))
                        {
                            try
                            {
                                workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);
                            }
                            catch (Exception ex)
                            {
                                continue;
                            }
                        }

                        if (workflow == null) continue;

                        // Check if current user has already approved
                        var hasApproved = false;
                        if (currentUser != null && !string.IsNullOrEmpty(currentUserEmail))
                        {
                            hasApproved = workflow.Steps.Any(s =>
                                s.IsApproved &&
                                s.ApproverEmail?.Equals(currentUserEmail, StringComparison.OrdinalIgnoreCase) == true);
                        }

                        // Find current step
                        var currentStep = workflow.Steps.FirstOrDefault(s => !s.IsApproved);

                        // Check if user can approve current step
                        var canApprove = false;
                        if (currentStep != null && !hasApproved && currentUser != null)
                        {
                            canApprove = currentStep.RequiredApproverGroups.Any(g => userGroups.Contains(g));
                        }

                        // Get creator name
                        var creator = _userService.GetByProviderKey(content.CreatorId);

                        var item = new
                        {
                            id = content.Id,
                            name = content.Name,
                            createdBy = creator?.Name ?? "Unknown",
                            createDate = content.CreateDate,
                            priority = content.GetValue<string>("priority") ?? "medium",
                            category = content.GetValue<string>("category") ?? "",
                            workflowName = workflow.Name,
                            approvalType = workflow.Steps.Count == 1 ? "Quick" : workflow.Steps.Count == 2 ? "Standard" : "Executive",
                            totalApprovals = workflow.Steps.Count,
                            completedApprovals = workflow.Steps.Count(s => s.IsApproved),
                            approvalProgress = $"{workflow.Steps.Count(s => s.IsApproved)}/{workflow.Steps.Count}",
                            currentStep = currentStep?.ApproverRole ?? "Complete",
                            hasApproved = hasApproved,
                            canApprove = canApprove,
                            approvalHistory = workflow.Steps.Where(s => s.IsApproved).Select(s => new
                            {
                                approvedBy = s.ApprovedBy,
                                approvedDate = s.ApprovalDate?.ToString("yyyy-MM-dd HH:mm"),
                                role = s.ApproverRole
                            })
                        };

                        pendingItems.Add(item);
                    }
                }

                return Ok(pendingItems);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load pending approvals", details = ex.Message });
            }
        }

        [HttpPost("approve/{id}")]
        public IActionResult ApproveContent(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var content = _contentService.GetById(id);
                if (content == null)
                    return NotFound(new { error = "Content not found" });

                // Get current user from request
                IUser currentUser = null;

                // Try to get by ID if provided
                if (request.ApprovedById > 0)
                {
                    currentUser = _userService.GetUserById(request.ApprovedById);
                }

                // Fallback to email
                if (currentUser == null && !string.IsNullOrEmpty(request.ApprovedByEmail))
                {
                    currentUser = _userService.GetByEmail(request.ApprovedByEmail);
                }

                // Fallback to name
                if (currentUser == null && !string.IsNullOrEmpty(request.ApprovedBy))
                {
                    currentUser = _userService.GetByUsername(request.ApprovedBy);
                }

                // Final fallback to backoffice security
                if (currentUser == null)
                {
                    currentUser = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
                }

                if (currentUser == null)
                    return Unauthorized(new { error = "User not authenticated" });
                var approvalHistoryJson = content.GetValue<string>("approvalHistory");
                if (string.IsNullOrEmpty(approvalHistoryJson))
                    return BadRequest(new { error = "No approval workflow found" });

                var workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);

                var currentUserEmail = currentUser.Email;
                var hasAlreadyApproved = workflow.Steps.Any(s =>
                    s.IsApproved &&
                    s.ApproverEmail?.Equals(currentUserEmail, StringComparison.OrdinalIgnoreCase) == true);

                if (hasAlreadyApproved)
                {
                    return BadRequest(new
                    {
                        error = "You have already approved this content",
                        message = "Each user can only approve content once in the workflow"
                    });
                }

                var currentStep = workflow.Steps.FirstOrDefault(s => !s.IsApproved);

                if (currentStep == null)
                {
                    return BadRequest(new { error = "No pending approvals for this content" });
                }

                var userGroups = currentUser.Groups.Select(g => g.Alias).ToList();
                var canApprove = currentStep.RequiredApproverGroups.Any(g => userGroups.Contains(g));

                if (!canApprove)
                {
                    return StatusCode(403, new
                    {
                        error = "You don't have permission to approve this step",
                        message = $"This step requires one of these roles: {string.Join(", ", currentStep.RequiredApproverGroups)}",
                        yourRoles = userGroups
                    });
                }

                // Approve the step
                currentStep.IsApproved = true;
                currentStep.ApprovedBy = currentUser.Name;
                currentStep.ApproverEmail = currentUser.Email;
                currentStep.ApprovalDate = DateTime.Now;
                currentStep.Action = "Approved";
                currentStep.Comments = request.Comments;

                content.SetValue("approvalHistory", JsonSerializer.Serialize(workflow));

                // Check if all approved
                if (workflow.Steps.All(s => s.IsApproved))
                {
                    content.SetValue("approvalStatus", "Approved");

                    // Auto-publish if user has permission
                    if (userGroups.Contains("administrators") ||
                        userGroups.Contains("manager") ||
                        userGroups.Contains("director") ||
                        (workflow.Steps.Count == 1 && userGroups.Contains("editor")))
                    {
                        var publishResult = _contentService.SaveAndPublish(content);

                        if (publishResult.Success)
                        {
                            return Ok(new
                            {
                                message = "Content approved and published successfully!",
                                workflow = GetWorkflowSummary(workflow),
                                published = true
                            });
                        }
                        else
                        {
                            _contentService.Save(content);
                            var errors = string.Join(", ", publishResult.EventMessages
                                .GetAll().Where(x => x.MessageType == EventMessageType.Error)
                                .Select(x => x.Message));

                            return Ok(new
                            {
                                message = "Content approved but could not be published automatically. " + errors,
                                workflow = GetWorkflowSummary(workflow),
                                published = false,
                                errors = errors
                            });
                        }
                    }
                    else
                    {
                        _contentService.Save(content);
                        return Ok(new
                        {
                            message = "Content approved. A user with publish permissions needs to publish it.",
                            workflow = GetWorkflowSummary(workflow),
                            published = false
                        });
                    }
                }

                // Not all approved yet
                _contentService.Save(content);
                var remaining = workflow.Steps.Count(s => !s.IsApproved);
                var nextStep = workflow.Steps.FirstOrDefault(s => !s.IsApproved);

                return Ok(new
                {
                    message = $"Your approval has been recorded. {remaining} more approval(s) needed.",
                    nextApprover = nextStep?.ApproverRole,
                    workflow = GetWorkflowSummary(workflow),
                    published = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to approve content", details = ex.Message });
            }
        }

        [HttpPost("reject/{id}")]
        public IActionResult RejectContent(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var content = _contentService.GetById(id);
                if (content == null)
                    return NotFound();

                content.SetValue("approvalStatus", "Rejected");

                // Update workflow to show rejection
                var approvalHistoryJson = content.GetValue<string>("approvalHistory");
                if (!string.IsNullOrEmpty(approvalHistoryJson))
                {
                    var workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);
                    var currentStep = workflow.Steps.FirstOrDefault(s => !s.IsApproved);
                    if (currentStep != null)
                    {
                        currentStep.Comments = $"Rejected by {request.ApprovedBy ?? request.RejectedBy ?? "Unknown"}";
                    }
                    content.SetValue("approvalHistory", JsonSerializer.Serialize(workflow));
                }

                _contentService.Save(content);

                return Ok(new { message = "Content rejected successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to reject content", details = ex.Message });
            }
        }
        private IActionResult Forbidden(object value)
        {
            return StatusCode(403, value);
        }

        private object GetWorkflowSummary(ApprovalWorkflow workflow)
        {
            return new
            {
                name = workflow.Name,
                totalSteps = workflow.Steps.Count,
                completedSteps = workflow.Steps.Count(s => s.IsApproved),
                steps = workflow.Steps.Select(s => new
                {
                    step = s.Order,
                    role = s.ApproverRole,
                    approved = s.IsApproved,
                    approvedBy = s.ApprovedBy,
                    approvedDate = s.ApprovalDate?.ToString("yyyy-MM-dd HH:mm"),
                    action = s.Action
                })
            };
        }

    }
    public class ApprovalRequest
    {
        public string? ApprovedBy { get; set; }
        public int ApprovedById { get; set; }
        public string? ApprovedByEmail { get; set; }
        public string? RejectedBy { get; set; }
        public int RejectedById { get; set; }
        public string? RejectedByEmail { get; set; }
        public string? Comments { get; set; }
    }
}