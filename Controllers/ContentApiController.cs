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

        [HttpPost("approve/{id}")]
        public IActionResult ApproveContent(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var content = _contentService.GetById(id);
                if (content == null)
                    return NotFound(new { error = "Content not found" });

                var currentUser = GetCurrentUser(request);
                if (currentUser == null)
                    return Unauthorized(new { error = "User not authenticated" });

                var approvalHistoryJson = content.GetValue<string>("approvalHistory");
                if (string.IsNullOrEmpty(approvalHistoryJson))
                    return BadRequest(new { error = "No approval workflow found" });

                var workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);

                // Check if user already approved
                var userId = currentUser.Id.ToString();
                var hasAlreadyApproved = workflow.Requirements
                    .SelectMany(r => r.Actions)
                    .Any(a => a.UserId == userId && a.Action == "Approved");

                if (hasAlreadyApproved)
                {
                    return BadRequest(new
                    {
                        error = "You have already approved this content"
                    });
                }

                // Check if user CAN approve (simplified)
                var canApprove = false;
                var userGroups = currentUser.Groups.Select(g => g.Alias).ToList();

                foreach (var requirement in workflow.Requirements)
                {
                    // Check if user is directly assigned
                    if (requirement.AssignedTo.Contains(userId))
                    {
                        canApprove = true;
                        break;
                    }

                    // Check if user's group is assigned
                    if (requirement.AssignedTo.Any(g => userGroups.Contains(g)))
                    {
                        canApprove = true;
                        break;
                    }
                }

                if (!canApprove)
                {
                    return StatusCode(403, new
                    {
                        error = "You are not assigned to approve this content"
                    });
                }

                // Add approval
                var approvalRequirement = workflow.Requirements.First();
                approvalRequirement.Actions.Add(new ApprovalAction
                {
                    UserId = userId,
                    UserName = currentUser.Name,
                    UserEmail = currentUser.Email,
                    Action = "Approved",
                    ActionDate = DateTime.Now,
                    Comments = request.Comments
                });

                // Update workflow
                content.SetValue("approvalHistory", JsonSerializer.Serialize(workflow));
                content.SetValue("approvalStatus", "Approved");

                // Publish if user has permission
                var canPublish = userGroups.Any(g => new[] { "Administrators", "Manager", "Director", "Editors" }.Contains(g));

                if (canPublish)
                {
                    var publishResult = _contentService.SaveAndPublish(content);
                    if (publishResult.Success)
                    {
                        return Ok(new
                        {
                            message = "Content approved and published!",
                            published = true
                        });
                    }
                }

                _contentService.Save(content);
                return Ok(new
                {
                    message = "Content approved successfully!",
                    published = false
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to approve content", details = ex.Message });
            }
        }

        [HttpGet("pending-approvals")]
        public IActionResult GetPendingApprovals()
        {
            try
            {
                var currentUser = GetCurrentUser();
                if (currentUser == null)
                    return Ok(new List<object>());

                var userId = currentUser.Id.ToString();
                var userGroups = currentUser.Groups.Select(g => g.Alias).ToList();
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
                        if (string.IsNullOrEmpty(approvalHistoryJson)) continue;

                        var workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);
                        if (workflow == null) continue;

                        // Check if user can see this item (is assigned)
                        var isAssigned = false;
                        foreach (var requirement in workflow.Requirements)
                        {
                            if (requirement.AssignedTo.Contains(userId) ||
                                requirement.AssignedTo.Any(g => userGroups.Contains(g)))
                            {
                                isAssigned = true;
                                break;
                            }
                        }

                        if (!isAssigned) continue;

                        // Check if user already approved
                        var hasApproved = workflow.Requirements
                            .SelectMany(r => r.Actions)
                            .Any(a => a.UserId == userId && a.Action == "Approved");

                        var creator = _userService.GetByProviderKey(content.CreatorId);

                        var item = new
                        {
                            id = content.Id,
                            name = content.Name,
                            createdBy = creator?.Name ?? "Unknown",
                            createDate = content.CreateDate,
                            priority = content.GetValue<string>("priority") ?? "medium",
                            category = content.GetValue<string>("category") ?? "",
                            canApprove = !hasApproved,
                            hasApproved = hasApproved,
                            assignedTo = GetAssignedToDisplay(content)
                        };

                        pendingItems.Add(item);
                    }
                }

                return Ok(pendingItems);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost("reject/{id}")]
        public IActionResult RejectContent(int id, [FromBody] RejectionRequest request)
        {
            try
            {
                // Validate comment is required
                if (string.IsNullOrWhiteSpace(request.Comments))
                {
                    return BadRequest(new { error = "Rejection comment is required" });
                }

                var content = _contentService.GetById(id);
                if (content == null)
                    return NotFound(new { error = "Content not found" });

                var currentUser = GetCurrentUser(request);
                if (currentUser == null)
                    return Unauthorized(new { error = "User not authenticated" });

                var approvalHistoryJson = content.GetValue<string>("approvalHistory");
                if (!string.IsNullOrEmpty(approvalHistoryJson))
                {
                    var workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);

                    // Add rejection to the first incomplete requirement user can act on
                    foreach (var requirement in workflow.Requirements)
                    {
                        if (CanUserActOnRequirement(currentUser, requirement))
                        {
                            requirement.Actions.Add(new ApprovalAction
                            {
                                UserId = currentUser.Id.ToString(),
                                UserName = currentUser.Name,
                                UserEmail = currentUser.Email,
                                Action = "Rejected",
                                ActionDate = DateTime.Now,
                                Comments = request.Comments
                            });
                            break;
                        }
                    }

                    content.SetValue("approvalHistory", JsonSerializer.Serialize(workflow));
                }

                content.SetValue("approvalStatus", "Rejected");
                content.SetValue("rejectionReason", request.Comments);
                content.SetValue("rejectedBy", currentUser.Name);
                content.SetValue("rejectionDate", DateTime.Now.ToString("O"));

                _contentService.Save(content);

                return Ok(new { message = "Content rejected with feedback provided to the author" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to reject content", details = ex.Message });
            }
        }

        [HttpGet("my-content")]
        public IActionResult GetMyContent([FromQuery] int? userId = null)
        {
            try
            {
                var currentUser = GetCurrentUser();
                var targetUserId = userId ?? currentUser?.Id;

                if (targetUserId == null)
                    return BadRequest(new { error = "User ID required" });

                var myContent = new List<object>();
                var rootContent = _contentService.GetRootContent().ToList();

                foreach (var root in rootContent)
                {
                    var descendants = _contentService.GetPagedDescendants(root.Id, 0, 1000, out _);

                    var userContent = descendants
                        .Where(x => x.ContentType.Alias == "contentItem" &&
                                   x.CreatorId == targetUserId)
                        .ToList();

                    foreach (var content in userContent)
                    {
                        var status = content.GetValue<string>("approvalStatus") ?? "Draft";
                        var rejectionReason = content.GetValue<string>("rejectionReason");
                        var rejectedBy = content.GetValue<string>("rejectedBy");
                        var rejectionDate = content.GetValue<string>("rejectionDate");

                        var item = new
                        {
                            id = content.Id,
                            title = content.Name,
                            status = status,
                            createDate = content.CreateDate,
                            updateDate = content.UpdateDate,
                            lastActionDate = !string.IsNullOrEmpty(rejectionDate)
                                ? DateTime.Parse(rejectionDate)
                                : content.UpdateDate,
                            rejectionDetails = status == "Rejected" ? new
                            {
                                reason = rejectionReason,
                                rejectedBy = rejectedBy,
                                date = rejectionDate
                            } : null,
                            canEdit = status == "Rejected" || status == "Draft"
                        };

                        myContent.Add(item);
                    }
                }

                return Ok(myContent.OrderByDescending(c => ((dynamic)c).updateDate));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to load content", details = ex.Message });
            }
        }

        [HttpGet("content-comparison/{id}")]
        public IActionResult GetContentComparison(int id)
        {
            try
            {
                var content = _contentService.GetById(id);
                if (content == null)
                    return NotFound(new { error = "Content not found" });

                // Get current published version
                IContent publishedVersion = null;
                if (content.Published)
                {
                    publishedVersion = content;
                }
                else if (content.PublishedVersionId > 0)
                {
                    // Get the published version
                    publishedVersion = _contentService.GetVersion(content.PublishedVersionId);
                }

                var comparison = new
                {
                    current = new
                    {
                        title = content.GetValue<string>("title"),
                        content = content.GetValue<string>("content"),
                        category = content.GetValue<string>("category"),
                        priority = content.GetValue<string>("priority"),
                        imageUrl = GetImageUrl(content)
                    },
                    published = publishedVersion != null ? new
                    {
                        title = publishedVersion.GetValue<string>("title"),
                        content = publishedVersion.GetValue<string>("content"),
                        category = publishedVersion.GetValue<string>("category"),
                        priority = publishedVersion.GetValue<string>("priority"),
                        imageUrl = GetImageUrl(publishedVersion)
                    } : null,
                    changes = GetChangeSummary(content, publishedVersion),
                    hasChanges = publishedVersion == null || HasContentChanged(content, publishedVersion)
                };

                return Ok(comparison);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Failed to get comparison", details = ex.Message });
            }
        }

        private IUser GetCurrentUser(ApprovalRequest request = null)
        {
            IUser currentUser = null;

            // Try multiple methods to get current user
            if (Request.Headers.TryGetValue("X-Current-User-Id", out var userIdHeader))
            {
                if (int.TryParse(userIdHeader, out var userId))
                {
                    currentUser = _userService.GetUserById(userId);
                }
            }

            if (currentUser == null && request?.ApprovedById > 0)
            {
                currentUser = _userService.GetUserById(request.ApprovedById);
            }

            if (currentUser == null)
            {
                currentUser = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser;
            }

            return currentUser;
        }

        private bool CanUserActOnRequirement(IUser user, ApprovalRequirement requirement)
        {
            var userGroups = user.Groups.Select(g => g.Alias).ToList();
            var userId = user.Id.ToString();

            if (requirement.Type == ApprovalType.AnyFromGroup &&
                requirement.AssignedTo.Any(g => userGroups.Contains(g)))
            {
                return true;
            }

            if (requirement.Type == ApprovalType.SpecificPerson &&
                requirement.AssignedTo.Contains(userId))
            {
                return true;
            }

            return false;
        }

        private object GetChangeSummary(IContent current, IContent published)
        {
            var changes = new List<string>();

            if (published == null)
            {
                changes.Add("New content");
                return new { summary = changes, count = changes.Count };
            }

            if (current.GetValue<string>("title") != published.GetValue<string>("title"))
                changes.Add("Title changed");
            if (current.GetValue<string>("content") != published.GetValue<string>("content"))
                changes.Add("Content updated");
            if (current.GetValue<string>("category") != published.GetValue<string>("category"))
                changes.Add("Category changed");
            if (current.GetValue<string>("priority") != published.GetValue<string>("priority"))
                changes.Add("Priority changed");

            return new { summary = changes, count = changes.Count };
        }

        private bool HasContentChanged(IContent current, IContent published)
        {
            if (published == null) return true;

            return current.GetValue<string>("title") != published.GetValue<string>("title") ||
                   current.GetValue<string>("content") != published.GetValue<string>("content") ||
                   current.GetValue<string>("category") != published.GetValue<string>("category") ||
                   current.GetValue<string>("priority") != published.GetValue<string>("priority");
        }

        private string GetImageUrl(IContent content)
        {
            // This would need proper implementation based on how images are stored
            var imageValue = content.GetValue<string>("featuredImage");
            return imageValue ?? string.Empty;
        }
        private string GetAssignedToDisplay(IContent content)
        {
            var assigned = new List<string>();

            // Get assigned users
            var userIds = content.GetValue<string>("assignedUsers");
            if (!string.IsNullOrEmpty(userIds))
            {
                // Parse and get user names
                var ids = ParseUserIds(userIds);
                foreach (var id in ids)
                {
                    if (int.TryParse(id, out var userId))
                    {
                        var user = _userService.GetUserById(userId);
                        if (user != null)
                            assigned.Add(user.Name);
                    }
                }
            }

            // Get assigned groups
            var groups = content.GetValue<string[]>("assignedGroups");
            if (groups != null && groups.Any())
            {
                assigned.AddRange(groups);
            }

            return assigned.Any() ? string.Join(", ", assigned) : "Default approvers";
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
            {//log error
            }

            return userIds;
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
    public class RejectionRequest : ApprovalRequest
    {
        public string Comments { get; set; } // Required for rejection
    }
}