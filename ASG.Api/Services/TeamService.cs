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
        private readonly INotificationService _notify;

        public TeamService(ITeamRepository teamRepository, IUserRepository userRepository, INotificationService notify)
        {
            _teamRepository = teamRepository;
            _userRepository = userRepository;
            _notify = notify;
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

            var payloadPlayers = createTeamDto.Players ?? new List<CreatePlayerDto>();
            var playersToCreate = new List<Player>();
            Player? creatorExistingPlayer = null;
            CreatePlayerDto? firstPayload = payloadPlayers.FirstOrDefault();

            if (!string.IsNullOrEmpty(userId) && firstPayload != null)
            {
                creatorExistingPlayer = await _teamRepository.GetPlayerByUserIdAsync(userId);
                if (creatorExistingPlayer == null)
                {
                    playersToCreate.Add(new Player
                    {
                        Id = Guid.NewGuid(),
                        Name = firstPayload.Name,
                        GameId = firstPayload.GameId,
                        GameRank = firstPayload.GameRank,
                        Description = firstPayload.Description,
                        UserId = userId
                    });
                }
                // 若已有玩家，则不在创建列表中重复创建，稍后迁移并更新信息
                foreach (var p in payloadPlayers.Skip(1))
                {
                    playersToCreate.Add(new Player
                    {
                        Id = Guid.NewGuid(),
                        Name = p.Name,
                        GameId = p.GameId,
                        GameRank = p.GameRank,
                        Description = p.Description
                    });
                }
            }
            else
            {
                playersToCreate = payloadPlayers.Select(p => new Player
                {
                    Id = Guid.NewGuid(),
                    Name = p.Name,
                    GameId = p.GameId,
                    GameRank = p.GameRank,
                    Description = p.Description
                }).ToList();
            }

            var team = new Team
            {
                Id = Guid.NewGuid(),
                Name = createTeamDto.Name,
                Password = BCrypt.Net.BCrypt.HashPassword(createTeamDto.Password),
                Description = createTeamDto.Description,
                QqNumber = createTeamDto.QqNumber,
                // 记录创建者（兼容旧字段）
                UserId = userId,
                OwnerId = userId,
                Players = playersToCreate
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
                // 如果创建者已有玩家，则迁移到该战队并更新为第一个队员信息
                if (creatorExistingPlayer != null && firstPayload != null)
                {
                    creatorExistingPlayer.Name = firstPayload.Name;
                    creatorExistingPlayer.GameId = firstPayload.GameId;
                    creatorExistingPlayer.GameRank = firstPayload.GameRank;
                    creatorExistingPlayer.Description = firstPayload.Description;
                    creatorExistingPlayer.TeamId = createdTeam.Id;
                    creatorExistingPlayer.UpdatedAt = DateTime.UtcNow;
                    await _teamRepository.UpdatePlayerAsync(creatorExistingPlayer);
                }
            }

            var dto = MapToTeamDto(createdTeam);
            if (!string.IsNullOrEmpty(userId))
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new { teamId = dto.Id, teamName = dto.Name });
                await _notify.NotifyUserAsync(userId, "team.created", payload);
            }
            return dto;
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
            if (!string.IsNullOrWhiteSpace(updateTeamDto.QqNumber))
            {
                team.QqNumber = updateTeamDto.QqNumber;
            }

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

            // 将“我的玩家”添加/迁移到该战队（如果存在）
            var myPlayer = await _teamRepository.GetPlayerByUserIdAsync(userId);
            if (myPlayer != null)
            {
                if (myPlayer.TeamId != teamId)
                {
                    myPlayer.TeamId = teamId;
                    myPlayer.UpdatedAt = DateTime.UtcNow;
                    await _teamRepository.UpdatePlayerAsync(myPlayer);
                }
            }

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

            // 将“我的玩家”添加/迁移到该战队（如果存在）
            var myPlayer = await _teamRepository.GetPlayerByUserIdAsync(userId);
            if (myPlayer != null)
            {
                if (myPlayer.TeamId != team.Id)
                {
                    myPlayer.TeamId = team.Id;
                    myPlayer.UpdatedAt = DateTime.UtcNow;
                    await _teamRepository.UpdatePlayerAsync(myPlayer);
                }
            }
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
            if (updatePlayerDtos == null)
            {
                return;
            }

            var incomingIds = updatePlayerDtos
                .Where(dto => dto.Id.HasValue)
                .Select(dto => dto.Id!.Value)
                .ToHashSet();

            var toRemove = team.Players
                .Where(p => !incomingIds.Contains(p.Id))
                .ToList();

            foreach (var removePlayer in toRemove)
            {
                team.Players.Remove(removePlayer);
            }

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
                QqNumber = team.QqNumber,
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

        public async Task<TeamInviteDto> GenerateTeamInviteAsync(Guid teamId, string userId, int validDays = 7)
        {
            var team = await _teamRepository.GetTeamByIdAsync(teamId);
            if (team == null)
            {
                throw new InvalidOperationException("战队不存在");
            }
            if (!await VerifyTeamOwnershipAsync(teamId, userId))
            {
                throw new UnauthorizedAccessException("您没有权限生成该战队的邀请链接");
            }

            team.InviteToken = Guid.NewGuid();
            team.InviteExpiresAt = DateTime.UtcNow.AddDays(Math.Max(1, validDays));
            await _teamRepository.UpdateTeamAsync(team);

            return new TeamInviteDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
                Token = team.InviteToken!.Value,
                ExpiresAt = team.InviteExpiresAt!.Value
            };
        }

        public async Task<TeamInviteDto?> GetTeamInviteAsync(Guid token)
        {
            var team = await _teamRepository.GetTeamByInviteTokenAsync(token);
            if (team == null || !team.InviteToken.HasValue || team.InviteToken != token)
            {
                return null;
            }
            var exp = team.InviteExpiresAt ?? DateTime.MinValue;
            if (DateTime.UtcNow > exp)
            {
                return null;
            }
            return new TeamInviteDto
            {
                TeamId = team.Id,
                TeamName = team.Name,
                Token = token,
                ExpiresAt = exp
            };
        }

        public async Task<PlayerDto> AcceptTeamInviteAsync(Guid token, string userId, CreatePlayerDto playerDto)
        {
            var team = await _teamRepository.GetTeamByInviteTokenAsync(token);
            if (team == null || !team.InviteToken.HasValue || team.InviteToken != token)
            {
                throw new InvalidOperationException("邀请无效");
            }
            var exp = team.InviteExpiresAt ?? DateTime.MinValue;
            if (DateTime.UtcNow > exp)
            {
                throw new InvalidOperationException("邀请已过期");
            }
            // 全局检查：该用户是否已有玩家（可选的一对一）
            var existingGlobal = await _teamRepository.GetPlayerByUserIdAsync(userId);
            if (existingGlobal != null)
            {
                // 如果已有玩家但不在当前战队，则迁移到当前战队
                if (existingGlobal.TeamId != team.Id)
                {
                    existingGlobal.TeamId = team.Id;
                    existingGlobal.UpdatedAt = DateTime.UtcNow;
                    await _teamRepository.UpdatePlayerAsync(existingGlobal);
                }

                // 将用户绑定到当前战队
                var user = await _userRepository.GetByIdAsync(userId);
                if (user != null && user.TeamId != team.Id)
                {
                    user.TeamId = team.Id;
                    await _userRepository.UpdateAsync(user);
                }

                return new PlayerDto
                {
                    Id = existingGlobal.Id,
                    Name = existingGlobal.Name,
                    GameId = existingGlobal.GameId,
                    GameRank = existingGlobal.GameRank,
                    Description = existingGlobal.Description,
                    CreatedAt = existingGlobal.CreatedAt,
                    UpdatedAt = existingGlobal.UpdatedAt,
                    TeamId = existingGlobal.TeamId
                };
            }

            // 否则，为该用户创建一个新玩家并加入战队
            if (playerDto == null)
            {
                throw new InvalidOperationException("请先创建玩家或填写玩家信息");
            }
            var newPlayer = new Player
            {
                Id = Guid.NewGuid(),
                Name = playerDto.Name,
                GameId = playerDto.GameId,
                GameRank = playerDto.GameRank,
                Description = playerDto.Description,
                TeamId = team.Id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            team.Players.Add(newPlayer);

            var updatedTeam = await _teamRepository.UpdateTeamAsync(team);

            // 绑定用户到战队（建立一对一的团队绑定关系）
            var bindUser = await _userRepository.GetByIdAsync(userId);
            if (bindUser != null && bindUser.TeamId != team.Id)
            {
                bindUser.TeamId = team.Id;
                await _userRepository.UpdateAsync(bindUser);
            }

            var created = updatedTeam.Players.First(p => p.Id == newPlayer.Id);
            var ownerUserId = team.OwnerId ?? team.UserId;
            if (!string.IsNullOrEmpty(ownerUserId))
            {
                var payload = System.Text.Json.JsonSerializer.Serialize(new { teamId = team.Id, teamName = team.Name, applicantUserId = userId, playerName = created.Name });
                await _notify.NotifyUserAsync(ownerUserId, "team.invite.submitted", payload);
            }
            return new PlayerDto
            {
                Id = created.Id,
                Name = created.Name,
                GameId = created.GameId,
                GameRank = created.GameRank,
                Description = created.Description,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt,
                TeamId = created.TeamId
            };
        }

        public async Task<bool> LeaveTeamAsync(Guid teamId, string userId)
        {
            var team = await _teamRepository.GetTeamByIdWithPlayersAsync(teamId);
            if (team == null)
            {
                throw new InvalidOperationException("战队不存在");
            }

            // 仅解绑：将当前用户在该战队的玩家的 TeamId 置空，不删除玩家
            var myPlayers = team.Players.Where(p => p.UserId == userId && p.TeamId == teamId).ToList();
            foreach (var p in myPlayers)
            {
                p.TeamId = null;
                p.UpdatedAt = DateTime.UtcNow;
                await _teamRepository.UpdatePlayerAsync(p);
            }

            // 同步解除用户绑定关系
            var user = await _userRepository.GetByIdAsync(userId);
            if (user != null && user.TeamId == teamId)
            {
                user.TeamId = null;
                await _userRepository.UpdateAsync(user);
            }

            return true;
        }

        public async Task<PlayerDto?> GetMyPlayerAsync(string userId)
        {
            var p = await _teamRepository.GetPlayerByUserIdAsync(userId);
            if (p == null) return null;
            return new PlayerDto
            {
                Id = p.Id,
                Name = p.Name,
                GameId = p.GameId,
                GameRank = p.GameRank,
                Description = p.Description,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                TeamId = p.TeamId
            };
        }

        public async Task<PlayerDto> UpsertMyPlayerAsync(string userId, CreatePlayerDto playerDto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException("用户不存在");
            }
            if (!user.TeamId.HasValue)
            {
                throw new InvalidOperationException("请先绑定或创建战队");
            }

            var existing = await _teamRepository.GetPlayerByUserIdAsync(userId);
            if (existing != null)
            {
                existing.Name = playerDto.Name;
                existing.GameId = playerDto.GameId;
                existing.GameRank = playerDto.GameRank;
                existing.Description = playerDto.Description;
                existing.UpdatedAt = DateTime.UtcNow;
                var updated = await _teamRepository.UpdatePlayerAsync(existing);
                return new PlayerDto
                {
                    Id = updated.Id,
                    Name = updated.Name,
                    GameId = updated.GameId,
                    GameRank = updated.GameRank,
                    Description = updated.Description,
                    CreatedAt = updated.CreatedAt,
                    UpdatedAt = updated.UpdatedAt,
                    TeamId = updated.TeamId
                };
            }

            var team = await _teamRepository.GetTeamByIdWithPlayersAsync(user.TeamId.Value);
            if (team == null)
            {
                throw new InvalidOperationException("战队不存在");
            }
            var newPlayer = new Player
            {
                Id = Guid.NewGuid(),
                Name = playerDto.Name,
                GameId = playerDto.GameId,
                GameRank = playerDto.GameRank,
                Description = playerDto.Description,
                TeamId = team.Id,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            team.Players.Add(newPlayer);
            var updatedTeam = await _teamRepository.UpdateTeamAsync(team);
            var created = updatedTeam.Players.First(p => p.Id == newPlayer.Id);
            return new PlayerDto
            {
                Id = created.Id,
                Name = created.Name,
                GameId = created.GameId,
                GameRank = created.GameRank,
                Description = created.Description,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt,
                TeamId = created.TeamId
            };
        }
    }
}