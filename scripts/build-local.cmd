SET VERSION_NUMBER=1.0.1
SET /A BuildNumber+=1
dotnet pack CommonLibraries.sln -o \dev\local-dev\ /p:Version=%VERSION_NUMBER%-preview%BuildNumber%
