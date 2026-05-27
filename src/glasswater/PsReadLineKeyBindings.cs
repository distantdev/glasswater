namespace glasswater;

public static class PsReadLineKeyBindings
{
    // Replacement must run here (not from C# reflection) so PSReadLine is on the read-loop thread.
    public const string InstallScript = @"
Set-PSReadLineKeyHandler -Chord 'RightArrow' -BriefDescription 'glasswater-accept-nl' -ScriptBlock {
    param($key, $arg)
    $replacement = [glasswater.GlasswaterAccept]::TakePendingReplacement()
    if (-not [string]::IsNullOrWhiteSpace($replacement)) {
        $replacement = $replacement.Trim()
        $buf = $null
        $cursor = 0
        [Microsoft.PowerShell.PSConsoleReadLine]::GetBufferState([ref]$buf, [ref]$cursor) | Out-Null
        $lineStart = 0
        $prevNewline = $buf.LastIndexOf([char]10, [Math]::Max(0, $cursor - 1))
        if ($prevNewline -ge 0) { $lineStart = $prevNewline + 1 }
        $lineEnd = $buf.IndexOf([char]10, $cursor)
        if ($lineEnd -lt 0) { $lineEnd = $buf.Length }
        $lineLength = $lineEnd - $lineStart
        [Microsoft.PowerShell.PSConsoleReadLine]::Replace($lineStart, $lineLength, $replacement)
        [glasswater.GlasswaterSession]::OnNaturalLanguageAccepted($replacement)
        return
    }
    [Microsoft.PowerShell.PSConsoleReadLine]::AcceptSuggestion($key, $arg)
}
";

    public const string UninstallScript =
        "Set-PSReadLineKeyHandler -Chord 'RightArrow' -Function AcceptSuggestion";
}
