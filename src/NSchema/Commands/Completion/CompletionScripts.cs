namespace NSchema.Commands.Completion;

/// <summary>
/// Shell completion scripts. Each registers a completion function that, on tab, calls
/// <c>nschema [suggest:&lt;cursor&gt;] "&lt;command line&gt;"</c> — backed by System.CommandLine's
/// <see cref="System.CommandLine.Completions.SuggestDirective"/> — and feeds the candidates back to the shell.
/// The CLI is its own completion provider, so no external tool (e.g. <c>dotnet-suggest</c>) is required.
/// </summary>
internal static class CompletionScripts
{
    public static string For(string shell) => shell switch
    {
        "bash" => Bash,
        "zsh" => Zsh,
        "fish" => Fish,
        "pwsh" => Pwsh,
        _ => throw new ArgumentOutOfRangeException(nameof(shell), shell, "Unknown shell."),
    };

    private const string Bash =
        "# nschema bash completion.\n" +
        "# Enable for the current shell:  source <(nschema completion bash)\n" +
        "# Or install permanently:        nschema completion bash > /etc/bash_completion.d/nschema\n" +
        "_nschema_complete()\n" +
        "{\n" +
        "    local completions\n" +
        "    completions=\"$(\"${COMP_WORDS[0]}\" \"[suggest:${COMP_POINT}]\" \"${COMP_LINE}\" 2>/dev/null)\"\n" +
        "    COMPREPLY=( $(compgen -W \"${completions}\" -- \"${COMP_WORDS[COMP_CWORD]}\") )\n" +
        "    return 0\n" +
        "}\n" +
        "complete -f -F _nschema_complete nschema\n";

    private const string Zsh =
        "# nschema zsh completion.\n" +
        "# Enable for the current shell:  source <(nschema completion zsh)\n" +
        "# Or install permanently:        nschema completion zsh > \"${fpath[1]}/_nschema\"\n" +
        "_nschema_complete()\n" +
        "{\n" +
        "    local completions=\"$(\"${words[1]}\" \"[suggest:${CURSOR}]\" \"${BUFFER}\" 2>/dev/null)\"\n" +
        "    _values 'completions' ${(ps:\\n:)completions}\n" +
        "}\n" +
        "compdef _nschema_complete nschema\n";

    private const string Fish =
        "# nschema fish completion.\n" +
        "# Enable for the current shell:  nschema completion fish | source\n" +
        "# Or install permanently:        nschema completion fish > ~/.config/fish/completions/nschema.fish\n" +
        "function __nschema_complete\n" +
        "    set -l line (commandline -cp)\n" +
        "    nschema \"[suggest:\"(string length -- $line)\"]\" \"$line\" 2>/dev/null\n" +
        "end\n" +
        "complete -c nschema -f -a '(__nschema_complete)'\n";

    private const string Pwsh =
        "# nschema PowerShell completion.\n" +
        "# Enable for the current session:  nschema completion pwsh | Out-String | Invoke-Expression\n" +
        "# Or add that line to your $PROFILE to install permanently.\n" +
        "Register-ArgumentCompleter -Native -CommandName nschema -ScriptBlock {\n" +
        "    param($wordToComplete, $commandAst, $cursorPosition)\n" +
        "    $line = $commandAst.ToString()\n" +
        "    & nschema \"[suggest:$cursorPosition]\" \"$line\" 2>$null | ForEach-Object {\n" +
        "        [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)\n" +
        "    }\n" +
        "}\n";
}
