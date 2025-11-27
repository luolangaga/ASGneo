using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ASG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TeamsController : ControllerBase
    {
        private readonly ITeamService _teamService;
        private readonly IEventService _eventService;
        private readonly ILogger<TeamsController> _logger;
        private readonly IWebHostEnvironment _env;

        public TeamsController(ITeamService teamService, IEventService eventService, ILogger<TeamsController> logger, IWebHostEnvironment env)
        {
            _teamService = teamService;
            _eventService = eventService;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// 获取所有战队列表（分页）
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<TeamDto>>> GetAllTeams([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _teamService.GetAllTeamsAsync(page, pageSize);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取战队列表时发生错误");
                return StatusCode(500, "获取战队列表失败");
            }
        }

        /// <summary>
        /// 获取战队荣誉（该战队获得冠军的赛事列表）
        /// </summary>
        /// <param name="id">战队ID</param>
        /// <returns>赛事DTO列表</returns>
        [HttpGet("{id}/honors")]
        public async Task<ActionResult<IEnumerable<EventDto>>> GetTeamHonors(Guid id)
        {
            try
            {
                var events = await _eventService.GetChampionEventsByTeamAsync(id);
                foreach (var e in events)
                {
                    e.LogoUrl = GetEventLogoUrl(e.Id);
                }
                return Ok(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取战队荣誉失败，战队ID: {TeamId}", id);
                return StatusCode(500, new { message = "获取战队荣誉失败", error = ex.Message });
            }
        }

        private string? GetEventLogoUrl(Guid eventId)
        {
            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var eventDir = Path.Combine(root, "event-logos", eventId.ToString());
            if (!Directory.Exists(eventDir)) return null;
            var files = Directory.GetFiles(eventDir, "logo.*");
            if (files.Length == 0) return null;
            var fileName = Path.GetFileName(files[0]);
            var relativePath = $"/event-logos/{eventId}/{fileName}";
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            if (!string.IsNullOrEmpty(host))
            {
                return $"{scheme}://{host}{relativePath}";
            }
            return relativePath;
        }

        /// <summary>
        /// 通过名称搜索战队（模糊匹配，分页）
        /// </summary>
        [HttpGet("search")]
        public async Task<ActionResult<PagedResult<TeamDto>>> SearchTeamsByName([FromQuery] string name, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _teamService.SearchTeamsByNameAsync(name, page, pageSize);
                // 为每个结果设置徽标URL（如果存在）
                foreach (var item in result.Items)
                {
                    item.LogoUrl = GetTeamLogoUrl(item.Id);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "搜索战队时发生错误，关键词: {Keyword}", name);
                return StatusCode(500, "搜索战队失败");
            }
        }

        /// <summary>
        /// 根据ID获取战队详情
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<TeamDto>> GetTeam(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var team = await _teamService.GetTeamByIdAsync(id, userId);
                if (team == null)
                {
                    return NotFound("战队不存在");
                }
                team.LogoUrl = GetTeamLogoUrl(id);
                return Ok(team);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取战队详情时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, "获取战队详情失败");
            }
        }

        /// <summary>
        /// 创建新战队（需要登录，自动绑定到当前用户）
        /// </summary>
        [HttpPost("register")]
        [Authorize]
        public async Task<ActionResult<TeamDto>> RegisterTeam([FromBody] CreateTeamDto createTeamDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // 获取当前登录用户ID
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("无法获取用户信息");
                }

                var team = await _teamService.CreateTeamAsync(createTeamDto, userId);
                team.LogoUrl = GetTeamLogoUrl(team.Id);
                return CreatedAtAction(nameof(GetTeam), new { id = team.Id }, team);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建战队时发生错误");
                return StatusCode(500, "创建战队失败");
            }
        }

        /// <summary>
        /// 更新战队信息（需要登录且为战队拥有者）
        /// </summary>
        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult<TeamDto>> UpdateTeam(Guid id, [FromBody] UpdateTeamDto updateTeamDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var team = await _teamService.UpdateTeamAsync(id, updateTeamDto, userId);
                team.LogoUrl = GetTeamLogoUrl(team.Id);
                return Ok(team);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新战队时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, "更新战队失败");
            }
        }

        /// <summary>
        /// 删除战队（需要登录且为战队拥有者或管理员）
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> DeleteTeam(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                // 检查是否为管理员
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var isAdmin = userRole == UserRole.Admin.ToString() || userRole == UserRole.SuperAdmin.ToString();

                var result = await _teamService.DeleteTeamAsync(id, userId, isAdmin);
                if (!result)
                {
                    return NotFound("战队不存在");
                }

                return NoContent();
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "删除战队时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, "删除战队失败");
            }
        }

        /// <summary>
        /// 绑定战队（需要登录）
        /// </summary>
        [HttpPost("bind")]
        [Authorize]
        public async Task<ActionResult> BindTeam([FromBody] TeamBindDto bindDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.BindTeamAsync(bindDto.TeamId, bindDto.Password, userId);
                if (!result)
                {
                    return BadRequest("战队不存在或密码错误");
                }

                var my = await _teamService.GetMyPlayerAsync(userId);
                var needsPlayer = my == null;
                var playerAdded = !needsPlayer;
                return Ok(new { message = "战队绑定成功", playerAdded, needsPlayer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "绑定战队时发生错误，战队ID: {TeamId}", bindDto.TeamId);
                return StatusCode(500, "绑定战队失败");
            }
        }

        /// <summary>
        /// 通过战队名称绑定（需要登录）
        /// </summary>
        [HttpPost("bind-by-name")]
        [Authorize]
        public async Task<ActionResult> BindTeamByName([FromBody] TeamBindByNameDto bindByNameDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.BindTeamByNameAsync(bindByNameDto.Name, bindByNameDto.Password, userId);
                if (!result)
                {
                    return BadRequest("战队不存在或密码错误");
                }

                var my = await _teamService.GetMyPlayerAsync(userId);
                var needsPlayer = my == null;
                var playerAdded = !needsPlayer;
                return Ok(new { message = "战队绑定成功", playerAdded, needsPlayer });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通过名称绑定战队时发生错误，战队名称: {Name}", bindByNameDto.Name);
                return StatusCode(500, "绑定战队失败");
            }
        }

        /// <summary>
        /// 解绑战队（需要登录）
        /// </summary>
        [HttpPost("unbind")]
        [Authorize]
        public async Task<ActionResult> UnbindTeam()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.UnbindTeamAsync(userId);
                if (!result)
                {
                    return BadRequest("当前用户尚未绑定战队或用户不存在");
                }

                return Ok(new { message = "战队解绑成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "解绑战队时发生错误");
                return StatusCode(500, "解绑战队失败");
            }
        }

        /// <summary>
        /// 修改战队密码（需要登录且为战队拥有者）
        /// </summary>
        [HttpPost("{id}/change-password")]
        [Authorize]
        public async Task<ActionResult> ChangeTeamPassword(Guid id, [FromBody] ChangeTeamPasswordDto changePasswordDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.ChangeTeamPasswordAsync(id, changePasswordDto, userId);
                if (!result)
                {
                    return NotFound("战队不存在");
                }

                return Ok(new { message = "密码修改成功" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "修改战队密码时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, "修改密码失败");
            }
        }

        [HttpPost("{id}/transfer-owner")]
        [Authorize]
        public async Task<ActionResult> TransferOwner(Guid id, [FromBody] TransferOwnerDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized("用户未登录");
                var ok = await _teamService.TransferTeamOwnershipAsync(id, userId, dto.TargetUserId);
                if (!ok) return BadRequest(new { message = "转移失败" });
                return Ok(new { message = "已转移队长" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转移队长时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, new { message = "转移失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 验证战队所有权（需要登录）
        /// </summary>
        [HttpGet("{id}/verify-ownership")]
        [Authorize]
        public async Task<ActionResult<bool>> VerifyTeamOwnership(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                var result = await _teamService.VerifyTeamOwnershipAsync(id, userId);
                return Ok(new { isOwner = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证战队所有权时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, "验证失败");
            }
        }

        /// <summary>
        /// 给战队点赞
        /// </summary>
        [HttpPost("{id}/like")]
        public async Task<ActionResult> LikeTeam(Guid id)
        {
            try
            {
                var likes = await _teamService.LikeTeamAsync(id);
                return Ok(new { likes });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "给战队点赞时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, "点赞失败");
            }
        }

        [HttpGet("{id}/reviews")]
        public ActionResult<IEnumerable<TeamReviewDto>> GetTeamReviews(Guid id)
        {
            return Ok(Array.Empty<TeamReviewDto>());
        }

        [HttpPost("{id}/reviews")]
        [Authorize]
        public ActionResult AddTeamReview(Guid id)
        {
            return StatusCode(410, new { message = "评论功能已下线" });
        }

        public class SetDisputeDto { public bool HasDispute { get; set; } public string? DisputeDetail { get; set; } public Guid? CommunityPostId { get; set; } }

        [HttpPost("{id}/dispute")]
        [Authorize]
        public async Task<ActionResult> SetDispute(Guid id, [FromBody] SetDisputeDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized("用户未登录");
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var isAdmin = userRole == UserRole.Admin.ToString() || userRole == UserRole.SuperAdmin.ToString();
                var canManage = isAdmin || await _eventService.CanUserManageAnyEventOfTeamAsync(id, userId);
            if (!canManage) return StatusCode(403, new { message = "无权设置纠纷状态" });
                if (dto.HasDispute && !dto.CommunityPostId.HasValue)
                {
                    return BadRequest(new { message = "必须绑定社区帖子才能标记为纠纷" });
                }
                var ok = await _teamService.SetTeamDisputeAsync(id, dto.HasDispute, dto.DisputeDetail, dto.CommunityPostId);
                if (!ok) return NotFound(new { message = "战队不存在" });
                return Ok(new { hasDispute = dto.HasDispute, disputeDetail = dto.DisputeDetail, communityPostId = dto.CommunityPostId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "设置战队纠纷状态失败，战队ID: {TeamId}", id);
                return StatusCode(500, new { message = "设置失败", error = ex.Message });
            }
        }

        [HttpPost("{id}/invite")]
        [Authorize]
        public async Task<ActionResult<TeamInviteDto>> GenerateInvite(Guid id, [FromQuery] int validDays = 7)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized("用户未登录");
                var dto = await _teamService.GenerateTeamInviteAsync(id, userId, validDays);
                return Ok(dto);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成邀请链接失败，战队ID: {TeamId}", id);
                return StatusCode(500, new { message = "生成邀请失败", error = ex.Message });
            }
        }

        [HttpGet("invites/{token}")]
        public async Task<ActionResult<TeamInviteDto>> GetInvite(Guid token)
        {
            try
            {
                var dto = await _teamService.GetTeamInviteAsync(token);
                if (dto == null) return NotFound(new { message = "邀请不存在或已过期" });
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取邀请信息失败，Token: {Token}", token);
                return StatusCode(500, new { message = "获取邀请失败", error = ex.Message });
            }
        }

        [HttpPost("invites/{token}/accept")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> AcceptInvite(Guid token, [FromBody] CreatePlayerDto? player)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized("用户未登录");
                var result = await _teamService.AcceptTeamInviteAsync(token, userId, player);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "接受邀请失败，Token: {Token}", token);
                return StatusCode(500, new { message = "接受邀请失败", error = ex.Message });
            }
        }

        [HttpGet("me/player")]
        [Authorize]
        public async Task<ActionResult<PlayerDto?>> GetMyPlayer()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }
                var dto = await _teamService.GetMyPlayerAsync(userId);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取我的玩家失败");
                return StatusCode(500, new { message = "获取玩家失败", error = ex.Message });
            }
        }

        [HttpPost("me/player")]
        [Authorize]
        public async Task<ActionResult<PlayerDto>> UpsertMyPlayer([FromBody] CreatePlayerDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }
                var result = await _teamService.UpsertMyPlayerAsync(userId, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "保存我的玩家失败");
                return StatusCode(500, new { message = "保存玩家失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 退出当前战队（移除当前用户在该战队的玩家记录，并解除绑定）
        /// </summary>
        [HttpPost("{id}/leave")]
        [Authorize]
        public async Task<ActionResult> LeaveTeam(Guid id)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "用户未登录" });
                }

                var ok = await _teamService.LeaveTeamAsync(id, userId);
                if (!ok)
                {
                    return BadRequest(new { message = "退出失败" });
                }
                return Ok(new { message = "已退出战队" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退出战队失败，战队ID: {TeamId}", id);
                return StatusCode(500, new { message = "退出战队失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 上传战队徽标（需要登录且为战队拥有者）
        /// </summary>
        [HttpPost("{id}/logo")]
        [Authorize]
        public async Task<IActionResult> UploadTeamLogo(Guid id, [FromForm] IFormFile? logo)
        {
            try
            {
                if (logo == null || logo.Length == 0)
                {
                    return BadRequest(new { message = "请选择要上传的徽标文件" });
                }

                var allowedExts = new[] { ".png", ".jpg", ".jpeg", ".webp" };
                var ext = Path.GetExtension(logo.FileName).ToLowerInvariant();
                if (!allowedExts.Contains(ext))
                {
                    return BadRequest(new { message = "仅支持 png/jpg/jpeg/webp 格式" });
                }

                const long maxSize = 5 * 1024 * 1024; // 5MB
                if (logo.Length > maxSize)
                {
                    return BadRequest(new { message = "文件大小不能超过 5MB" });
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("用户未登录");
                }

                // 验证所有权
                var isOwner = await _teamService.VerifyTeamOwnershipAsync(id, userId);
                if (!isOwner)
                {
                    return StatusCode(403, new { message = "您没有权限上传该战队的徽标" });
                }

                var root = _env.WebRootPath;
                if (string.IsNullOrEmpty(root))
                {
                    root = Path.Combine(_env.ContentRootPath, "wwwroot");
                }

                var teamDir = Path.Combine(root, "team-logos", id.ToString());
                Directory.CreateDirectory(teamDir);

                foreach (var file in Directory.GetFiles(teamDir, "logo.*"))
                {
                    try { System.IO.File.Delete(file); } catch { /* ignore */ }
                }

                var filePath = Path.Combine(teamDir, $"logo{ext}");
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await logo.CopyToAsync(stream);
                }

                var url = GetTeamLogoUrl(id);
                return Ok(new { logoUrl = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "上传战队徽标时发生错误，战队ID: {TeamId}", id);
                return StatusCode(500, new { message = "上传徽标失败", error = ex.Message });
            }
        }

        public class LogoFromUrlDto
        {
            public string SourceUrl { get; set; } = string.Empty;
        }

        [HttpPost("{id}/logo-from-url")]
        [Authorize]
        public async Task<IActionResult> UploadTeamLogoFromUrl(Guid id, [FromBody] LogoFromUrlDto dto)
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId)) return Unauthorized("用户未登录");
                var isOwner = await _teamService.VerifyTeamOwnershipAsync(id, userId);
                if (!isOwner) return StatusCode(403, new { message = "您没有权限上传该战队的徽标" });
                if (string.IsNullOrWhiteSpace(dto?.SourceUrl)) return BadRequest(new { message = "缺少来源" });
                var root = _env.WebRootPath;
                if (string.IsNullOrEmpty(root)) root = Path.Combine(_env.ContentRootPath, "wwwroot");
                var uri = new Uri(dto.SourceUrl, UriKind.RelativeOrAbsolute);
                string? srcPath = null;
                if (!uri.IsAbsoluteUri)
                {
                    srcPath = Path.Combine(root, uri.OriginalString.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                }
                else
                {
                    if (uri.Host == (Request.Host.HasValue ? Request.Host.Host : string.Empty))
                    {
                        srcPath = Path.Combine(root, uri.AbsolutePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    }
                }
                if (string.IsNullOrEmpty(srcPath) || !System.IO.File.Exists(srcPath)) return BadRequest(new { message = "来源不可用" });
                var teamDir = Path.Combine(root, "team-logos", id.ToString());
                Directory.CreateDirectory(teamDir);
                foreach (var file in Directory.GetFiles(teamDir, "logo.*")) { try { System.IO.File.Delete(file); } catch { } }
                var ext = Path.GetExtension(srcPath);
                if (string.IsNullOrWhiteSpace(ext)) ext = ".png";
                var filePath = Path.Combine(teamDir, $"logo{ext.ToLowerInvariant()}");
                await using (var src = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (var dst = new FileStream(filePath, FileMode.Create))
                {
                    await src.CopyToAsync(dst);
                }
                var url = GetTeamLogoUrl(id);
                return Ok(new { logoUrl = url });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "通过URL设置战队徽标失败，战队ID: {TeamId}", id);
                return StatusCode(500, new { message = "上传徽标失败", error = ex.Message });
            }
        }

        private string? GetTeamLogoUrl(Guid teamId)
        {
            var root = _env.WebRootPath;
            if (string.IsNullOrEmpty(root))
            {
                root = Path.Combine(_env.ContentRootPath, "wwwroot");
            }
            var teamDir = Path.Combine(root, "team-logos", teamId.ToString());
            if (!Directory.Exists(teamDir)) return null;
            var files = Directory.GetFiles(teamDir, "logo.*");
            if (files.Length == 0) return null;
            var fileName = Path.GetFileName(files[0]);
            var relativePath = $"/team-logos/{teamId}/{fileName}";
            var scheme = Request.Scheme;
            var host = Request.Host.HasValue ? Request.Host.Value : string.Empty;
            if (!string.IsNullOrEmpty(host))
            {
                return $"{scheme}://{host}{relativePath}";
            }
            return relativePath;
        }
    }
}
