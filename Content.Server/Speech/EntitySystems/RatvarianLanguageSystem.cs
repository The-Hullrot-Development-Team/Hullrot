﻿using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffect;
using Microsoft.Extensions.Primitives;

namespace Content.Server.Speech.EntitySystems;

public sealed class RatvarianLanguageSystem : SharedRatvarianLanguageSystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    //TODO: Need to add the other ratvarian language rules

    /*
     * Any time the word "of" occurs, it's linked to the previous word by a hyphen: "I am-of Ratvar"
     * Any time "th", followed by any two letters occurs, you add a grave (`) between those two letters: "Thi`s"
     * In the same vein, any time "ti" followed by one letter occurs, you add a grave (`) between "i" and the letter: "Ti`me"
     * Wherever "te" or "et" appear and there is another letter next to the "e", add a hyphen between "e" and the letter: "M-etal/Greate-r"
     * Where "gua" appears, add a hyphen between "gu" and "a": "Gu-ard"
     * Where the word "and" appears it's linked to all surrounding words by hyphens: "Sword-and-shield"
     * Where the word "to" appears, it's linked to the following word by a hyphen: "to-use"
     * Where the word "my" appears, it's linked to the following word by a hyphen: "my-light"
     * Any Ratvarian proper noun is not translated: Ratvar, Nezbere, Sevtug, Nzcrentr and Inath-neq
        * This only applies if they're being used as a proper noun: armorer/Nezbere
     */

    //TODO: Make a class of or put the Regex options into the component
    private const string RatvarianKey = "RatvarianLanguage";

    private static Regex THPattern = new Regex(@"th\w\B", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex ETPattern = new Regex(@"\Bet", RegexOptions.Compiled);
    private static Regex TEPattern = new Regex(@"te\B",RegexOptions.Compiled);
    private static Regex OFPattern = new Regex(@"(\s)(of)");
    private static Regex TIPattern = new Regex(@"ti\B", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex GUAPattern = new Regex(@"(gu)(a)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex ANDPattern = new Regex(@"\b(\s)(and)(\s)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex TOMYPattern = new Regex(@"(to|my)\s", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static Regex ProperNouns = new Regex(@"(ratvar)|(nezbere)|(sevtuq)|(nzcrentr)|(inath-neq)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // see if you can run the replacements in the regex
    // regex [th] th\w\B IgnoreCase, ($&` Replace) OR (th\w)(\w) ($1`$2)
    // regex [et] \Bet -$&
    // regex [te] te\B  $&-
    // regex [of] (\s)(of) -$2 Replace
    // regex [ti] ti\B Ignore Case ($&` Replace)
    // regex [gua] (gu)(a) ignore case $1-$2
    // regex [and] \b(\s)(and)(\s) ignore case -$2-
    // regex [to] [my] (to|my)\s ignore case $1-

    // Message (input) > Translation (output)

    //1 - Take in message:
    //"This timid metal granted of Ratvar's armorer shall guard and guide me to see my victory"
    //2 - Run the pre translation checks (ratvarian rules) (looping or regex)
    //  regex: tbd
    //  looping: split by space or take in the entire sentence (won't work by character)
    //3 - Rules implemented:
    //"Thi`s ti`mid m-etal grante-d-of Ratvar's armorer shall gu-ard-and-guide me to-see my-victory"
    //4 - block out proper nouns: Ratvar's
    //Run translation and output:
    //"Guv`f gv`zvq z-rgny tenagr-q-bs Ratvar's nezbere funyy th-neq-naq-thvqr zr gb-frr zl-ivpgbel"

    //Notes: you may need to take in stutters

    public override void Initialize()
    {
        SubscribeLocalEvent<RatvarianLanguageComponent, AccentGetEvent>(OnAccent);
    }

    public override void DoRatvarian(EntityUid uid, TimeSpan time, bool refresh, StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return;

        _statusEffects.TryAddStatusEffect<RatvarianLanguageComponent>(uid, RatvarianKey, time, refresh, status);
    }

    private void OnAccent(EntityUid uid, RatvarianLanguageComponent component, AccentGetEvent args)
    {
        args.Message = Translate(args.Message);
    }

    private string Translate(string message)
    {
        var ruleTranslation = message;
        var finalMessage = new StringBuilder();
        var newWord = new StringBuilder();

        ruleTranslation = THPattern.Replace(ruleTranslation, "$&`");
        ruleTranslation = TEPattern.Replace(ruleTranslation, "$&-");
        ruleTranslation = ETPattern.Replace(ruleTranslation, "-$&");
        ruleTranslation = OFPattern.Replace(ruleTranslation, "-$2");
        ruleTranslation = TIPattern.Replace(ruleTranslation, "$&`");
        ruleTranslation = GUAPattern.Replace(ruleTranslation, "$1-$2");
        ruleTranslation = ANDPattern.Replace(ruleTranslation, "-$2-");
        ruleTranslation = TOMYPattern.Replace(ruleTranslation, "$1-");

        var temp = ruleTranslation.Split(' ');

        foreach (var word in temp)
        {
            newWord.Clear();

            if (ProperNouns.IsMatch(word))
                newWord.Append(word);

            else
            {
                for (int i = 0; i < word.Length; i++)
                {
                    var letter = word[i];

                    if (letter >= 97 && letter <= 122)
                    {
                        var letterRot = letter + 13;

                        if (letterRot > 122)
                            letterRot -= 26;

                        newWord.Append((char) letterRot);
                    }
                    else if (letter >= 65 && letter <= 90)
                    {
                        var letterRot = letter + 13;

                        if (letterRot > 90)
                            letterRot -= 26;

                        newWord.Append((char) letterRot);
                    }
                    else
                    {
                        newWord.Append(word[i]);
                    }
                }
            }
            finalMessage.Append(newWord + " ");
        }
        return finalMessage.ToString().Trim();
    }
}
