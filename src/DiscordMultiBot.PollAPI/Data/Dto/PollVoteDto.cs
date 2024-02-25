namespace DiscordMultiBot.PollService.Data.Dto;

public enum PollVoterState
{
    NotReady,
    Ready
}

public record PollVoteDto(ulong Id, ulong UserId, ulong PollId, string VoteOption, string VoteData);
public record PollVoterStateDto(ulong Id, ulong UserId, ulong PollId, PollVoterState VoterState);
public record PollVoteResultDto(ulong PollId, ulong ChannelId, ulong UserId, string VoteOption, string VoteData, PollVoterState VoterState);