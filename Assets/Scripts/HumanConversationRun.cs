using System;

[Serializable]
public sealed class HumanDialogueStep
{
    public string customerLine;
    public string[] options;
    public int correctOption;
    public string wrongResponse;
}

// Each accepted device owns its own progress; scenario assets contain only text.
public sealed class HumanConversationRun
{
    private readonly HumanDialogueStep[] steps;
    private readonly string completionLine;
    public string Error { get; }
    public bool IsValid => string.IsNullOrEmpty(Error);
    public int Count => steps.Length;
    public int Credits { get; private set; }
    public int Mistakes { get; private set; }
    public bool Finished => IsValid && Credits == Count;
    public string Line { get; private set; }
    public int OptionCount => !IsValid || Finished ? 0 : steps[Credits].options.Length;

    public HumanConversationRun(HumanDialogueStep[] source, string completed)
    {
        steps = Array.Empty<HumanDialogueStep>();
        completionLine = completed;
        if (source == null || source.Length == 0 || source.Length > 8 || string.IsNullOrWhiteSpace(completed))
        {
            Error = "A Human scenario needs 1-8 steps and a completion line.";
            return;
        }
        var snapshot = new HumanDialogueStep[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var s = source[i];
            if (s == null || string.IsNullOrWhiteSpace(s.customerLine) || string.IsNullOrWhiteSpace(s.wrongResponse)
                || s.options == null || s.options.Length < 2 || s.options.Length > 3
                || s.correctOption < 0 || s.correctOption >= s.options.Length
                || Array.Exists(s.options, string.IsNullOrWhiteSpace))
            {
                Error = "Each Human step needs a clue, 2-3 choices, a valid answer, and useful wrong-answer feedback.";
                return;
            }
            snapshot[i] = new HumanDialogueStep {
                customerLine = s.customerLine, options = (string[])s.options.Clone(),
                correctOption = s.correctOption, wrongResponse = s.wrongResponse
            };
        }
        steps = snapshot;
        Line = steps[0].customerLine;
    }

    public string Option(int index) => index >= 0 && index < OptionCount ? steps[Credits].options[index] : "";

    public bool Choose(int index)
    {
        if (index < 0 || index >= OptionCount) return false;
        if (index != steps[Credits].correctOption)
        {
            Mistakes++;
            Line = steps[Credits].wrongResponse;
            return true;
        }
        Credits++;
        Line = Finished ? completionLine : steps[Credits].customerLine;
        return true;
    }
}
