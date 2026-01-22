cd .\infra\database\

# Subir banco
docker compose up -d

# Atualizar banco
cd ../../
cd .\FarmAndFriends.Api\
dotnet ef database update

# Rodar API
dotnet run