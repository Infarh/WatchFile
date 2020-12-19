@echo off
dotnet publish -p:PublishProfile=win32 --nologo
move release\win32\WatchFile.exe .\WatchFile32.exe

dotnet publish -p:PublishProfile=win64 --nologo
move release\win64\WatchFile.exe .\WatchFile64.exe

rmdir /s /q release