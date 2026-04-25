using Microsoft.AspNetCore.Mvc;
using TGitApi.Models;
using TGitApi.Services;

namespace TGitApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GitActivityController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly ILogger<GitActivityController> _logger;

    public GitActivityController(
        IStorageService storageService,
        ILogger<GitActivityController> logger
    )
    {
        _storageService = storageService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> RecordActivity([FromBody] GitActivity activity)
    {
        try
        {
            _logger.LogInformation("Received git activity: {Activity}", activity);
            // Validate required fields
            if (string.IsNullOrEmpty(activity.UserEmail) || string.IsNullOrEmpty(activity.RepoName))
            {
                return BadRequest(new ApiErrorResponse { Error = "Missing required fields" });
            }

            // Record the activity
            await _storageService.RecordActivityAsync(activity);

            return Ok(new ApiResponse { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing git activity");
            return BadRequest(new ApiErrorResponse { Error = "Invalid request" });
        }
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteTenant([FromQuery] string tenant)
    {
        if (string.IsNullOrEmpty(tenant))
        {
            return BadRequest(new ApiErrorResponse { Error = "Tenant is required" });
        }

        try
        {
            await _storageService.DeleteTenantAsync(tenant);
            return Ok(new ApiResponse { Success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tenant data");
            return BadRequest(new ApiErrorResponse { Error = "Failed to delete tenant data" });
        }
    }

    [HttpOptions]
    public IActionResult Options()
    {
        return NoContent();
    }
}
