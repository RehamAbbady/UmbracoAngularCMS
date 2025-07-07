using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoAngularCMS.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Microsoft.AspNetCore.Cors;
using Umbraco.Cms.Core;
using System.Text.Json;
using System.Xml.XPath;


namespace UmbracoAngularCMS.Controllers
{
    [EnableCors("AllowAngularApp")]
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]

    public class ContentApiController : UmbracoApiController
    {
        private readonly IContentService _contentService;
        private readonly IPublishedContentQuery _publishedContentQuery;
        private readonly IContentTypeService _contentTypeService;
        private readonly IMediaService _mediaService;

        public ContentApiController(
            IContentService contentService, 
            IPublishedContentQuery publishedContentQuery,
            IContentTypeService contentTypeService,
            IMediaService mediaService)
        {
            _contentService = contentService;
            _publishedContentQuery = publishedContentQuery;
            _contentTypeService = contentTypeService;
            _mediaService = mediaService;
        }

        [HttpGet("approved")]
        public IActionResult GetApprovedContent()
        {
            var contentItems = _publishedContentQuery
                .ContentAtRoot()
                .FirstOrDefault()?
                .Descendants("contentItem")
                .Where(x => x.Value<string>("status") == "Approved")
                .Select(MapToContentItemModel)
                .ToList();

            return Ok(contentItems ?? new List<ContentItemModel>());
        }

        [HttpGet("pending")]
        public IActionResult GetPendingContent()
        {
            try
            {
                var contentType = _contentTypeService.Get("contentItem");
                if (contentType == null)
                    return Ok(new List<ContentItemModel>());

                // Get all content and filter for contentItem type
                var allContent = _contentService.GetPagedChildren(-1, 0, 1000, out var totalRecords)
                    .Where(x => x.ContentType.Alias == "contentItem")
                    .ToList();
                
                // Also get children of root content nodes
                var rootContent = _contentService.GetRootContent();
                foreach (var root in rootContent)
                {
                    var children = _contentService.GetPagedChildren(root.Id, 0, 1000, out var childrenTotal)
                        .Where(x => x.ContentType.Alias == "contentItem");
                    allContent.AddRange(children);
                }

                var pendingItems = allContent
                    .Where(x => x.GetValue<string>("status") == "Pending Approval")
                    .Select(MapToContentItemModel)
                    .ToList();

                return Ok(pendingItems);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error retrieving pending content", error = ex.Message });
            }
        }

        [HttpGet("all")]
        public IActionResult GetAllContent()
        {
            try
            {
                var contentType = _contentTypeService.Get("contentItem");
                if (contentType == null)
                    return Ok(new List<ContentItemModel>());

                // Get all content and filter for contentItem type
                var allContent = _contentService.GetPagedChildren(-1, 0, 1000, out var totalRecords)
                    .Where(x => x.ContentType.Alias == "contentItem")
                    .ToList();
                
                // Also get children of root content nodes
                var rootContent = _contentService.GetRootContent();
                foreach (var root in rootContent)
                {
                    var children = _contentService.GetPagedChildren(root.Id, 0, 1000, out var childrenTotal)
                        .Where(x => x.ContentType.Alias == "contentItem");
                    allContent.AddRange(children);
                }

                var allItems = allContent
                    .Select(MapToContentItemModel)
                    .ToList();

                return Ok(allItems);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error retrieving all content", error = ex.Message });
            }
        }

        [HttpPost("approve/{id}")]
        public IActionResult ApproveContent(int id, [FromBody] ApprovalRequest request)
        {
            try
            {
                var content = _contentService.GetById(id);
                if (content == null)
                    return NotFound();

                content.SetValue("status", "Approved");
                content.SetValue("approvedBy", request.ApprovedBy);
                content.SetValue("approvalDate", DateTime.Now);

                var result = _contentService.SaveAndPublish(content);
                
                if (result.Success)
                    return Ok(new { message = "Content approved and published successfully" });
                else
                    return BadRequest(new { message = "Failed to approve content", errors = result.EventMessages?.GetAll().Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error approving content", error = ex.Message });
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

                content.SetValue("status", "Rejected");
                content.SetValue("approvedBy", request.ApprovedBy);
                content.SetValue("approvalDate", DateTime.Now);

                var result = _contentService.Save(content);
                
                if (result.Success)
                    return Ok(new { message = "Content rejected successfully" });
                else
                    return BadRequest(new
                    {
                        message = "Failed to reject content",
                        errors = result.EventMessages?.GetAll().Select(e => e.Message)
                    });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "Error rejecting content", error = ex.Message });
            }
        }

        private ContentItemModel MapToContentItemModel(IPublishedContent content)
        {
            return new ContentItemModel
            {
                Id = content.Id,
                Title = content.Value<string>("title") ?? "",
                ParagraphContent = content.Value<string>("paragraphContent") ?? "",
                ImageUrl = content.Value<IPublishedContent>("image")?.Url() ?? "",
                Status = content.Value<string>("status") ?? "",
                CreatedBy = content.Value<string>("createdBy") ?? "",
                ApprovedBy = content.Value<string>("approvedBy") ?? "",
                ApprovalDate = content.Value<DateTime?>("approvalDate"),
                CreateDate = content.CreateDate
            };
        }

        private ContentItemModel MapToContentItemModel(IContent content)
        {
            return new ContentItemModel
            {
                Id = content.Id,
                Title = content.GetValue<string>("title") ?? "",
                ParagraphContent = content.GetValue<string>("paragraphContent") ?? "",
                ImageUrl = ResolveImageUrl(content.GetValue("image")),
                Status = content.GetValue<string>("status") ?? "",
                CreatedBy = content.GetValue<string>("createdBy") ?? "",
                ApprovedBy = content.GetValue<string>("approvedBy") ?? "",
                ApprovalDate = content.GetValue<DateTime?>("approvalDate"),
                CreateDate = content.CreateDate
            };
        }

        private string ResolveImageUrl(object? imageValue)
        {
            if (imageValue == null)
                return "";

            var imageString = imageValue.ToString();
            if (string.IsNullOrEmpty(imageString))
                return "";

            try
            {
                // Try to parse as UDI string first
                if (imageString.StartsWith("umb://media/"))
                {
                    if (UdiParser.TryParse(imageString, out var udi))
                    {
                        var mediaItem = _publishedContentQuery.Content(udi);
                        return mediaItem?.Url() ?? "";
                    }
                }

                // Try to parse as JSON (newer media picker format)
                if (imageString.StartsWith("[") || imageString.StartsWith("{"))
                {
                    try
                    {
                        var jsonElement = JsonSerializer.Deserialize<JsonElement>(imageString);
                        if (jsonElement.ValueKind == JsonValueKind.Array && jsonElement.GetArrayLength() > 0)
                        {
                            var firstItem = jsonElement[0];
                            if (firstItem.TryGetProperty("udi", out var udiElement))
                            {
                                var udiString = udiElement.GetString();
                                if (!string.IsNullOrEmpty(udiString) && UdiParser.TryParse(udiString, out var jsonUdi))
                                {
                                    var mediaItem = _publishedContentQuery.Content(jsonUdi);
                                    return mediaItem?.Url() ?? "";
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, continue to integer parsing
                    }
                }

                // Try to parse as integer ID (legacy)
                if (int.TryParse(imageString, out var id))
                {
                    var mediaItem = _publishedContentQuery.Content(id);
                    return mediaItem?.Url() ?? "";
                }
            }
            catch
            {
                // If all parsing fails, return empty string
            }

            return "";
        }
    }

    public class ApprovalRequest
    {
        public string ApprovedBy { get; set; } = "";
    }
}