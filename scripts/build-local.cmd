SET VERSION_NUMBER=1.0.1
SET /A BuildNumber+=1
SET VERSION_STRING=%VERSION_NUMBER%-preview%BuildNumber%
dotnet pack CommonLibraries.sln -o \dev\local-dev\ /p:Version=%VERSION_STRING%
