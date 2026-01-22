# Recriar o banco
docker compose down -v
docker compose up -d

# Acessar API
cd ../../
cd .\FarmAndFriends.Api\

# Rodar script de reset da API
.\reset-dev.ps1