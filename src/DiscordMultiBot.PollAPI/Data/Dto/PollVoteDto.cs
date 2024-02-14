namespace DiscordMultiBot.PollService.Data.Dto;

public record PollVoteDto(ulong Id, ulong UserId, ulong PollId, string VoteData);