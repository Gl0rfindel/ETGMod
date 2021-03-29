using System;

public class ConsoleCommand : ConsoleCommandUnit {

    public ConsoleCommand(Action<string[]> cmdref, AutocompletionSettings autocompletion) {
        CommandReference = cmdref;
        Autocompletion = autocompletion;
    }

    public ConsoleCommand(Action<string[]> cmdref) {
        CommandReference = cmdref;
        Autocompletion = new AutocompletionSettings(input => Array<string>.Empty);
    }
}

