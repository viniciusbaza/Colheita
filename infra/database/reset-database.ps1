# Recriar o banco
docker compose down -v
docker compose up -d

# Acessar API
cd ../../
cd .\FarmAndFriends.Api\

# Rodar script de reset da API, apaga todas as migrations e recria o commit inicial do banco
.\reset-dev.ps1