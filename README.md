# Suggest Backend

## Local development

1. Set secrets like described in https://github.com/Suggest-App/SGSecrets.
2. Run `docker compose up -d` to start the database
3. Run `dotnet run` from the SGBackend project directory (one dir down).

If there are any database changes you have to reset the database (since migrations are not implemented yet). 
To do that run `docker compose down -v`, followed by `docker compose up -d` to create a fresh database.

When you then run `dotnet run`, the backend will initialize the database again on startup.