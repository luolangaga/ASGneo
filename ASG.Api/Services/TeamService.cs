using ASG.Api.DTOs;
using ASG.Api.Models;
using ASG.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ASG.Api.Services
{
    public class TeamService : ITeamService
    {
        private readonly ITeamRepository _teamRepository;
        private readonly IUserRepository _userRepository;

        public TeamService(ITeamRepository teamRepository, IUserRepository userRepository)
        {
            _teamRepository = teamRepository;
            _userRepository = userRepository;
        }

        public async Task<PagedResult<TeamDto>> GetAllTeamsAsync(int page = 1, int pageSize = 10)
        {
            var teams = await _teamRepository.GetAllTeamsAsync(page, pageSize);
            var totalCount = await _teamRepository.GetTeamCountAsync();

            return new PagedResult<TeamDto>
            {
                Items = teams.Select(MapToTeamDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<TeamDto>> SearchTeamsByNameAsync(string name, int page = 1, int pageSize = 10)
        {
            var teams = await _teamRepository.SearchTeamsByNameAsync(name, page, pageSize);
            var totalCount = await _teamRepository.GetSearchTeamCountAsync(name);

            return new PagedResult<TeamDto>
            {
                Items = teams.Select(MapToTeamDto),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<TeamDto?> GetTeamByIdAsync(Guid id)
        {
            var team = await _teamRepository.GetTeamByIdWithPlayersAsync(id);
            return team != null ? MapToTeamDto(team) : null;
        }

        public async Task<TeamDto> CreateTeamAsync(CreateTeamDto createTeamDto, string? userId = null)
        {
            // 检查战队名称是否已存在
            if (await _teamRepository.TeamNameExistsAsync(createTeamDto.Name))
            {
                throw new InvalidOperationException("战队名称已存在");
            }

            var team = new Team
            {
                Id = Guid.NewGuid(),
                Name = createTeamDto.Name,
                Password = BCrypt.Net.BCrypt.HashPassword(createTeamDto.Password),
                Description = createTeamDto.Description,
                // 记录创建者（兼容旧字段）
                UserId = userId,
                OwnerId = userId,
                Players = createTeamDto.Players.Select(p => new Player
                {
                    Id = Guid.NewGuid(),
                    Name = p.Name,
                    GameId = p.GameId,
                    GameRank = p.GameRank,
                    Description = p.Description
                }).ToList()
            };

            var createdTeam = await _teamRepository.CreateTeamAsync(team);

            // 自动将用户绑定到新创建的战队（建立 User.TeamId -> Team.Id 的一对一关系）
            if (!string.IsNullOrEmpty(userId))
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null && user.TeamId != createdTeam.Id)
                {
                    user.TeamId = createdTeam.Id;
                    await _userRepository.UpdateAsync(user);
                }
            }

            return MapToTeamDto(createdTeam);
        }

        public async Task<TeamDto> UpdateTeamAsync(Guid id, UpdateTeamDto updateTeamDto, string? userId = null)
        {
            var team = await _teamRepository.GetTeamByIdWithPlayersAsync(id);
            if (team == null)
            {
                throw new InvalidOperationException("战队不存在");
            }

            // 如果提供了用户ID，验证用户是否为战队拥有者
            if (!string.IsNullOrEmpty(userId))
            {
                if (!await VerifyTeamOwnershipAsync(id, userId))
                {
                    throw new UnauthorizedAccessException("您没有权限修改此战队");
                }
            }

            // 检查战队名称是否已被其他战队使用
            if (await _teamRepository.TeamNameExistsAsync(updateTeamDto.Name, id))
            {
                throw new InvalidOperationException("战队名称已存在");
            }

            team.Name = updateTeamDto.Name;
            team.Description = updateTeamDto.Description;

            // 更新玩家信息
            await UpdatePlayersAsync(team, updateTeamDto.Players);

            var updatedTeam = await _teamRepository.UpdateTeamAsync(team);
            return MapToTeamDto(updatedTeam);
        }

        public async Task<bool> DeleteTeamAsync(Guid id, string? userId = null, bool isAdmin = false)
        {
            var team = await _teamRepository.GetTeamByIdAsync(id);
            if (team == null)
            {
                return false;
            }

            // 如果不是管理员且提供了用户ID，验证用户是否为战队拥有者
            if (!isAdmin && !string.IsNullOrEmpty(userId))
            {
                if (!await VerifyTeamOwnershipAsync(id, userId))
                {
                    throw new UnauthorizedAccessException("您没有权限删除此战队");
                }
            }

            return await _teamRepository.DeleteTeamAsync(id);
        }

        public async Task<bool> BindTeamAsync(Guid teamId, string password, string userId)
        {
            // 验证战队密码
            if (!await _teamRepository.VerifyTeamPasswordAsync(teamId, password))
            {
                return false;
            }

            // 获取用户
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return false;
            }

            // 绑定战队
            user.TeamId = teamId;
            await _userRepository.UpdateAsync(user);

            return true;
        }

        public async Task<bool> BindTeamByNameAsync(string name, string password, string userId)
        {
            var team = await _teamRepository.GetTeamByNameAsync(name);
            if (team == null) return false;

            // 验证密码
            if (!BCrypt.Net.BCrypt.Verify(password, team.Password))
            {
                return false;
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;

            user.TeamId = team.Id;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> UnbindTeamAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null) return false;
            if (user.TeamId == null) return false;

            user.TeamId = null;
            await _userRepository.UpdateAsync(user);
            return true;
        }

        public async Task<bool> ChangeTeamPasswordAsync(Guid teamId, ChangeTeamPasswordDto changePasswordDto, string userId)
        {
            // 验证用户是否为战队拥有者
            if (!await VerifyTeamOwnershipAsync(teamId, userId))
            {
                throw new UnauthorizedAccessException("您没有权限修改此战队密码");
            }

            var team = await _teamRepository.GetTeamByIdAsync(teamId);
            if (team == null)
            {
                return false;
            }

            // 验证当前密码
            if (!BCrypt.Net.BCrypt.Verify(changePasswordDto.CurrentPassword, team.Password))
            {
                throw new InvalidOperationException("当前密码不正确");
            }

            // 更新密码
            team.Password = BCrypt.Net.BCrypt.HashPassword(changePasswordDto.NewPassword);
            await _teamRepository.UpdateTeamAsync(team);

            return true;
        }

        public async Task<bool> VerifyTeamOwnershipAsync(Guid teamId, string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            return user != null && user.TeamId == teamId;
        }

        public async Task<bool> TeamExistsAsync(Guid id)
        {
            return await _teamRepository.TeamExistsAsync(id);
        }

        private async Task UpdatePlayersAsync(Team team, List<UpdatePlayerDto>? updatePlayerDtos)
        {
            // 空列表保护：如果未提供玩家列表，则不对现有玩家做任何修改
            if (updatePlayerDtos == null || updatePlayerDtos.Count == 0)
            {
                return;
            }

            // 仅更新或新增，不删除未出现在请求中的玩家，避免因前端未传全量列表导致误删与约束错误
            foreach (var updatePlayerDto in updatePlayerDtos)
            {
                if (updatePlayerDto.Id.HasValue)
                {
                    var existingPlayer = team.Players.FirstOrDefault(p => p.Id == updatePlayerDto.Id.Value);
                    if (existingPlayer != null)
                    {
                        existingPlayer.Name = updatePlayerDto.Name;
                        existingPlayer.GameId = updatePlayerDto.GameId;
                        existingPlayer.GameRank = updatePlayerDto.GameRank;
                        existingPlayer.Description = updatePlayerDto.Description;
                        existingPlayer.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // 前端可能为新玩家生成了临时ID（或玩家已被并发删除），
                        // 将其按“新增”处理以避免并发更新冲突
                        var addPlayer = new Player
                        {
                            Id = Guid.NewGuid(),
                            Name = updatePlayerDto.Name,
                            GameId = updatePlayerDto.GameId,
                            GameRank = updatePlayerDto.GameRank,
                            Description = updatePlayerDto.Description,
                            TeamId = team.Id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        team.Players.Add(addPlayer);
                    }
                }
                else
                {
                    var newPlayer = new Player
                    {
                        Id = Guid.NewGuid(),
                        Name = updatePlayerDto.Name,
                        GameId = updatePlayerDto.GameId,
                        GameRank = updatePlayerDto.GameRank,
                        Description = updatePlayerDto.Description,
                        TeamId = team.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    team.Players.Add(newPlayer);
                }
            }
        }

        public async Task<int> LikeTeamAsync(Guid id)
        {
            return await _teamRepository.LikeTeamAsync(id);
        }

        private static TeamDto MapToTeamDto(Team team)
        {
            return new TeamDto
            {
                Id = team.Id,
                Name = team.Name,
                Description = team.Description,
                CreatedAt = team.CreatedAt,
                UpdatedAt = team.UpdatedAt,
                Likes = team.Likes,
                Players = team.Players.Select(p => new PlayerDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    GameId = p.GameId,
                    GameRank = p.GameRank,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    TeamId = p.TeamId
                }).ToList()
            };
        }
    }
}