﻿using Discord.Interactions;
using DiscordMultiBot.App.EmbedXml;

namespace DiscordMultiBot.App.Modules.Misc;

public class MiscModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("roll", "Do a roll of a dice")]
    public async Task RollDiceAsync(
        [Summary("roll", "Dice notation")] string dice, 
        [Summary("class", "Complexity class"), MinValue(1), ] int cp = 0
    )
    {
        try
        {
            string[] notation = dice.Split('d');
            string[] value = notation[1].Split('+');

            int numDice = Int32.Parse(notation[0].Trim());
            int diceValue = Int32.Parse(value[0].Trim());;
            if (value.Length > 1)
            {
                for (int i = 1; i < value.Length; ++i)
                {
                    if (value[i].Length == 0) continue;
                    diceValue += Int32.Parse(value[i]);
                }
            }

            int minRoll = 1 * numDice;
            int maxRoll = diceValue * numDice;
            int roll = Random.Shared.Next(minRoll, maxRoll);

            EmbedXmlCreator creator = new();
            creator.Bindings.Add("Roll", roll.ToString());
            creator.Bindings.Add("MaxRoll", maxRoll.ToString());
            
            if (cp != 0)
            {
                creator.Bindings.Add("CP", cp.ToString());
                if (roll >= cp)
                {
                    creator.Bindings["Color"] = "08d31b";
                    creator.Bindings["Result"] = "Success";
                }
                else
                {
                    creator.Bindings["Color"] = "b10f0f";
                    creator.Bindings["Result"] = "Failure";
                }

                if (numDice == 1 && diceValue == 20)
                {
                    if (roll == maxRoll)
                    {
                        creator.Bindings["Result"] = "Critical success";
                    }
                    else if (roll == minRoll)
                    {
                        creator.Bindings["Result"] = "Critical failure";
                    }
                }

                await creator.Create("RollCp").RespondFromXmlAsync(Context);
            }
            else
            {
                int minRange = (int)(maxRoll * 0.33);
                int maxRange = (int)(maxRoll * 0.66);

                bool isMinRange = (roll >= minRoll) && (roll < minRange);
                bool isMidRange = (roll >= minRange) && (roll < maxRange);
                bool isMaxRange = (roll >= maxRange && roll <= maxRoll);

                string minColor = "b10f0f";
                string midColor = "e79b1e";
                string maxColor = "08d31b";

                string hexColor = "";
                if (isMinRange)
                {
                    hexColor = minColor;
                }
                else if (isMidRange)
                {
                    hexColor = midColor;
                }
                else if (isMaxRange)
                {
                    hexColor = maxColor;
                }
                
                creator.Bindings.Add("Color", hexColor);
                await creator.Create("Roll").RespondFromXmlAsync(Context);
            }
        }
        catch (Exception)
        {
            await
                EmbedXmlUtils
                    .CreateErrorEmbed("Error", "Error in notation")
                    .RespondFromXmlAsync(Context, ephemeral: true);
        }
    }
}