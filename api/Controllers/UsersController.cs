using Microsoft.AspNetCore.Mvc;
using TGitApi.Models;
using TGitApi.Services;

namespace TGitApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IStorageService _storageService;

    public UsersController(IStorageService storageService)
    {
        _storageService = storageService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] bool active = false, [FromQuery] string? tenant = null)
    {
        var users = active
            ? await _storageService.GetActiveUsersAsync(tenant)
            : await _storageService.GetAllUsersAsync(tenant);

        var serializedUsers = users.Select(user => new UserStatusDto
        {
            UserName = user.UserName,
            UserEmail = user.UserEmail,
            LastActivity = user.LastActivity,
            IsActive = _storageService.IsUserActive(user),
            Activities = user.Activities.Values.ToList()
        }).ToList();

        var response = new UsersResponse
        {
            Users = serializedUsers,
            TotalCount = serializedUsers.Count,
            ActiveCount = serializedUsers.Count(u => u.IsActive)
        };

        return Ok(response);
    }
}
