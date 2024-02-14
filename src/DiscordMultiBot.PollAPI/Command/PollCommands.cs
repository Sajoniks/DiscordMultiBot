using DiscordMultiBot.PollService.Data.Dto;

namespace DiscordMultiBot.PollService.Command;

public enum PollCommandAction
{
    Create,
    Remove
}

public record PollCommand(PollCommandAction Action, PollOptions Options) : ICommand;