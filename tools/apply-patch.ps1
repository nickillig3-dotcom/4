param(
    [Parameter(Mandatory=$true)]
    [string]$PatchFile
)
Set-Location -Path (git rev-parse --show-toplevel)
git am --3way "$PatchFile"
