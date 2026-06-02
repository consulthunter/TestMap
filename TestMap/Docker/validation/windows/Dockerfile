# escape=`

FROM mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022

SHELL ["C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", "-Command"]

WORKDIR /app

ENV DOTNET_ROLL_FORWARD=latestMajor
ENV PATH="C:\\Git\\cmd;C:\\Program Files\\dotnet;C:\\Python312;C:\\Python312\\Scripts;C:\\Users\\ContainerAdministrator\\.dotnet\\tools;C:\\Windows\\system32;C:\\Windows;C:\\Windows\\System32\\WindowsPowerShell\\v1.0"
ENV PYTHONPATH="C:\\app\\runner\\src"

RUN Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1

RUN Set-ExecutionPolicy Bypass -Scope Process -Force
RUN ./dotnet-install.ps1 -Channel 9.0 -InstallDir 'C:\Program Files\dotnet'
RUN ./dotnet-install.ps1 -Channel 8.0 -InstallDir 'C:\Program Files\dotnet'
RUN ./dotnet-install.ps1 -Channel 7.0 -InstallDir 'C:\Program Files\dotnet'
RUN ./dotnet-install.ps1 -Channel 6.0 -InstallDir 'C:\Program Files\dotnet'

RUN & 'C:\Program Files\dotnet\dotnet.exe' tool install -g dotnet-stryker
RUN & 'C:\Program Files\dotnet\dotnet.exe' tool install -g dotnet-coverage
RUN & 'C:\Program Files\dotnet\dotnet.exe' tool install -g dotnet-reportgenerator-globaltool

RUN Invoke-WebRequest "https://www.python.org/ftp/python/3.12.2/python-3.12.2-amd64.exe" -OutFile python-installer.exe ; `
    Start-Process python-installer.exe -ArgumentList '/quiet InstallAllUsers=1 TargetDir=C:\Python312 PrependPath=1' -Wait ; `
    Remove-Item python-installer.exe

RUN Invoke-WebRequest "https://github.com/git-for-windows/git/releases/download/v2.53.0.windows.1/PortableGit-2.53.0-64-bit.7z.exe" -OutFile portablegit.exe ; `
    Start-Process .\portablegit.exe -ArgumentList '-y', '-oC:\Git' -Wait ; `
    Remove-Item portablegit.exe ; `
    & 'C:\Git\cmd\git.exe' --version

SHELL ["cmd", "/S", "/C"]

RUN echo Downloading VS Build Tools bootstrapper... `
    && curl.exe -L --fail --show-error --output C:\vs_buildtools.exe https://aka.ms/vs/stable/vs_buildtools.exe `
    && echo Download complete `
    && dir C:\vs_buildtools.exe

RUN echo Installing VS Build Tools... `
    && C:\vs_buildtools.exe --quiet --wait --norestart --nocache `
        --installPath "%ProgramFiles(x86)%\Microsoft Visual Studio\BuildTools" `
        --add Microsoft.VisualStudio.Workload.AzureBuildTools `
        --add Microsoft.VisualStudio.Workload.MSBuildTools `
        --add Microsoft.Net.Component.4.8.SDK `
        --add Microsoft.Net.Component.4.8.TargetingPack `
        --add Microsoft.Net.ComponentGroup.4.8.DeveloperTools `
        --add Microsoft.Net.Component.4.8.1.SDK `
        --add Microsoft.Net.Component.4.8.1.TargetingPack `
        --add Microsoft.Net.ComponentGroup.4.8.1.DeveloperTools `
        --add Microsoft.Net.Component.4.7.2.TargetingPack `
        --add Microsoft.Net.Component.4.7.1.TargetingPack `
        --add Microsoft.Net.Component.4.7.TargetingPack `
        --add Microsoft.Net.Component.4.6.2.TargetingPack `
        --add Microsoft.Net.ComponentGroup.4.6.2-4.7.1.DeveloperTools `
        --add Microsoft.VisualStudio.Component.NuGet.BuildTools `
        --remove Microsoft.VisualStudio.Component.Windows10SDK.10240 `
        --remove Microsoft.VisualStudio.Component.Windows10SDK.10586 `
        --remove Microsoft.VisualStudio.Component.Windows10SDK.14393 `
        --remove Microsoft.VisualStudio.Component.Windows81SDK `
    || IF "%ERRORLEVEL%"=="3010" EXIT 0

RUN echo VS Build Tools install completed `
    && del /q C:\vs_buildtools.exe

SHELL ["C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe", "-Command"]

RUN git --version

RUN New-Item -ItemType Directory -Path C:\app\runner\src -Force
COPY runner/src/testmap_runner C:/app/runner/src/testmap_runner/

ENTRYPOINT ["C:\\Program Files (x86)\\Microsoft Visual Studio\\BuildTools\\Common7\\Tools\\VsDevCmd.bat", "&&", "powershell.exe", "-NoLogo", "-ExecutionPolicy", "Bypass"]
