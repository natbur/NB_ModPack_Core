REM Copy this file to "PostBuild.bat"
REM This is a local file copy after build.  Get it once and your .gitnore
REM should handle it after that.  Make all your local copies at the end.

REM Set this to your local RimWorld install path and CCL Assemblies directory
Set solutionPath=%3
Set InstalledCCLAssemblies="%solutionPath:"=%..\Assemblies"

if NOT EXIST %InstalledCCLAssemblies% (
    echo Missing or invalid copy target:
    echo %InstalledCCLAssemblies%
    EXIT -1
)

echo Build Config: %1
echo Build Target: %2
echo Solution Path: %3
echo CCL Install Path: %InstalledCCLAssemblies%

echo Copy to RimWorld
copy %2 %InstalledCCLAssemblies%

:Finished
