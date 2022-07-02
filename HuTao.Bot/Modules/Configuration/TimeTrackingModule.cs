using Discord.Commands;
using HuTao.Data.Models.Authorization;
using HuTao.Services.Core.Preconditions.Commands;

namespace HuTao.Bot.Modules.Configuration;

[Name("Time Tracking")]
[Group("time")]
[Summary("Time tracking module.")]
[RequireAuthorization(AuthorizationScope.Configuration)]
public class TimeTrackingModule : ModuleBase<SocketCommandContext> { }