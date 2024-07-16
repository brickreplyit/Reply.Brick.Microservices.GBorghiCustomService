FROM mcr.microsoft.com/dotnet/sdk:8.0 as build
RUN dotnet dev-certs https --clean
RUN dotnet dev-certs https -t
COPY PublishedMS/ App/
WORKDIR /App
ENTRYPOINT ["dotnet", "Reply.Brick.CSPSConnector.API.dll"]