using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoAngularCMS.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Microsoft.AspNetCore.Cors;
using Umbraco.Cms.Core;
using System.Text.Json;

namespace UmbracoAngularCMS.Controllers
{
    [EnableCors("AllowAngularApp")]
    [ApiController]
    [Route("api/[controller]")]
    public class ContentApiController : UmbracoApiController
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IPublishedContentQuery _publishedContentQuery;
        private readonly IContentService _contentService;

        public ContentApiController(IHttpContextAccessor httpContextAccessor,
            IPublishedContentQuery publishedContentQuery,
            IContentService contentService)
        {
            _httpContextAccessor = httpContextAccessor;
            _publishedContentQuery = publishedContentQuery;
            _contentService = contentService;
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
                var pendingItems = new List<object>();

                // Get all content items with pending approval
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
                                // Log error but continue
                            }
                        }

                        var item = new
                        {
                            id = content.Id,
                            name = content.Name,
                            createdBy = content.CreatorId,
                            createDate = content.CreateDate,
                            priority = content.GetValue<string>("priority") ?? "medium",
                            category = content.GetValue<string>("category") ?? "",
                            approvalType = workflow?.Steps.Count == 1 ? "Single" : "Multiple",
                            totalApprovals = workflow?.Steps.Count ?? 0,
                            completedApprovals = workflow?.Steps.Count(s => s.IsApproved) ?? 0,
                            approvalProgress = $"{workflow?.Steps.Count(s => s.IsApproved) ?? 0}/{workflow?.Steps.Count ?? 0}",
                            currentStep = workflow?.Steps.FirstOrDefault(s => !s.IsApproved)?.ApproverRole ?? "Unknown"
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

                var approvalHistoryJson = content.GetValue<string>("approvalHistory");
                if (string.IsNullOrEmpty(approvalHistoryJson))
                    return BadRequest(new { error = "No approval workflow found" });

                var workflow = JsonSerializer.Deserialize<ApprovalWorkflow>(approvalHistoryJson);
                var currentStep = workflow.Steps.FirstOrDefault(s => !s.IsApproved);

                if (currentStep != null)
                {
                    currentStep.IsApproved = true;
                    currentStep.ApprovedBy = request.ApprovedBy ?? "Unknown";
                    currentStep.ApprovalDate = DateTime.Now;

                    content.SetValue("approvalHistory", JsonSerializer.Serialize(workflow));

                    // Check if all approved
                    if (workflow.Steps.All(s => s.IsApproved))
                    {
                        content.SetValue("approvalStatus", "Approved");

                        // AUTO-PUBLISH: SaveAndPublish instead of just Save
                        var publishResult = _contentService.SaveAndPublish(content);

                        if (publishResult.Success)
                        {
                            return Ok(new
                            {
                                message = workflow.Steps.Count == 1
                                    ? "Content approved and published successfully!"
                                    : "All approvals complete. Content has been published!",
                                published = true,
                                canPublish = true
                            });
                        }
                        else
                        {
                            // If publish failed, still save the approval
                            _contentService.Save(content);

                            var errors = string.Join(", ", publishResult.EventMessages?.GetAll()
                                .Select(x => x.Message));

                            return Ok(new
                            {
                                message = "Content approved but could not be published automatically. " + errors,
                                published = false,
                                canPublish = true,
                                errors = errors
                            });
                        }
                    }
                    else
                    {
                        // Not all approved yet, just save
                        _contentService.Save(content);
                        var remaining = workflow.Steps.Count(s => !s.IsApproved);
                        return Ok(new
                        {
                            message = $"Approval recorded. {remaining} more approval(s) needed.",
                            published = false,
                            canPublish = false
                        });
                    }
                }

                return Ok(new { message = "No pending approvals for this content" });
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


    }
    public class ApprovalRequest
    {
        public string? ApprovedBy { get; set; }
        public string? RejectedBy { get; set; }
        public string? Comments { get; set; }
    }
}