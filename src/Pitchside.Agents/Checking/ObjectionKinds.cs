namespace Pitchside.Agents.Checking;

/// <summary>The kinds of objection the fact-checker raises.</summary>
public static class ObjectionKinds
{
    /// <summary>A digit outside a <c>[[key]]</c> token.</summary>
    public const string UncitedNumber = "uncited_number";

    /// <summary>A sentence with no <c>[[key]]</c> token, so nothing it says is tied to the fact sheet.</summary>
    public const string UncitedSentence = "uncited_sentence";

    /// <summary>A token whose key is not on the fact sheet.</summary>
    public const string UnknownFact = "unknown_fact";

    /// <summary>A key outside the moment's scope: a player not on the moment, or an unknown scope.</summary>
    public const string WrongScope = "wrong_scope";

    /// <summary>A name not in the match document, or a forbidden word.</summary>
    public const string ForbiddenContent = "forbidden_content";

    /// <summary>The model-based check judged a sentence not supported by its cited facts.</summary>
    public const string UnsupportedClaim = "unsupported_claim";

    /// <summary>The model's answer was not narrative JSON, or the call failed.</summary>
    public const string InvalidOutput = "invalid_output";

    /// <summary>The sentence index used for objections about the whole draft.</summary>
    public const string WholeDraft = "*";
}
