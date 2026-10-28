FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FlightBookingSystem/FlightBookingSystem.csproj FlightBookingSystem/
RUN dotnet restore FlightBookingSystem/FlightBookingSystem.csproj
COPY FlightBookingSystem/ FlightBookingSystem/
RUN dotnet publish FlightBookingSystem/FlightBookingSystem.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "FlightBookingSystem.dll"]
