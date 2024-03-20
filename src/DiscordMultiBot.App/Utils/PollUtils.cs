using DiscordMultiBot.App.Data;
using DiscordMultiBot.PollService.Data.Dto;
using Newtonsoft.Json;

namespace DiscordMultiBot.App.Utils;

public static class PollUtils
{
    public static string PollVoteDataByTypeToString(string voteData, PollType type)
    {
        switch (type)
        {
            case PollType.Binary:
            {
                var json = JsonConvert.DeserializeObject<PollDataYesNo>(voteData);
                if (json is not null)
                {
                    return json.Value ? "yes" : "no";
                }
            }
                break;

            case PollType.Numeric:
            {
                var json = JsonConvert.DeserializeObject<PollDataPreference>(voteData);
                if (json is not null)
                {
                    return json.Preference.ToString();
                }
            }
                break;
        }

        return "";
    }
}